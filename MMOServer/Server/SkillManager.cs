using MMOServer.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace MMOServer.Server
{
    /// <summary>
    /// ✅ VERSÃO MELHORADA - Sistema completo de Skills com segurança e performance
    /// </summary>
    public class SkillManager
    {
        private static SkillManager? instance;
        public static SkillManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new SkillManager();
                return instance;
            }
        }

        // ✅ Thread-safe para múltiplos jogadores
        private readonly ConcurrentDictionary<int, SkillTemplate> skillTemplates = new();
        private readonly ConcurrentDictionary<string, List<ActiveEffect>> activeEffects = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, long>> playerCooldowns = new();
        
        private readonly Random random = new();
        private int nextEffectId = 1;
        private readonly object effectIdLock = new();

        public void Initialize()
        {
            Console.WriteLine("⚔️ SkillManager: Initializing...");
            LoadSkillTemplates();
            
            if (skillTemplates.Count == 0)
            {
                Console.WriteLine("❌ CRITICAL: No skills loaded! Check Config/skills.json");
                return;
            }

            Console.WriteLine($"✅ SkillManager: Loaded {skillTemplates.Count} skill templates");
            
            // Log primeiras 3 skills
            foreach (var skill in skillTemplates.Values.Take(3))
            {
                Console.WriteLine($"   - [{skill.id}] {skill.name} ({skill.requiredClass})");
            }
        }

        // ==================== CONFIGURAÇÃO ====================

        private void LoadSkillTemplates()
        {
            string filePath = Path.Combine("Config", "skills.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ {filePath} not found!");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var config = JsonConvert.DeserializeObject<SkillConfig>(json);

                if (config?.skills != null)
                {
                    foreach (var skill in config.skills)
                    {
                        // ✅ VALIDAÇÃO: Garante que skill tem dados válidos
                        if (skill.id <= 0 || string.IsNullOrEmpty(skill.name))
                        {
                            Console.WriteLine($"⚠️ Invalid skill detected, skipping: ID={skill.id}");
                            continue;
                        }

                        skillTemplates[skill.id] = skill;
                    }
                    
                    Console.WriteLine($"✅ Loaded {skillTemplates.Count} valid skills");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading skills: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
            }
        }

        public SkillTemplate? GetSkillTemplate(int skillId)
        {
            skillTemplates.TryGetValue(skillId, out var template);
            return template;
        }

        public List<SkillTemplate> GetSkillsByClass(string className)
        {
            return skillTemplates.Values
                .Where(s => string.Equals(s.requiredClass, className, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.requiredLevel)
                .ThenBy(s => s.id)
                .ToList();
        }

        // ==================== USO DE SKILLS ====================

        /// <summary>
        /// ✅ MELHORADO - Usa skill com validações completas e thread-safety
        /// </summary>
        public SkillResult UseSkill(Player player, UseSkillRequest request, float currentTime)
        {
            var result = new SkillResult
            {
                attackerId = player.sessionId,
                attackerName = player.character.nome,
                attackerType = "player"
            };

            // ✅ VALIDAÇÃO #1: Player válido
            if (player.character.isDead)
            {
                result.success = false;
                result.failReason = "PLAYER_DEAD";
                return result;
            }

            // ✅ VALIDAÇÃO #2: Skill aprendida
            var learnedSkill = player.character.learnedSkills?
                .FirstOrDefault(s => s.skillId == request.skillId);

            if (learnedSkill == null)
            {
                result.success = false;
                result.failReason = "SKILL_NOT_LEARNED";
                Console.WriteLine($"⚠️ {player.character.nome} tried to use unlearned skill {request.skillId}");
                return result;
            }

            // ✅ VALIDAÇÃO #3: Template existe
            var template = GetSkillTemplate(request.skillId);
            if (template == null)
            {
                result.success = false;
                result.failReason = "SKILL_NOT_FOUND";
                return result;
            }

            learnedSkill.template = template;

            // ✅ VALIDAÇÃO #4: Cooldown (thread-safe)
            if (!CanUseSkill(player.sessionId, learnedSkill, currentTime))
            {
                result.success = false;
                result.failReason = "COOLDOWN";
                return result;
            }

            // ✅ VALIDAÇÃO #5: Level data válido
            var levelData = GetSkillLevelData(template, learnedSkill.currentLevel);
            if (levelData == null)
            {
                result.success = false;
                result.failReason = "INVALID_LEVEL";
                return result;
            }

            // ✅ VALIDAÇÃO #6: Custos
            if (player.character.mana < template.manaCost)
            {
                result.success = false;
                result.failReason = "NO_MANA";
                return result;
            }

            if (player.character.health <= template.healthCost)
            {
                result.success = false;
                result.failReason = "NO_HEALTH";
                return result;
            }

            // ✅ VALIDAÇÃO #7: Range (se aplicável)
            if (!ValidateSkillRange(player, template, request))
            {
                result.success = false;
                result.failReason = "OUT_OF_RANGE";
                return result;
            }

            // ✅ EXECUÇÃO: Consome recursos
            player.character.mana -= template.manaCost;
            player.character.health -= template.healthCost;
            result.manaCost = template.manaCost;
            result.healthCost = template.healthCost;

            // ✅ ATUALIZA: Cooldown (thread-safe)
            SetSkillCooldown(player.sessionId, request.skillId, currentTime);

            // ✅ EXECUTA: Skill
            result.success = true;
            ExecuteSkill(player, template, levelData, request, result, currentTime);

            // ✅ SALVA: Character
            try
            {
                DatabaseHandler.Instance.UpdateCharacter(player.character);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving character after skill use: {ex.Message}");
            }

            return result;
        }

        // ✅ NOVO: Validação de range thread-safe
        private bool ValidateSkillRange(Player player, SkillTemplate template, UseSkillRequest request)
        {
            if (template.targetType != "enemy" || string.IsNullOrEmpty(request.targetId))
                return true;

            if (!int.TryParse(request.targetId, out int monsterId))
                return false;

            var monster = MonsterManager.Instance.GetMonster(monsterId);
            if (monster == null || !monster.isAlive)
                return false;

            float distance = GetDistance(player.position, monster.position);
            return distance <= template.range;
        }

        // ✅ MELHORADO: Sistema de cooldown thread-safe
        private bool CanUseSkill(string playerId, LearnedSkill learnedSkill, float currentTime)
        {
            if (learnedSkill.template == null)
                return false;

            if (!playerCooldowns.TryGetValue(playerId, out var cooldowns))
                return true;

            if (!cooldowns.TryGetValue(learnedSkill.skillId, out long lastUsedMs))
                return true;

            float lastUsed = lastUsedMs / 1000f;
            float timeSinceLastUse = currentTime - lastUsed;

            return timeSinceLastUse >= learnedSkill.template.cooldown;
        }

        // ✅ NOVO: Define cooldown thread-safe
        private void SetSkillCooldown(string playerId, int skillId, float currentTime)
        {
            var cooldowns = playerCooldowns.GetOrAdd(playerId, _ => new ConcurrentDictionary<int, long>());
            long currentTimeMs = (long)(currentTime * 1000);
            cooldowns[skillId] = currentTimeMs;
        }

        // ✅ MELHORADO: Execução de skills mais robusta
        private void ExecuteSkill(Player player, SkillTemplate template, SkillLevelData levelData, 
            UseSkillRequest request, SkillResult result, float currentTime)
        {
            try
            {
                switch (template.targetType)
                {
                    case "enemy":
                        ExecuteSingleTargetSkill(player, template, levelData, request, result, currentTime);
                        break;

                    case "area":
                        ExecuteAreaSkill(player, template, levelData, request, result, currentTime);
                        break;

                    case "self":
                        ExecuteSelfSkill(player, template, levelData, result, currentTime);
                        break;

                    case "ally":
                        ExecuteAllySkill(player, template, levelData, request, result, currentTime);
                        break;

                    default:
                        Console.WriteLine($"⚠️ Unknown target type: {template.targetType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error executing skill {template.name}: {ex.Message}");
                result.success = false;
                result.failReason = "EXECUTION_ERROR";
            }
        }

        private void ExecuteSingleTargetSkill(Player player, SkillTemplate template, SkillLevelData levelData,
            UseSkillRequest request, SkillResult result, float currentTime)
        {
            if (string.IsNullOrEmpty(request.targetId) || !int.TryParse(request.targetId, out int monsterId))
                return;

            var monster = MonsterManager.Instance.GetMonster(monsterId);
            if (monster == null || !monster.isAlive)
                return;

            var targetResult = CalculateSkillDamage(player, monster, template, levelData);
            
            // ✅ THREAD-SAFE: Lock durante aplicação de dano
            lock (monster)
            {
                int actualDamage = monster.TakeDamage(targetResult.damage);
                targetResult.damage = actualDamage;
                targetResult.remainingHealth = monster.currentHealth;
                targetResult.targetDied = !monster.isAlive;

                // XP e level up
                if (targetResult.targetDied)
                {
                    int exp = CombatManager.Instance.CalculateExperienceReward(
                        player.character.level, monster.template.level, monster.template.experienceReward);
                    
                    bool leveledUp = player.character.GainExperience(exp);

                    targetResult.experienceGained = exp;
                    targetResult.leveledUp = leveledUp;
                    targetResult.newLevel = player.character.level;

                    Console.WriteLine($"💀 {monster.template.name} killed by {template.name}! +{exp} XP");
                }
            }

            // Aplica efeitos
            ApplySkillEffects(player, monster, template, targetResult, currentTime);

            result.targets.Add(targetResult);
        }

        private void ExecuteAreaSkill(Player player, SkillTemplate template, SkillLevelData levelData,
            UseSkillRequest request, SkillResult result, float currentTime)
        {
            Position center = request.targetPosition ?? player.position;
            var monsters = MonsterManager.Instance.GetAliveMonsters();

            // ✅ OTIMIZAÇÃO: Filtra monsters por distância antes de processar
            var monstersInRange = monsters
                .Where(m => GetDistance(center, m.position) <= template.areaRadius)
                .ToList();

            foreach (var monster in monstersInRange)
            {
                var targetResult = CalculateSkillDamage(player, monster, template, levelData);
                
                lock (monster)
                {
                    int actualDamage = monster.TakeDamage(targetResult.damage);
                    targetResult.damage = actualDamage;
                    targetResult.remainingHealth = monster.currentHealth;
                    targetResult.targetDied = !monster.isAlive;

                    if (targetResult.targetDied)
                    {
                        int exp = CombatManager.Instance.CalculateExperienceReward(
                            player.character.level, monster.template.level, monster.template.experienceReward);
                        
                        bool leveledUp = player.character.GainExperience(exp);

                        targetResult.experienceGained = exp;
                        targetResult.leveledUp = leveledUp;
                        targetResult.newLevel = player.character.level;
                    }
                }

                ApplySkillEffects(player, monster, template, targetResult, currentTime);
                result.targets.Add(targetResult);
            }

            if (result.targets.Count > 0)
            {
                Console.WriteLine($"💥 {template.name} hit {result.targets.Count} targets in area!");
            }
        }

        private void ExecuteSelfSkill(Player player, SkillTemplate template, SkillLevelData levelData,
            SkillResult result, float currentTime)
        {
            var targetResult = new SkillTargetResult
            {
                targetId = player.sessionId,
                targetName = player.character.nome,
                targetType = "player"
            };

            // Cura
            if (levelData.baseHealing > 0)
            {
                int healing = CalculateHealing(player, template, levelData);
                int oldHealth = player.character.health;
                
                player.character.health = Math.Min(player.character.health + healing, player.character.maxHealth);
                
                targetResult.healing = healing;
                targetResult.remainingHealth = player.character.health;
                
                Console.WriteLine($"💚 {player.character.nome} healed {healing} HP ({oldHealth} → {player.character.health})");
            }

            // Aplica buffs
            foreach (var effect in template.effects)
            {
                if (effect.effectType == "buff_stat")
                {
                    ApplyBuff(player.sessionId, player.sessionId, template.id, effect, currentTime);
                    
                    targetResult.appliedEffects.Add(new AppliedEffect
                    {
                        effectType = effect.effectType,
                        value = effect.value,
                        duration = effect.duration
                    });
                }
            }

            result.targets.Add(targetResult);
        }

        private void ExecuteAllySkill(Player player, SkillTemplate template, SkillLevelData levelData,
            UseSkillRequest request, SkillResult result, float currentTime)
        {
            // TODO: Implementar party system
            ExecuteSelfSkill(player, template, levelData, result, currentTime);
        }

        // ==================== CÁLCULOS ====================

        private SkillTargetResult CalculateSkillDamage(Player player, MonsterInstance monster, 
            SkillTemplate template, SkillLevelData levelData)
        {
            var result = new SkillTargetResult
            {
                targetId = monster.id.ToString(),
                targetName = monster.template.name,
                targetType = "monster"
            };

            // Dano base
            int baseDamage = levelData.baseDamage;

            // Escala com ATK ou MATK
            int attackPower = template.damageType == "magical" 
                ? player.character.magicPower 
                : player.character.attackPower;

            int scaledDamage = (int)(attackPower * levelData.damageMultiplier);
            int totalDamage = baseDamage + scaledDamage;

            // Crítico
            float critChance = template.damageType == "magical"
                ? 0.05f + (player.character.intelligence * 0.002f)
                : 0.01f + (player.character.dexterity * 0.003f);
            
            critChance += levelData.critChanceBonus;
            critChance = Math.Clamp(critChance, 0f, 0.75f);

            result.isCritical = random.NextDouble() < critChance;
            if (result.isCritical)
            {
                totalDamage = (int)(totalDamage * 1.5f);
            }

            // Redução de defesa
            int defense = monster.template.defense;
            float defReduction = 1.0f - (defense / (float)(defense + 100));
            defReduction = Math.Max(defReduction, 0.1f);

            totalDamage = (int)(totalDamage * defReduction);
            totalDamage = Math.Max(1, totalDamage);

            result.damage = totalDamage;

            return result;
        }

        private int CalculateHealing(Player player, SkillTemplate template, SkillLevelData levelData)
        {
            int baseHealing = levelData.baseHealing;
            int scaledHealing = (int)(player.character.magicPower * levelData.damageMultiplier);
            return Math.Max(1, baseHealing + scaledHealing);
        }

        // ==================== EFEITOS E BUFFS ====================

        private void ApplySkillEffects(Player player, MonsterInstance monster, SkillTemplate template,
            SkillTargetResult targetResult, float currentTime)
        {
            foreach (var effect in template.effects)
            {
                if (random.NextDouble() <= effect.chance)
                {
                    // TODO: Implementar stun, dot, etc
                    targetResult.appliedEffects.Add(new AppliedEffect
                    {
                        effectType = effect.effectType,
                        value = effect.value,
                        duration = effect.duration
                    });
                }
            }
        }

        private void ApplyBuff(string targetId, string sourceId, int skillId, SkillEffect effect, float currentTime)
        {
            var effects = activeEffects.GetOrAdd(targetId, _ => new List<ActiveEffect>());

            int effectId;
            lock (effectIdLock)
            {
                effectId = nextEffectId++;
            }

            var activeEffect = new ActiveEffect
            {
                id = effectId,
                skillId = skillId,
                effectType = effect.effectType,
                targetStat = effect.targetStat,
                value = effect.value,
                startTime = currentTime,
                duration = effect.duration,
                sourceId = sourceId
            };

            lock (effects)
            {
                effects.Add(activeEffect);
            }

            Console.WriteLine($"✨ Buff applied: {effect.targetStat} +{effect.value} for {effect.duration}s");
        }

        public void UpdateActiveEffects(float currentTime)
        {
            foreach (var kvp in activeEffects.ToList())
            {
                var effects = kvp.Value;
                
                lock (effects)
                {
                    effects.RemoveAll(e => e.IsExpired(currentTime));
                }

                if (effects.Count == 0)
                {
                    activeEffects.TryRemove(kvp.Key, out _);
                }
            }
        }

        public List<ActiveEffect> GetActiveEffects(string playerId)
        {
            if (activeEffects.TryGetValue(playerId, out var effects))
            {
                lock (effects)
                {
                    return effects.ToList();
                }
            }
            return new List<ActiveEffect>();
        }

        // ==================== APRENDIZADO DE SKILLS ====================

        public (bool canLearn, string reason) CanLearnSkill(Character character, int skillId)
        {
            var template = GetSkillTemplate(skillId);
            
            if (template == null)
                return (false, "Skill não encontrada");

            if (character.level < template.requiredLevel)
                return (false, $"Nível insuficiente (requer {template.requiredLevel})");

            if (!string.IsNullOrEmpty(template.requiredClass) && 
                !string.Equals(template.requiredClass, character.classe, StringComparison.OrdinalIgnoreCase))
                return (false, $"Classe incorreta (requer {template.requiredClass})");

            if (character.learnedSkills?.Any(s => s.skillId == skillId) == true)
                return (false, "Skill já aprendida");

            return (true, "OK");
        }

        public bool LearnSkill(Player player, int skillId, int slotNumber)
        {
            var template = GetSkillTemplate(skillId);
            
            if (template == null)
            {
                Console.WriteLine($"❌ Skill {skillId} not found");
                return false;
            }

            var (canLearn, reason) = CanLearnSkill(player.character, skillId);
            
            if (!canLearn)
            {
                Console.WriteLine($"❌ Cannot learn skill: {reason}");
                return false;
            }

            if (slotNumber < 1 || slotNumber > 9)
            {
                Console.WriteLine($"❌ Invalid slot: {slotNumber}");
                return false;
            }

            // Inicializa lista se null
            player.character.learnedSkills ??= new List<LearnedSkill>();

            // Remove skill anterior do slot
            var oldSkillInSlot = player.character.learnedSkills.FirstOrDefault(s => s.slotNumber == slotNumber);
            if (oldSkillInSlot != null)
            {
                oldSkillInSlot.slotNumber = 0;
            }

            // Adiciona skill
            var learnedSkill = new LearnedSkill
            {
                skillId = skillId,
                currentLevel = 1,
                slotNumber = slotNumber,
                lastUsedTime = 0
            };

            player.character.learnedSkills.Add(learnedSkill);
            
            // Salva
            try
            {
                DatabaseHandler.Instance.SaveCharacterSkills(player.character.id, player.character.learnedSkills);
                Console.WriteLine($"✅ {player.character.nome} learned {template.name} (Slot {slotNumber})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving learned skill: {ex.Message}");
                return false;
            }
        }

        public bool LevelUpSkill(Player player, int skillId)
        {
            player.character.learnedSkills ??= new List<LearnedSkill>();

            var learnedSkill = player.character.learnedSkills.FirstOrDefault(s => s.skillId == skillId);
            
            if (learnedSkill == null)
            {
                Console.WriteLine($"❌ Skill not learned");
                return false;
            }

            var template = GetSkillTemplate(skillId);
            if (template == null)
                return false;

            if (learnedSkill.currentLevel >= template.maxLevel)
            {
                Console.WriteLine($"❌ Skill already at max level");
                return false;
            }

            var nextLevelData = GetSkillLevelData(template, learnedSkill.currentLevel + 1);
            if (nextLevelData == null)
                return false;

            if (player.character.statusPoints < nextLevelData.statusPointCost)
            {
                Console.WriteLine($"❌ Not enough status points: {player.character.statusPoints} < {nextLevelData.statusPointCost}");
                return false;
            }

            // Consome points
            player.character.statusPoints -= nextLevelData.statusPointCost;
            learnedSkill.currentLevel++;

            // Salva
            try
            {
                DatabaseHandler.Instance.UpdateCharacter(player.character);
                DatabaseHandler.Instance.SaveCharacterSkills(player.character.id, player.character.learnedSkills);
                Console.WriteLine($"✅ {template.name} leveled up to {learnedSkill.currentLevel}!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving skill level up: {ex.Message}");
                return false;
            }
        }

        // ==================== HELPERS ====================

        private SkillLevelData? GetSkillLevelData(SkillTemplate template, int level)
        {
            return template.levels?.FirstOrDefault(l => l.level == level);
        }

        private float GetDistance(Position pos1, Position pos2)
        {
            float dx = pos1.x - pos2.x;
            float dz = pos1.z - pos2.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public void ReloadConfigs()
        {
            Console.WriteLine("🔄 Reloading skill configurations...");
            skillTemplates.Clear();
            LoadSkillTemplates();
            Console.WriteLine("✅ Skill configurations reloaded!");
        }

        [Serializable]
        public class SkillConfig
        {
            public List<SkillTemplate> skills { get; set; } = new();
        }
    }
}
