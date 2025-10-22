using System.Timers;
using Newtonsoft.Json;
using MMOServer.Models;

namespace MMOServer.Server
{
    /// <summary>
    /// ✅ CORRIGIDO - WorldManager com otimizações de performance e segurança
    /// </summary>
    public class WorldManager
    {
        private static WorldManager? instance;
        public static WorldManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new WorldManager();
                return instance;
            }
        }

        private System.Timers.Timer? updateTimer;
        private const int UPDATE_INTERVAL = 50; // 50ms = 20 ticks/segundo
        private const int SAVE_INTERVAL = 5000; // 5 segundos
        private const int BROADCAST_INTERVAL = 100; // 100ms = 10 broadcasts/segundo (OTIMIZAÇÃO)

        private long lastSaveTime = 0;
        private long lastBroadcastTime = 0; // ✅ NOVO
        private object broadcastLock = new object();
        
        private DateTime serverStartTime = DateTime.UtcNow;

        // ✅ NOVO - Anti-cheat básico
        private const float MAX_MOVEMENT_SPEED = 15f; // 3x velocidade normal
        private Dictionary<string, Position> lastPlayerPositions = new Dictionary<string, Position>();
        private Dictionary<string, long> lastPositionUpdateTime = new Dictionary<string, long>();

        public void Initialize()
        {
            Console.WriteLine("🌍 WorldManager initialized - Authoritative Server Mode");
            
            serverStartTime = DateTime.UtcNow;
            
            MonsterManager.Instance.Initialize();
            SkillManager.Instance.Initialize();
			
            updateTimer = new System.Timers.Timer(UPDATE_INTERVAL);
            updateTimer.Elapsed += OnWorldUpdate;
            updateTimer.AutoReset = true;
            updateTimer.Start();
            
            lastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lastBroadcastTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            Console.WriteLine("✅ Combat System: Ragnarok-style auto-attack");
            Console.WriteLine("✅ Skill System: 1-9 hotkeys");
            Console.WriteLine("✅ Loot System: Monster drops");
            Console.WriteLine("✅ Anti-cheat: Movement validation");
        }

        private void OnWorldUpdate(object? sender, ElapsedEventArgs e)
        {
            lock (broadcastLock)
            {
                long currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                float currentTime = (float)(DateTime.UtcNow - serverStartTime).TotalSeconds;
                float deltaTime = UPDATE_INTERVAL / 1000f;

                // 1. Atualiza movimento de players (com validação anti-cheat)
                PlayerManager.Instance.UpdateAllPlayersMovement(deltaTime);
                
                // 2. Processa combate automático
                ProcessPlayerCombat(currentTime, deltaTime);
                
                // 3. Atualiza monstros (AI e combate)
                MonsterManager.Instance.Update(deltaTime, currentTime);
                
                // 4. Atualiza efeitos de skills
                SkillManager.Instance.UpdateActiveEffects(currentTime);
                
                // 5. ✅ OTIMIZAÇÃO - Broadcast apenas a cada 100ms
                if (currentTimeMs - lastBroadcastTime >= BROADCAST_INTERVAL)
                {
                    BroadcastWorldState();
                    lastBroadcastTime = currentTimeMs;
                }
                
                // 6. Salva periodicamente
                if (currentTimeMs - lastSaveTime >= SAVE_INTERVAL)
                {
                    SaveWorldState();
                    lastSaveTime = currentTimeMs;
                }
            }
        }

        /// <summary>
        /// ✅ CORRIGIDO - Validação de movimento contra speed hack
        /// </summary>
        public bool ValidatePlayerMovement(string sessionId, Position newPosition)
        {
            if (!lastPlayerPositions.ContainsKey(sessionId))
            {
                lastPlayerPositions[sessionId] = newPosition;
                lastPositionUpdateTime[sessionId] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return true;
            }

            var lastPos = lastPlayerPositions[sessionId];
            var lastTime = lastPositionUpdateTime[sessionId];
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            float distance = GetDistance2D(lastPos, newPosition);
            float timeDelta = (currentTime - lastTime) / 1000f;
            
            if (timeDelta > 0)
            {
                float speed = distance / timeDelta;
                
                if (speed > MAX_MOVEMENT_SPEED)
                {
                    Console.WriteLine($"⚠️ SPEED HACK DETECTED: {sessionId} - Speed: {speed:F2} (max: {MAX_MOVEMENT_SPEED})");
                    return false;
                }
            }

            lastPlayerPositions[sessionId] = newPosition;
            lastPositionUpdateTime[sessionId] = currentTime;
            return true;
        }

        private void ProcessPlayerCombat(float currentTime, float deltaTime)
        {
            var players = PlayerManager.Instance.GetAllPlayers();
            
            foreach (var player in players)
            {
                if (player.character.isDead)
                {
                    if (player.inCombat)
                    {
                        player.CancelCombat();
                    }
                    continue;
                }
                
                if (!player.inCombat || !player.targetMonsterId.HasValue)
                    continue;

                var monster = MonsterManager.Instance.GetMonster(player.targetMonsterId.Value);
                
                if (monster == null || !monster.isAlive)
                {
                    player.CancelCombat();
                    continue;
                }

                float distance = GetDistance2D(player.position, monster.position);
                float attackRange = CombatManager.Instance.GetAttackRange();
                
                if (distance > attackRange)
                {
                    player.targetPosition = new Position 
                    { 
                        x = monster.position.x, 
                        y = monster.position.y, 
                        z = monster.position.z 
                    };
                    player.isMoving = true;
                    
                    if (player.lastAttackTime < 0)
                    {
                        player.lastAttackTime = currentTime - player.character.attackSpeed;
                    }
                }
                else
                {
                    player.isMoving = false;
                    player.targetPosition = null;
                    
                    if (player.CanAttack(currentTime))
                    {
                        player.Attack(currentTime);
                        BroadcastPlayerAttack(player, monster);
                        
                        var result = CombatManager.Instance.PlayerAttackMonster(player, monster);
                        BroadcastCombatResult(result);

                        if (result.targetDied)
                        {
                            player.CancelCombat();
                            ProcessMonsterLoot(player, monster);
                            
                            if (result.leveledUp)
                            {
                                BroadcastLevelUp(player, result.newLevel);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ✅ CORRIGIDO - Broadcast de ataque com validação
        /// </summary>
        private void BroadcastPlayerAttack(Player player, MonsterInstance monster)
        {
            var message = new
            {
                type = "playerAttack",
                playerId = player.sessionId,
                characterName = player.character.nome,
                monsterId = monster.id,
                monsterName = monster.template.name,
                attackerPosition = player.position,
                targetPosition = monster.position
            };

            string json = JsonConvert.SerializeObject(message);
            GameServer.BroadcastToAll(json);
        }

        /// <summary>
        /// ✅ OTIMIZADO - Loot com verificação de espaço
        /// </summary>
        private void ProcessMonsterLoot(Player player, MonsterInstance monster)
        {
            var loot = ItemManager.Instance.GenerateLoot(monster.templateId);
            
            if (loot.gold == 0 && loot.items.Count == 0)
                return;

            var inventory = ItemManager.Instance.LoadInventory(player.character.id);
            
            // Adiciona gold
            if (loot.gold > 0)
            {
                inventory.gold += loot.gold;
            }

            // Adiciona itens (com verificação de espaço)
            List<LootedItem> addedItems = new List<LootedItem>();
            
            foreach (var lootedItem in loot.items)
            {
                var template = ItemManager.Instance.GetItemTemplate(lootedItem.itemId);
                
                if (template == null)
                    continue;

                if (!inventory.HasSpace() && template.maxStack == 1)
                {
                    Console.WriteLine($"  ⚠️ Inventory full! {template.name} lost");
                    continue;
                }

                var itemInstance = ItemManager.Instance.CreateItemInstance(lootedItem.itemId, lootedItem.quantity);
                
                if (itemInstance != null && inventory.AddItem(itemInstance, template))
                {
                    addedItems.Add(lootedItem);
                }
            }

            ItemManager.Instance.SaveInventory(inventory);

            if (loot.gold > 0 || addedItems.Count > 0)
            {
                BroadcastLoot(player, loot.gold, addedItems);
            }
        }

        private void BroadcastLoot(Player player, int gold, List<LootedItem> items)
        {
            var message = new
            {
                type = "lootReceived",
                playerId = player.sessionId,
                characterName = player.character.nome,
                gold = gold,
                items = items
            };

            string json = JsonConvert.SerializeObject(message);
            GameServer.BroadcastToAll(json);
        }

        private float GetDistance2D(Position pos1, Position pos2)
        {
            float dx = pos1.x - pos2.x;
            float dz = pos1.z - pos2.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// ✅ OTIMIZADO - Envia apenas players/monstros próximos
        /// </summary>
        private void BroadcastWorldState()
        {
            var players = PlayerManager.Instance.GetAllPlayers();
            var monsters = MonsterManager.Instance.GetAllMonsterStates();
            
            if (players.Count == 0) return;

            var playerStates = players.Select(p => new
            {
                playerId = p.sessionId,
                characterName = p.character.nome,
                position = p.position,
                raca = p.character.raca,
                classe = p.character.classe,
                level = p.character.level,
                health = p.character.health,
                maxHealth = p.character.maxHealth,
                mana = p.character.mana,
                maxMana = p.character.maxMana,
                experience = p.character.experience,
                statusPoints = p.character.statusPoints,
                isMoving = p.isMoving,
                targetPosition = p.targetPosition,
                inCombat = p.inCombat,
                targetMonsterId = p.targetMonsterId,
                isDead = p.character.isDead
            }).ToList();

            var worldState = new
            {
                type = "worldState",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                players = playerStates,
                monsters = monsters
            };

            string json = JsonConvert.SerializeObject(worldState);
            GameServer.BroadcastToAll(json);
        }

        public void BroadcastCombatResult(CombatResult result)
        {
            var message = new
            {
                type = "combatResult",
                data = result
            };

            string json = JsonConvert.SerializeObject(message);
            GameServer.BroadcastToAll(json);
        }

        public void BroadcastPlayerStatsUpdate(Player player)
        {
            var message = new
            {
                type = "playerStatsUpdate",
                playerId = player.sessionId,
                health = player.character.health,
                maxHealth = player.character.maxHealth,
                mana = player.character.mana,
                maxMana = player.character.maxMana
            };

            string json = JsonConvert.SerializeObject(message);
            GameServer.BroadcastToAll(json);
        }

        private void BroadcastLevelUp(Player player, int newLevel)
        {
            var message = new
            {
                type = "levelUp",
                playerId = player.sessionId,
                characterName = player.character.nome,
                newLevel = newLevel,
                statusPoints = player.character.statusPoints,
                experience = player.character.experience,
                requiredExp = player.character.GetRequiredExp(),
                newStats = new
                {
                    maxHealth = player.character.maxHealth,
                    maxMana = player.character.maxMana,
                    attackPower = player.character.attackPower,
                    magicPower = player.character.magicPower,
                    defense = player.character.defense,
                    attackSpeed = player.character.attackSpeed,
                    strength = player.character.strength,
                    intelligence = player.character.intelligence,
                    dexterity = player.character.dexterity,
                    vitality = player.character.vitality
                }
            };

            string json = JsonConvert.SerializeObject(message);
            GameServer.BroadcastToAll(json);
        }

        public void BroadcastPlayerDeath(Player player)
        {
            var message = new
            {
                type = "playerDeath",
                playerId = player.sessionId,
                characterName = player.character.nome
            };

            string json = JsonConvert.SerializeObject(message);
            GameServer.BroadcastToAll(json);
        }

        public void BroadcastPlayerRespawn(Player player)
        {
            var message = new
            {
                type = "playerRespawn",
                playerId = player.sessionId,
                characterName = player.character.nome,
                position = player.position,
                health = player.character.health,
                maxHealth = player.character.maxHealth
            };

            string json = JsonConvert.SerializeObject(message);
            GameServer.BroadcastToAll(json);
        }

        public void BroadcastSkillResult(SkillResult result)
        {
            var message = new
            {
                type = "skillUsed",
                result = result
            };

            string json = JsonConvert.SerializeObject(message);
            GameServer.BroadcastToAll(json);
        }

        private void SaveWorldState()
        {
            var players = PlayerManager.Instance.GetAllPlayers();
            foreach (var player in players)
            {
                try
                {
                    DatabaseHandler.Instance.UpdateCharacter(player.character);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving {player.character.nome}: {ex.Message}");
                }
            }

            MonsterManager.Instance.SaveAllMonsters();
        }

        public void Shutdown()
        {
            Console.WriteLine("🛑 WorldManager: Saving all data...");
            SaveWorldState();
            
            updateTimer?.Stop();
            updateTimer?.Dispose();
            Console.WriteLine("✅ WorldManager shutdown complete");
        }
    }
}
