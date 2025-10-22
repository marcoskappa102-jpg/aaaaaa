using MMOServer.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace MMOServer.Server
{
    /// <summary>
    /// ✅ VERSÃO PROFISSIONAL - ItemManager com validações robustas
    /// MELHORIAS:
    /// - Validação ANTES de consumir item (não desperdiça)
    /// - Thread-safe item instance ID
    /// - Validação de equipamentos (nível, classe, já equipado)
    /// - Proteção contra item duplication
    /// - Cooldown de poções por tipo (não global)
    /// </summary>
    public class ItemManager
    {
        private static ItemManager? instance;
        public static ItemManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new ItemManager();
                return instance;
            }
        }

        private readonly ConcurrentDictionary<int, ItemTemplate> itemTemplates = new();
        private readonly ConcurrentDictionary<int, LootTable> lootTables = new();
        private int nextInstanceId = 1;
        private readonly Random random = new();
        
        // ✅ CORREÇÃO: Cooldown separado por tipo de poção
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DateTime>> playerPotionCooldowns = new();
        private const double POTION_COOLDOWN_SECONDS = 1.0;

        // ✅ NOVO: Thread-safe instance ID
        private readonly object instanceIdLock = new();

        public void Initialize()
        {
            Console.WriteLine("📦 ItemManager - Professional Edition v2.0");
            
            LoadItemTemplates();
            LoadLootTables();
            LoadInstanceIdCounter();
            
            Console.WriteLine($"✅ Loaded {itemTemplates.Count} items and {lootTables.Count} loot tables");
            Console.WriteLine("   ✅ Item Validation");
            Console.WriteLine("   ✅ Cooldown System");
            Console.WriteLine("   ✅ Thread-Safe Operations");
        }

        // ==================== ITEM TEMPLATES ====================

        private void LoadItemTemplates()
        {
            string filePath = Path.Combine("Config", "items.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"⚠️ {filePath} not found!");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var config = JsonConvert.DeserializeObject<ItemConfig>(json);

                if (config?.items != null)
                {
                    foreach (var item in config.items)
                    {
                        // Validações
                        if (item.id <= 0)
                        {
                            Console.WriteLine($"⚠️ Invalid item ID: {item.id}");
                            continue;
                        }

                        if (string.IsNullOrEmpty(item.name))
                        {
                            Console.WriteLine($"⚠️ Item {item.id} has no name");
                            continue;
                        }

                        itemTemplates[item.id] = item;
                    }
                    
                    Console.WriteLine($"✅ Loaded {itemTemplates.Count} item templates");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading items: {ex.Message}");
            }
        }

        // ==================== LOOT TABLES ====================

        private void LoadLootTables()
        {
            string filePath = Path.Combine("Config", "loot_tables.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"⚠️ {filePath} not found!");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine($"⚠️ {filePath} is empty!");
                    return;
                }

                var config = JsonConvert.DeserializeObject<LootConfig>(json);

                if (config?.lootTables != null)
                {
                    foreach (var table in config.lootTables)
                    {
                        if (table == null) continue;

                        table.drops ??= new List<ItemDrop>();
                        table.guaranteedGold ??= new GoldDrop { min = 0, max = 0 };

                        lootTables[table.monsterId] = table;
                    }
                    
                    Console.WriteLine($"✅ Loaded {lootTables.Count} loot tables");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading loot tables: {ex.Message}");
            }
        }

        public LootResult GenerateLoot(int monsterId)
        {
            var result = new LootResult();

            if (!lootTables.TryGetValue(monsterId, out var table))
            {
                result.gold = random.Next(5, 15);
                return result;
            }

            result.gold = random.Next(table.guaranteedGold.min, table.guaranteedGold.max + 1);

            foreach (var drop in table.drops)
            {
                double roll = random.NextDouble();
                
                if (roll <= drop.dropChance)
                {
                    int quantity = random.Next(drop.minQuantity, drop.maxQuantity + 1);
                    
                    result.items.Add(new LootedItem
                    {
                        itemId = drop.itemId,
                        itemName = drop.itemName,
                        quantity = quantity
                    });
                }
            }

            return result;
        }

        // ==================== ITEM INSTANCES ====================

        /// <summary>
        /// ✅ CORRIGIDO - Thread-safe item instance creation
        /// </summary>
        public ItemInstance CreateItemInstance(int templateId, int quantity = 1)
        {
            var template = GetItemTemplate(templateId);
            
            if (template == null)
            {
                Console.WriteLine($"⚠️ Item template {templateId} not found!");
                return null!;
            }

            int instanceId;
            lock (instanceIdLock)
            {
                instanceId = nextInstanceId++;
                SaveInstanceIdCounter();
            }

            var instance = new ItemInstance
            {
                instanceId = instanceId,
                templateId = templateId,
                quantity = Math.Min(quantity, template.maxStack),
                template = template
            };

            return instance;
        }

        public ItemTemplate? GetItemTemplate(int itemId)
        {
            itemTemplates.TryGetValue(itemId, out var template);
            return template;
        }

        // ==================== INVENTÁRIO ====================

        public Inventory LoadInventory(int characterId)
        {
            var inventory = DatabaseHandler.Instance.LoadInventory(characterId);
            
            // Carrega templates
            foreach (var item in inventory.items)
            {
                if (item.template == null)
                {
                    item.template = GetItemTemplate(item.templateId);
                    
                    if (item.template == null)
                    {
                        Console.WriteLine($"⚠️ Template {item.templateId} not found for item {item.instanceId}!");
                    }
                }
            }
            
            return inventory;
        }

        public void SaveInventory(Inventory inventory)
        {
            DatabaseHandler.Instance.SaveInventory(inventory);
        }

        public bool AddItemToPlayer(string sessionId, int itemId, int quantity = 1)
        {
            var player = PlayerManager.Instance.GetPlayer(sessionId);
            
            if (player == null)
                return false;

            var template = GetItemTemplate(itemId);
            
            if (template == null)
                return false;

            var itemInstance = CreateItemInstance(itemId, quantity);
            
            if (itemInstance == null)
                return false;

            var inventory = LoadInventory(player.character.id);
            bool success = inventory.AddItem(itemInstance, template);
            
            if (success)
            {
                SaveInventory(inventory);
                Console.WriteLine($"📦 {player.character.nome} received {quantity}x {template.name}");
            }
            
            return success;
        }

        public bool RemoveItemFromPlayer(string sessionId, int instanceId, int quantity = 1)
        {
            try
            {
                var player = PlayerManager.Instance.GetPlayer(sessionId);
                
                if (player == null)
                {
                    Console.WriteLine($"❌ Player not found: {sessionId}");
                    return false;
                }

                var inventory = LoadInventory(player.character.id);
                var item = inventory.GetItem(instanceId);
                
                if (item == null)
                {
                    Console.WriteLine($"❌ Item {instanceId} not found");
                    return false;
                }

                // ✅ VALIDAÇÃO: Não pode dropar item equipado
                if (item.isEquipped)
                {
                    Console.WriteLine($"❌ Cannot drop equipped item {instanceId}");
                    return false;
                }

                if (item.quantity < quantity)
                {
                    Console.WriteLine($"❌ Not enough quantity (has {item.quantity}, need {quantity})");
                    return false;
                }

                bool success = inventory.RemoveItem(instanceId, quantity);
                
                if (success)
                {
                    SaveInventory(inventory);
                    
                    string itemName = item.template?.name ?? "Unknown Item";
                    Console.WriteLine($"📤 {player.character.nome} dropped {quantity}x {itemName}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in RemoveItemFromPlayer: {ex.Message}");
                return false;
            }
        }

        // ==================== USE ITEM ====================

        /// <summary>
        /// ✅ CORRIGIDO - Valida ANTES de consumir, cooldown por tipo
        /// </summary>
        public string UseItem(string sessionId, int instanceId)
        {
            var player = PlayerManager.Instance.GetPlayer(sessionId);
            
            if (player == null)
                return "PLAYER_NOT_FOUND";
            
            if (player.character.isDead)
                return "PLAYER_DEAD";

            var inventory = LoadInventory(player.character.id);
            var item = inventory.GetItem(instanceId);
            
            if (item == null)
                return "ITEM_NOT_FOUND";

            if (item.template == null)
            {
                item.template = GetItemTemplate(item.templateId);
            }

            var template = item.template;
            
            if (template == null)
                return "TEMPLATE_NOT_FOUND";

            if (template.type != "consumable")
                return "NOT_CONSUMABLE";

            // ✅ VALIDAÇÃO 1: Verifica cooldown POR TIPO de poção
            string cooldownKey = $"{sessionId}_{template.effectTarget}"; // Ex: "session123_health"
            
            var playerCooldowns = playerPotionCooldowns.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, DateTime>());
            
            if (playerCooldowns.TryGetValue(template.effectTarget, out var lastUse))
            {
                var timeSinceLastUse = (DateTime.UtcNow - lastUse).TotalSeconds;
                
                if (timeSinceLastUse < POTION_COOLDOWN_SECONDS)
                {
                    double remaining = POTION_COOLDOWN_SECONDS - timeSinceLastUse;
                    Console.WriteLine($"⏳ {player.character.nome} potion on cooldown ({remaining:F1}s)");
                    return "ON_COOLDOWN";
                }
            }

            // ✅ VALIDAÇÃO 2: Verifica se pode usar ANTES de consumir
            if (template.effectType == "heal")
            {
                if (template.effectTarget == "health")
                {
                    if (player.character.health >= player.character.maxHealth)
                    {
                        Console.WriteLine($"⚠️ {player.character.nome} HP already full");
                        return "HP_FULL";
                    }
                }
                else if (template.effectTarget == "mana")
                {
                    if (player.character.mana >= player.character.maxMana)
                    {
                        Console.WriteLine($"⚠️ {player.character.nome} MP already full");
                        return "MP_FULL";
                    }
                }
            }

            // ✅ APLICA EFEITO
            bool effectApplied = false;
            int oldValue = 0;
            int newValue = 0;
            
            if (template.effectType == "heal")
            {
                if (template.effectTarget == "health")
                {
                    oldValue = player.character.health;
                    player.character.health = Math.Min(player.character.health + template.effectValue, player.character.maxHealth);
                    newValue = player.character.health;
                    int healed = newValue - oldValue;
                    
                    Console.WriteLine($"💊 {player.character.nome} healed {healed} HP with {template.name}");
                    effectApplied = true;
                }
                else if (template.effectTarget == "mana")
                {
                    oldValue = player.character.mana;
                    player.character.mana = Math.Min(player.character.mana + template.effectValue, player.character.maxMana);
                    newValue = player.character.mana;
                    int restored = newValue - oldValue;
                    
                    Console.WriteLine($"💊 {player.character.nome} restored {restored} MP with {template.name}");
                    effectApplied = true;
                }
            }

            if (effectApplied)
            {
                // ✅ Atualiza cooldown POR TIPO
                playerCooldowns[template.effectTarget] = DateTime.UtcNow;
                
                // Remove item do inventário
                inventory.RemoveItem(instanceId, 1);
                SaveInventory(inventory);
                DatabaseHandler.Instance.UpdateCharacter(player.character);
                
                WorldManager.Instance.BroadcastPlayerStatsUpdate(player);
                return "SUCCESS";
            }

            return "NO_EFFECT";
        }

        // ==================== EQUIP ITEM ====================

        /// <summary>
        /// ✅ CORRIGIDO - Validações robustas antes de equipar
        /// </summary>
        public bool EquipItem(string sessionId, int instanceId)
        {
            var player = PlayerManager.Instance.GetPlayer(sessionId);
            
            if (player == null)
            {
                Console.WriteLine($"❌ EquipItem: Player not found");
                return false;
            }

            var inventory = LoadInventory(player.character.id);
            var item = inventory.GetItem(instanceId);
            
            if (item == null)
            {
                Console.WriteLine($"❌ EquipItem: Item {instanceId} not found");
                return false;
            }

            if (item.template == null)
            {
                item.template = GetItemTemplate(item.templateId);
            }

            var template = item.template;
            
            if (template == null)
            {
                Console.WriteLine($"❌ EquipItem: Template not found for item {item.templateId}");
                return false;
            }

            if (template.type != "equipment")
            {
                Console.WriteLine($"❌ EquipItem: Item is not equipment");
                return false;
            }

            // ✅ VALIDAÇÃO 1: Nível requerido
            if (player.character.level < template.requiredLevel)
            {
                Console.WriteLine($"⚠️ {player.character.nome} can't equip {template.name} (Level {player.character.level} < {template.requiredLevel})");
                return false;
            }

            // ✅ VALIDAÇÃO 2: Classe requerida
            if (!string.IsNullOrEmpty(template.requiredClass) && 
                !string.Equals(template.requiredClass, player.character.classe, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"⚠️ {player.character.nome} can't equip {template.name} (Wrong class: {player.character.classe} != {template.requiredClass})");
                return false;
            }

            // ✅ VALIDAÇÃO 3: Já está equipado
            if (item.isEquipped)
            {
                Console.WriteLine($"⚠️ Item {instanceId} is already equipped");
                return false;
            }

            string slot = template.slot;
            
            // Obtém item antigo no slot
            int? oldItemId = slot switch
            {
                "weapon" => inventory.weaponId,
                "armor" => inventory.armorId,
                "helmet" => inventory.helmetId,
                "boots" => inventory.bootsId,
                "gloves" => inventory.glovesId,
                "ring" => inventory.ringId,
                "necklace" => inventory.necklaceId,
                _ => null
            };

            // Desequipa item antigo
            if (oldItemId.HasValue)
            {
                var oldItem = inventory.GetItem(oldItemId.Value);
                
                if (oldItem != null)
                {
                    // Verifica se tem espaço para desquipar
                    int usedSlots = inventory.items.Count(i => !i.isEquipped);
                    int availableSlots = inventory.maxSlots - usedSlots;
                    
                    if (availableSlots <= 0)
                    {
                        Console.WriteLine($"⚠️ No space to unequip old item");
                        return false;
                    }
                    
                    oldItem.isEquipped = false;
                }
            }

            // Equipa novo item
            item.isEquipped = true;
            
            switch (slot)
            {
                case "weapon": inventory.weaponId = instanceId; break;
                case "armor": inventory.armorId = instanceId; break;
                case "helmet": inventory.helmetId = instanceId; break;
                case "boots": inventory.bootsId = instanceId; break;
                case "gloves": inventory.glovesId = instanceId; break;
                case "ring": inventory.ringId = instanceId; break;
                case "necklace": inventory.necklaceId = instanceId; break;
                default:
                    Console.WriteLine($"❌ Unknown slot: {slot}");
                    return false;
            }

            // Recalcula stats
            RecalculatePlayerStats(player, inventory);
            SaveInventory(inventory);
            DatabaseHandler.Instance.UpdateCharacter(player.character);
            
            Console.WriteLine($"⚔️ {player.character.nome} equipped {template.name}");
            return true;
        }

        // ==================== UNEQUIP ITEM ====================

        /// <summary>
        /// ✅ CORRIGIDO - Validações e limpeza de estado corrompido
        /// </summary>
        public bool UnequipItem(string sessionId, string slot)
        {
            try
            {
                var player = PlayerManager.Instance.GetPlayer(sessionId);
                
                if (player == null)
                {
                    Console.WriteLine($"❌ UnequipItem: Player not found: {sessionId}");
                    return false;
                }

                var inventory = LoadInventory(player.character.id);
                
                // Determina qual item está equipado no slot
                int? itemId = slot switch
                {
                    "weapon" => inventory.weaponId,
                    "armor" => inventory.armorId,
                    "helmet" => inventory.helmetId,
                    "boots" => inventory.bootsId,
                    "gloves" => inventory.glovesId,
                    "ring" => inventory.ringId,
                    "necklace" => inventory.necklaceId,
                    _ => null
                };

                if (!itemId.HasValue)
                {
                    Console.WriteLine($"⚠️ No item equipped in slot '{slot}'");
                    return false;
                }

                var item = inventory.GetItem(itemId.Value);
                
                if (item == null)
                {
                    Console.WriteLine($"❌ Item {itemId.Value} not found in inventory (corrupted state)");
                    
                    // ✅ Limpa o slot corrompido
                    switch (slot)
                    {
                        case "weapon": inventory.weaponId = null; break;
                        case "armor": inventory.armorId = null; break;
                        case "helmet": inventory.helmetId = null; break;
                        case "boots": inventory.bootsId = null; break;
                        case "gloves": inventory.glovesId = null; break;
                        case "ring": inventory.ringId = null; break;
                        case "necklace": inventory.necklaceId = null; break;
                    }
                    
                    SaveInventory(inventory);
                    return false;
                }

                // ✅ VALIDAÇÃO: Verifica se tem espaço no inventário
                int usedSlots = inventory.items.Count(i => !i.isEquipped);
                int availableSlots = inventory.maxSlots - usedSlots;
                
                if (availableSlots <= 0)
                {
                    Console.WriteLine($"⚠️ Inventory full, cannot unequip");
                    return false;
                }

                // Desequipa o item
                item.isEquipped = false;
                
                // Remove do slot de equipamento
                switch (slot)
                {
                    case "weapon": inventory.weaponId = null; break;
                    case "armor": inventory.armorId = null; break;
                    case "helmet": inventory.helmetId = null; break;
                    case "boots": inventory.bootsId = null; break;
                    case "gloves": inventory.glovesId = null; break;
                    case "ring": inventory.ringId = null; break;
                    case "necklace": inventory.necklaceId = null; break;
                    default:
                        Console.WriteLine($"❌ Invalid slot '{slot}'");
                        return false;
                }

                // Recalcula stats do player
                RecalculatePlayerStats(player, inventory);
                
                // Salva alterações
                SaveInventory(inventory);
                DatabaseHandler.Instance.UpdateCharacter(player.character);
                
                Console.WriteLine($"⚔️ {player.character.nome} unequipped {item.template?.name ?? "item"} from {slot}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in UnequipItem: {ex.Message}");
                return false;
            }
        }

        // ==================== STATS CALCULATION ====================

        /// <summary>
        /// ✅ Recalcula stats do personagem com equipamentos
        /// </summary>
        private void RecalculatePlayerStats(Player player, Inventory inventory)
        {
            // Reseta para stats base
            player.character.RecalculateStats();

            var equippedIds = new[] 
            { 
                inventory.weaponId, 
                inventory.armorId, 
                inventory.helmetId, 
                inventory.bootsId, 
                inventory.glovesId, 
                inventory.ringId, 
                inventory.necklaceId 
            };

            int totalBonusStr = 0;
            int totalBonusInt = 0;
            int totalBonusDex = 0;
            int totalBonusVit = 0;
            int totalBonusHP = 0;
            int totalBonusMP = 0;
            int totalBonusAtk = 0;
            int totalBonusMAtk = 0;
            int totalBonusDef = 0;
            float totalBonusAspd = 0f;

            foreach (var itemId in equippedIds)
            {
                if (!itemId.HasValue)
                    continue;

                var item = inventory.GetItem(itemId.Value);
                
                if (item?.template == null)
                    continue;

                var t = item.template;

                totalBonusStr += t.bonusStrength;
                totalBonusInt += t.bonusIntelligence;
                totalBonusDex += t.bonusDexterity;
                totalBonusVit += t.bonusVitality;
                totalBonusHP += t.bonusMaxHealth;
                totalBonusMP += t.bonusMaxMana;
                totalBonusAtk += t.bonusAttackPower;
                totalBonusMAtk += t.bonusMagicPower;
                totalBonusDef += t.bonusDefense;
                totalBonusAspd += t.bonusAttackSpeed;
            }

            // Aplica bônus dos equipamentos
            player.character.strength += totalBonusStr;
            player.character.intelligence += totalBonusInt;
            player.character.dexterity += totalBonusDex;
            player.character.vitality += totalBonusVit;
            player.character.maxHealth += totalBonusHP;
            player.character.maxMana += totalBonusMP;
            player.character.attackPower += totalBonusAtk;
            player.character.magicPower += totalBonusMAtk;
            player.character.defense += totalBonusDef;
            player.character.attackSpeed += totalBonusAspd;

            // Recalcula com os novos valores
            player.character.RecalculateStats();

            // ✅ Garante que HP/MP atual não exceda o máximo
            player.character.health = Math.Min(player.character.health, player.character.maxHealth);
            player.character.mana = Math.Min(player.character.mana, player.character.maxMana);

            Console.WriteLine($"📊 {player.character.nome} stats recalculated:");
            Console.WriteLine($"   ATK={player.character.attackPower} DEF={player.character.defense} HP={player.character.maxHealth}/{player.character.health}");
        }

        // ==================== PERSISTÊNCIA ====================

        private void LoadInstanceIdCounter()
        {
            nextInstanceId = DatabaseHandler.Instance.GetNextItemInstanceId();
            Console.WriteLine($"   Next item instance ID: {nextInstanceId}");
        }

        private void SaveInstanceIdCounter()
        {
            DatabaseHandler.Instance.SaveNextItemInstanceId(nextInstanceId);
        }

        public void ReloadConfigs()
        {
            Console.WriteLine("🔄 Reloading item configurations...");
            itemTemplates.Clear();
            lootTables.Clear();
            LoadItemTemplates();
            LoadLootTables();
            Console.WriteLine("✅ Item configurations reloaded!");
        }

        // ==================== UTILIDADES ====================

        /// <summary>
        /// ✅ Valida se player pode usar item (ownership check)
        /// </summary>
        public bool ValidateItemOwnership(string sessionId, int instanceId)
        {
            var player = PlayerManager.Instance.GetPlayer(sessionId);
            
            if (player == null)
                return false;

            var inventory = LoadInventory(player.character.id);
            return inventory.GetItem(instanceId) != null;
        }

        /// <summary>
        /// ✅ Obtém estatísticas do inventário
        /// </summary>
        public string GetInventoryStats(int characterId)
        {
            var inventory = LoadInventory(characterId);
            
            int usedSlots = inventory.items.Count(i => !i.isEquipped);
            int equippedItems = inventory.items.Count(i => i.isEquipped);
            int totalWeight = inventory.items.Sum(i => i.quantity);
            
            return $"Inventory: {usedSlots}/{inventory.maxSlots} slots | {equippedItems} equipped | {inventory.gold} gold | {totalWeight} items";
        }

        /// <summary>
        /// ✅ Limpa cooldowns expirados (manutenção)
        /// </summary>
        public void CleanupExpiredCooldowns()
        {
            var expiredPlayers = new List<string>();

            foreach (var kvp in playerPotionCooldowns)
            {
                var playerCooldowns = kvp.Value;
                var expiredTypes = playerCooldowns
                    .Where(x => (DateTime.UtcNow - x.Value).TotalMinutes > 5)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var type in expiredTypes)
                {
                    playerCooldowns.TryRemove(type, out _);
                }

                if (playerCooldowns.IsEmpty)
                {
                    expiredPlayers.Add(kvp.Key);
                }
            }

            foreach (var playerId in expiredPlayers)
            {
                playerPotionCooldowns.TryRemove(playerId, out _);
            }

            if (expiredPlayers.Count > 0)
            {
                Console.WriteLine($"🧹 Cleaned {expiredPlayers.Count} expired cooldown entries");
            }
        }
    }

    [Serializable]
    public class ItemConfig
    {
        public List<ItemTemplate> items { get; set; } = new List<ItemTemplate>();
    }

    [Serializable]
    public class LootConfig
    {
        public List<LootTable> lootTables { get; set; } = new List<LootTable>();
    }
}
