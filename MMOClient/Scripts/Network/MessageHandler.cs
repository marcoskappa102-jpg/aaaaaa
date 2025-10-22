using UnityEngine;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

public class MessageHandler : MonoBehaviour
{
    public static MessageHandler Instance { get; private set; }
	
    // Eventos para cada tipo de mensagem
    public event Action<LoginResponseData> OnLoginResponse;
    public event Action<RegisterResponseData> OnRegisterResponse;
    public event Action<CreateCharacterResponseData> OnCreateCharacterResponse;
    public event Action<SelectCharacterResponseData> OnSelectCharacterResponse;
    public event Action<PlayerJoinedData> OnPlayerJoined;
    public event Action<string> OnPlayerDisconnected;
    public event Action<WorldStateData> OnWorldStateUpdate;
    
    public event Action<CombatResultData> OnCombatResult;
    public event Action<LevelUpData> OnLevelUp;
    public event Action<PlayerDeathData> OnPlayerDeath;
    public event Action<PlayerRespawnData> OnPlayerRespawn;
    public event Action<AttackStartedData> OnAttackStarted;
    public event Action<PlayerAttackData> OnPlayerAttack;
    public event Action<StatusPointAddedData> OnStatusPointAdded;
    
    public event Action<InventoryData> OnInventoryReceived;
    public event Action<LootReceivedData> OnLootReceived;
    public event Action<ItemUsedData> OnItemUsed;
    public event Action<ItemEquippedData> OnItemEquipped;
    public event Action<ItemEquippedData> OnItemUnequipped;
    public event Action<ItemDroppedData> OnItemDropped;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (ClientManager.Instance != null)
        {
            ClientManager.Instance.OnMessageReceived += HandleMessage;
        }
    }

    private void HandleMessage(string message)
    {
        try
        {
            var json = JObject.Parse(message);
            var type = json["type"]?.ToString();

            // 🔍 LOG para debug - mostra o tipo de mensagem recebida
            Debug.Log($"📨 Message type: '{type}'");

            switch (type)
            {
				case "pong":
					// Silencioso - ping/pong funcionando
					break;
                // ==================== LOGIN/ACCOUNT ====================
                case "loginResponse":
                    HandleLoginResponse(json);
                    break;

                case "registerResponse":
                    HandleRegisterResponse(json);
                    break;

                // ==================== CHARACTER ====================
                case "createCharacterResponse":
                    HandleCreateCharacterResponse(json);
                    break;

                case "selectCharacterResponse":
                    HandleSelectCharacterResponse(json);
                    break;

                // ==================== WORLD/PLAYERS ====================
                case "playerJoined":
                    HandlePlayerJoined(json);
                    break;

                case "playerDisconnected":
                    HandlePlayerDisconnected(json);
                    break;

                case "worldState":
                    HandleWorldState(json);
                    break;

                // ==================== MOVEMENT ====================
                case "moveAccepted":
                    Debug.Log("✅ Move accepted");
                    break;

                // ==================== COMBAT ====================
                case "combatResult":
                    HandleCombatResult(json);
                    break;

                case "attackStarted":
                    HandleAttackStarted(json);
                    break;
                
                case "playerAttack":
                    HandlePlayerAttack(json);
                    break;

                // ==================== LEVEL/STATS ====================
                case "levelUp":
                    HandleLevelUp(json);
                    break;
                
                case "statusPointAdded":
                    HandleStatusPointAdded(json);
                    break;

                // ✅ CRÍTICO: Atualização de stats (HP/MP após usar poção)
                case "playerStatsUpdate":
                    HandlePlayerStatsUpdate(json);
                    break;

                // ==================== DEATH/RESPAWN ====================
                case "playerDeath":
                    HandlePlayerDeath(json);
                    break;

                case "playerRespawn":
                    HandlePlayerRespawn(json);
                    break;

                case "respawnResponse":
                    Debug.Log("✅ Respawn response received");
                    break;

                // ==================== INVENTORY/ITEMS ====================
                case "inventoryResponse":
                    HandleInventoryResponse(json);
                    break;

                case "lootReceived":
                    HandleLootReceived(json);
                    break;

                case "itemUsed":
                    HandleItemUsed(json);
                    break;

                case "itemUseFailed":
                    HandleItemUseFailed(json);
                    break;

                case "itemEquipped":
                    HandleItemEquipped(json);
                    break;

                case "itemUnequipped":
                    HandleItemUnequipped(json);
                    break;

                case "itemDropped":
                    HandleItemDropped(json);
                    break;
					

	
case "skillUsed":
    HandleSkillUsed(json);
    break;

case "skillUseFailed":
    HandleSkillUseFailed(json);
    break;

case "skillLearned":
    HandleSkillLearned(json);
    break;

case "skillLeveledUp":
    HandleSkillLeveledUp(json);
    break;

case "skillListResponse":
    HandleSkillListResponse(json);
    break;
	
	case "skillsResponse":
    HandleSkillsResponse(json);
    break;


                // ==================== ERRORS ====================
                case "error":
                    string errorMsg = json["message"]?.ToString() ?? "Unknown error";
                    Debug.LogError($"❌ Server error: {errorMsg}");
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.AddCombatLog($"<color=red>❌ Erro: {errorMsg}</color>");
                    }
                    break;

                // ==================== UNKNOWN ====================
                default:
                    Debug.LogWarning($"⚠️ Unknown message type: '{type}'");
                    Debug.LogWarning($"   Full message: {message.Substring(0, Math.Min(200, message.Length))}...");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Error parsing message: {ex.Message}");
            Debug.LogError($"   Message preview: {message.Substring(0, Math.Min(100, message.Length))}...");
        }
    }

    // ==================== HANDLERS ====================

    private void HandlePlayerStatsUpdate(JObject json)
    {
        try
        {
            string playerId = json["playerId"]?.ToString();
            int health = json["health"]?.ToObject<int>() ?? 0;
            int maxHealth = json["maxHealth"]?.ToObject<int>() ?? 0;
            int mana = json["mana"]?.ToObject<int>() ?? 0;
            int maxMana = json["maxMana"]?.ToObject<int>() ?? 0;

            Debug.Log($"📊 Stats update: HP={health}/{maxHealth}, MP={mana}/{maxMana}");

            // Atualiza UI se for o player local
            if (playerId == ClientManager.Instance.PlayerId && UIManager.Instance != null)
            {
                UIManager.Instance.UpdateHealthBar(health, maxHealth);
                UIManager.Instance.UpdateManaBar(mana, maxMana);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Error in HandlePlayerStatsUpdate: {ex.Message}");
        }
    }

    private void HandleWorldState(JObject json)
    {
        var data = new WorldStateData
        {
            timestamp = json["timestamp"]?.ToObject<long>() ?? 0,
            players = json["players"]?.ToObject<PlayerStateData[]>(),
            monsters = json["monsters"]?.ToObject<MonsterStateData[]>()
        };
        
        OnWorldStateUpdate?.Invoke(data);
    }

    private void HandleCombatResult(JObject json)
    {
        var data = json["data"]?.ToObject<CombatResultData>();
        if (data != null)
        {
            OnCombatResult?.Invoke(data);
        }
    }

    private void HandleLevelUp(JObject json)
    {
        var data = new LevelUpData
        {
            playerId = json["playerId"]?.ToString(),
            characterName = json["characterName"]?.ToString(),
            newLevel = json["newLevel"]?.ToObject<int>() ?? 1,
            statusPoints = json["statusPoints"]?.ToObject<int>() ?? 0,
            experience = json["experience"]?.ToObject<int>() ?? 0,
            requiredExp = json["requiredExp"]?.ToObject<int>() ?? 100,
            newStats = json["newStats"]?.ToObject<StatsData>()
        };
        
        OnLevelUp?.Invoke(data);
    }

    private void HandlePlayerDeath(JObject json)
    {
        var data = json.ToObject<PlayerDeathData>();
        OnPlayerDeath?.Invoke(data);
    }

    private void HandlePlayerRespawn(JObject json)
    {
        var data = json.ToObject<PlayerRespawnData>();
        OnPlayerRespawn?.Invoke(data);
    }

    private void HandleAttackStarted(JObject json)
    {
        var data = new AttackStartedData
        {
            monsterId = json["monsterId"]?.ToObject<int>() ?? 0,
            monsterName = json["monsterName"]?.ToString()
        };
        OnAttackStarted?.Invoke(data);
    }

    private void HandlePlayerAttack(JObject json)
    {
        var data = new PlayerAttackData
        {
            playerId = json["playerId"]?.ToString(),
            characterName = json["characterName"]?.ToString(),
            monsterId = json["monsterId"]?.ToObject<int>() ?? 0,
            monsterName = json["monsterName"]?.ToString()
        };
        
        OnPlayerAttack?.Invoke(data);
    }

    private void HandleStatusPointAdded(JObject json)
    {
        var data = new StatusPointAddedData
        {
            playerId = json["playerId"]?.ToString(),
            characterName = json["characterName"]?.ToString(),
            stat = json["stat"]?.ToString(),
            statusPoints = json["statusPoints"]?.ToObject<int>() ?? 0,
            newStats = json["newStats"]?.ToObject<StatsData>()
        };
        
        Debug.Log($"✅ Status point added: {data.stat} - Remaining: {data.statusPoints}");
        OnStatusPointAdded?.Invoke(data);
    }

 // ==================== skills ====================

private void HandleSkillsResponse(JObject json)
{
    try
    {
        var skillsArray = json["skills"];
        
        if (skillsArray == null)
        {
            Debug.LogWarning("⚠️ No skills array in response");
            return;
        }

        var skills = new List<LearnedSkillData>();

        foreach (var skillJson in skillsArray)
        {
            var skill = new LearnedSkillData
            {
                skillId = skillJson["skillId"]?.ToObject<int>() ?? 0,
                currentLevel = skillJson["currentLevel"]?.ToObject<int>() ?? 1,
                slotNumber = skillJson["slotNumber"]?.ToObject<int>() ?? 0,
                lastUsedTime = skillJson["lastUsedTime"]?.ToObject<long>() ?? 0
            };

            // Parse template
            var templateJson = skillJson["template"];
            
            if (templateJson != null)
            {
                skill.template = new SkillTemplateData
                {
                    id = templateJson["id"]?.ToObject<int>() ?? 0,
                    name = templateJson["name"]?.ToString() ?? "",
                    description = templateJson["description"]?.ToString() ?? "",
                    skillType = templateJson["skillType"]?.ToString() ?? "",
                    damageType = templateJson["damageType"]?.ToString() ?? "",
                    targetType = templateJson["targetType"]?.ToString() ?? "",
                    maxLevel = templateJson["maxLevel"]?.ToObject<int>() ?? 10,
                    manaCost = templateJson["manaCost"]?.ToObject<int>() ?? 0,
                    healthCost = templateJson["healthCost"]?.ToObject<int>() ?? 0,
                    cooldown = templateJson["cooldown"]?.ToObject<float>() ?? 0f,
                    castTime = templateJson["castTime"]?.ToObject<float>() ?? 0f,
                    range = templateJson["range"]?.ToObject<float>() ?? 0f,
                    areaRadius = templateJson["areaRadius"]?.ToObject<float>() ?? 0f,
                    iconPath = templateJson["iconPath"]?.ToString() ?? "",
                    levels = ParseSkillLevels(templateJson["levels"])
                };
            }

            skills.Add(skill);
        }

        Debug.Log($"📚 Received {skills.Count} skills from server");

        // Envia para o SkillManager
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.UpdateSkills(skills);
        }
    }
    catch (Exception ex)
    {
        Debug.LogError($"❌ Error parsing skills response: {ex.Message}");
    }
}

private void HandleSkillUsed(JObject json)
{
    try
    {
        var resultJson = json["result"];
        
        if (resultJson == null)
        {
            Debug.LogWarning("⚠️ No result in skillUsed message");
            return;
        }

        bool success = resultJson["success"]?.ToObject<bool>() ?? false;
        string attackerId = resultJson["attackerId"]?.ToString();
        string attackerName = resultJson["attackerName"]?.ToString();
        int manaCost = resultJson["manaCost"]?.ToObject<int>() ?? 0;

        if (success)
        {
            Debug.Log($"⚔️ {attackerName} used skill!");

            // Atualiza mana do player local
            if (attackerId == ClientManager.Instance.PlayerId && UIManager.Instance != null)
            {
                var charData = WorldManager.Instance.GetLocalCharacterData();
                
                if (charData != null)
                {
                    charData.mana -= manaCost;
                    UIManager.Instance.UpdateManaBar(charData.mana, charData.maxMana);
                }
            }

            // Processa targets atingidos
            var targetsArray = resultJson["targets"];
            
            if (targetsArray != null)
            {
                foreach (var targetJson in targetsArray)
                {
                    HandleSkillTarget(targetJson, attackerId);
                }
            }

            // Log no combate
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=cyan>⚔️ {attackerName} usou uma skill!</color>");
            }
        }
    }
    catch (Exception ex)
    {
        Debug.LogError($"❌ Error handling skill used: {ex.Message}");
    }
}

private void HandleSkillTarget(JToken targetJson, string attackerId)
{
    string targetId = targetJson["targetId"]?.ToString();
    string targetName = targetJson["targetName"]?.ToString();
    string targetType = targetJson["targetType"]?.ToString();
    int damage = targetJson["damage"]?.ToObject<int>() ?? 0;
    int healing = targetJson["healing"]?.ToObject<int>() ?? 0;
    bool isCritical = targetJson["isCritical"]?.ToObject<bool>() ?? false;
    bool targetDied = targetJson["targetDied"]?.ToObject<bool>() ?? false;
    int remainingHealth = targetJson["remainingHealth"]?.ToObject<int>() ?? 0;
    int expGained = targetJson["experienceGained"]?.ToObject<int>() ?? 0;

    // Mostra dano
    if (damage > 0)
    {
        if (targetType == "monster")
        {
            var monster = GameObject.Find($"Monster_{targetName}_{targetId}");
            
            if (monster != null)
            {
                var monsterController = monster.GetComponent<MonsterController>();
                monsterController?.ShowDamage(damage, isCritical);
            }
        }
        else if (targetType == "player")
        {
            var player = GameObject.Find($"Player_{targetName}_{targetId}");
            
            if (player != null)
            {
                var playerController = player.GetComponent<PlayerController>();
                playerController?.ShowDamage(damage, isCritical);
            }
        }

        // Log
        string critText = isCritical ? " <color=red>CRÍTICO!</color>" : "";
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog($"<color=orange>{damage}</color> de dano em {targetName}{critText}");
        }

        // Se matou
        if (targetDied && expGained > 0)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=lime>💀 {targetName} derrotado! +{expGained} XP</color>");
            }
        }
    }

    // Mostra cura
    if (healing > 0)
    {
        if (DamageTextManager.Instance != null)
        {
            Vector3 position = Vector3.zero; // TODO: pegar posição real
            DamageTextManager.Instance.ShowHeal(position, healing);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog($"<color=lime>+{healing} HP curado</color>");
        }
    }
}

private void HandleSkillUseFailed(JObject json)
{
    int skillId = json["skillId"]?.ToObject<int>() ?? 0;
    string reason = json["reason"]?.ToString() ?? "";

    Debug.LogWarning($"⚠️ Skill {skillId} failed: {reason}");

    string message = reason switch
    {
        "COOLDOWN" => "⏳ Skill em cooldown!",
        "NO_MANA" => "❌ Mana insuficiente!",
        "NO_HEALTH" => "❌ HP insuficiente!",
        "OUT_OF_RANGE" => "❌ Alvo fora de alcance!",
        "SKILL_NOT_LEARNED" => "❌ Você não aprendeu esta skill!",
        "INVALID_LEVEL" => "❌ Nível de skill inválido!",
        _ => $"❌ Falha ao usar skill: {reason}"
    };

    if (UIManager.Instance != null)
    {
        UIManager.Instance.AddCombatLog($"<color=yellow>{message}</color>");
    }
}

private void HandleSkillLearned(JObject json)
{
    bool success = json["success"]?.ToObject<bool>() ?? false;
    
    if (success)
    {
        int skillId = json["skillId"]?.ToObject<int>() ?? 0;
        string skillName = json["skillName"]?.ToString() ?? "";
        int slotNumber = json["slotNumber"]?.ToObject<int>() ?? 0;

        Debug.Log($"✅ Learned skill: {skillName} in slot {slotNumber}");

        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog($"<color=lime>✅ Skill {skillName} aprendida! (Slot {slotNumber})</color>");
        }

        // Recarrega skills
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.RequestSkills();
        }
    }
    else
    {
        string message = json["message"]?.ToString() ?? "Falha ao aprender skill";
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog($"<color=red>❌ {message}</color>");
        }
    }
}

private void HandleSkillLeveledUp(JObject json)
{
    bool success = json["success"]?.ToObject<bool>() ?? false;
    
    if (success)
    {
        int skillId = json["skillId"]?.ToObject<int>() ?? 0;
        int newLevel = json["newLevel"]?.ToObject<int>() ?? 1;
        int statusPoints = json["statusPoints"]?.ToObject<int>() ?? 0;

        Debug.Log($"⬆️ Skill {skillId} leveled up to {newLevel}");

        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog($"<color=cyan>⬆️ Skill aumentada para nível {newLevel}!</color>");
            
            // Atualiza status points
            var charData = WorldManager.Instance.GetLocalCharacterData();
            
            if (charData != null)
            {
                charData.statusPoints = statusPoints;
                UIManager.Instance.UpdateLocalCharacterData(charData);
            }
        }

        // Recarrega skills
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.RequestSkills();
        }
    }
    else
    {
        string message = json["message"]?.ToString() ?? "Falha ao aumentar nível da skill";
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog($"<color=red>❌ {message}</color>");
        }
    }
}

private void HandleSkillListResponse(JObject json)
{
    try
    {
        var skillsArray = json["skills"];
        
        if (skillsArray == null)
        {
            Debug.LogWarning("⚠️ No skills array in skillListResponse");
            return;
        }

        var availableSkills = new List<SkillTemplateData>();

        foreach (var skillJson in skillsArray)
        {
            var skill = new SkillTemplateData
            {
                id = skillJson["id"]?.ToObject<int>() ?? 0,
                name = skillJson["name"]?.ToString() ?? "",
                description = skillJson["description"]?.ToString() ?? "",
                skillType = skillJson["skillType"]?.ToString() ?? "",
                damageType = skillJson["damageType"]?.ToString() ?? "",
                targetType = skillJson["targetType"]?.ToString() ?? "",
                requiredLevel = skillJson["requiredLevel"]?.ToObject<int>() ?? 1,
                requiredClass = skillJson["requiredClass"]?.ToString() ?? "",
                maxLevel = skillJson["maxLevel"]?.ToObject<int>() ?? 10,
                manaCost = skillJson["manaCost"]?.ToObject<int>() ?? 0,
                healthCost = skillJson["healthCost"]?.ToObject<int>() ?? 0,
                cooldown = skillJson["cooldown"]?.ToObject<float>() ?? 0f,
                castTime = skillJson["castTime"]?.ToObject<float>() ?? 0f,
                range = skillJson["range"]?.ToObject<float>() ?? 0f,
                areaRadius = skillJson["areaRadius"]?.ToObject<float>() ?? 0f,
                iconPath = skillJson["iconPath"]?.ToString() ?? "",
                levels = ParseSkillLevels(skillJson["levels"])
            };

            availableSkills.Add(skill);
        }

        Debug.Log($"📖 Received {availableSkills.Count} available skills from server");

        // Envia para o SkillBookUI
        if (SkillBookUI.Instance != null)
        {
            SkillBookUI.Instance.UpdateAvailableSkills(availableSkills);
        }
        else
        {
            Debug.LogWarning("⚠️ SkillBookUI.Instance is null!");
        }
    }
    catch (Exception ex)
    {
        Debug.LogError($"❌ Error parsing skill list response: {ex.Message}");
        Debug.LogError($"   StackTrace: {ex.StackTrace}");
    }
}

private SkillLevelData[] ParseSkillLevels(JToken levelsJson)
{
    if (levelsJson == null || !levelsJson.HasValues)
        return new SkillLevelData[0];

    var levels = new List<SkillLevelData>();

    foreach (var levelJson in levelsJson)
    {
        var levelData = new SkillLevelData
        {
            level = levelJson["level"]?.ToObject<int>() ?? 1,
            baseDamage = levelJson["baseDamage"]?.ToObject<int>() ?? 0,
            baseHealing = levelJson["baseHealing"]?.ToObject<int>() ?? 0,
            damageMultiplier = levelJson["damageMultiplier"]?.ToObject<float>() ?? 1f,
            critChanceBonus = levelJson["critChanceBonus"]?.ToObject<float>() ?? 0f,
            statusPointCost = levelJson["statusPointCost"]?.ToObject<int>() ?? 1
        };

        levels.Add(levelData);
    }

    return levels.ToArray();
}
    // ==================== INVENTORY ====================

    private void HandleInventoryResponse(JObject json)
    {
        bool success = json["success"]?.ToObject<bool>() ?? false;
        
        if (!success)
        {
            Debug.LogError("❌ Failed to receive inventory");
            return;
        }

        var inventoryJson = json["inventory"];
        var data = inventoryJson?.ToObject<InventoryData>();
        
        if (data != null)
        {
            Debug.Log($"📦 Inventory received: {data.items.Count} items, {data.gold} gold");
            OnInventoryReceived?.Invoke(data);
        }
    }

    private void HandleLootReceived(JObject json)
    {
        var data = new LootReceivedData
        {
            playerId = json["playerId"]?.ToString(),
            characterName = json["characterName"]?.ToString(),
            gold = json["gold"]?.ToObject<int>() ?? 0,
            items = json["items"]?.ToObject<System.Collections.Generic.List<LootedItemData>>() 
                    ?? new System.Collections.Generic.List<LootedItemData>()
        };

        Debug.Log($"💰 Loot: {data.gold} gold, {data.items.Count} items");
        OnLootReceived?.Invoke(data);
    }

    private void HandleItemUsed(JObject json)
    {
        var data = new ItemUsedData
        {
            playerId = json["playerId"]?.ToString(),
            instanceId = json["instanceId"]?.ToObject<int>() ?? 0,
            health = json["health"]?.ToObject<int>() ?? 0,
            maxHealth = json["maxHealth"]?.ToObject<int>() ?? 0,
            mana = json["mana"]?.ToObject<int>() ?? 0,
            maxMana = json["maxMana"]?.ToObject<int>() ?? 0,
            remainingQuantity = json["remainingQuantity"]?.ToObject<int>() ?? 0
        };

        Debug.Log($"💊 Item used: {data.instanceId}");
        OnItemUsed?.Invoke(data);
    }

    private void HandleItemUseFailed(JObject json)
    {
        string reason = json["reason"]?.ToString() ?? "";
        string message = json["message"]?.ToString() ?? "Não foi possível usar o item";

        Debug.LogWarning($"⚠️ Item use failed: {reason}");

        if (UIManager.Instance != null)
        {
            string coloredMessage = reason switch
            {
                "HP_FULL" => "<color=yellow>💊 HP já está cheio!</color>",
                "MP_FULL" => "<color=cyan>💊 MP já está cheio!</color>",
                "ON_COOLDOWN" => "<color=orange>⏳ Aguarde antes de usar outra poção!</color>",
                _ => $"<color=yellow>⚠️ {message}</color>"
            };

            UIManager.Instance.AddCombatLog(coloredMessage);
        }
    }

    private void HandleItemEquipped(JObject json)
    {
        var data = new ItemEquippedData
        {
            playerId = json["playerId"]?.ToString(),
            instanceId = json["instanceId"]?.ToObject<int>() ?? 0,
            newStats = json["newStats"]?.ToObject<StatsData>(),
            equipment = json["equipment"]?.ToObject<EquipmentData>()
        };

        Debug.Log($"⚔️ Item equipped: {data.instanceId}");
        OnItemEquipped?.Invoke(data);
    }

    private void HandleItemUnequipped(JObject json)
    {
        var data = new ItemEquippedData
        {
            playerId = json["playerId"]?.ToString(),
            instanceId = 0,
            newStats = json["newStats"]?.ToObject<StatsData>(),
            equipment = json["equipment"]?.ToObject<EquipmentData>()
        };

        Debug.Log($"⚔️ Item unequipped: {json["slot"]}");
        OnItemUnequipped?.Invoke(data);
    }

    private void HandleItemDropped(JObject json)
    {
        var data = new ItemDroppedData
        {
            playerId = json["playerId"]?.ToString(),
            instanceId = json["instanceId"]?.ToObject<int>() ?? 0,
            quantity = json["quantity"]?.ToObject<int>() ?? 1
        };

        Debug.Log($"📤 Item dropped: {data.instanceId}");
        OnItemDropped?.Invoke(data);
    }

    // ==================== LOGIN/CHARACTER ====================

    private void HandleLoginResponse(JObject json)
    {
        var data = json["data"]?.ToObject<LoginResponseData>();
        OnLoginResponse?.Invoke(data);
    }

    private void HandleRegisterResponse(JObject json)
    {
        var data = new RegisterResponseData
        {
            success = json["success"]?.ToObject<bool>() ?? false,
            message = json["message"]?.ToString()
        };
        OnRegisterResponse?.Invoke(data);
    }

    private void HandleCreateCharacterResponse(JObject json)
    {
        var data = new CreateCharacterResponseData
        {
            success = json["success"]?.ToObject<bool>() ?? false,
            message = json["message"]?.ToString(),
            character = json["character"]?.ToObject<CharacterData>()
        };
        OnCreateCharacterResponse?.Invoke(data);
    }

    private void HandleSelectCharacterResponse(JObject json)
    {
        var data = new SelectCharacterResponseData
        {
            success = json["success"]?.ToObject<bool>() ?? false,
            message = json["message"]?.ToString(),
            character = json["character"]?.ToObject<CharacterData>(),
            playerId = json["playerId"]?.ToString(),
            allPlayers = json["allPlayers"]?.ToObject<PlayerStateData[]>(),
            allMonsters = json["allMonsters"]?.ToObject<MonsterStateData[]>(),
            inventory = json["inventory"]?.ToObject<InventoryData>()
        };

        if (data.success && !string.IsNullOrEmpty(data.playerId))
        {
            ClientManager.Instance.SetPlayerId(data.playerId);
        }

        OnSelectCharacterResponse?.Invoke(data);
    }

    private void HandlePlayerJoined(JObject json)
    {
        var playerData = json["player"];
        var data = new PlayerJoinedData
        {
            playerId = playerData["playerId"]?.ToString(),
            characterName = playerData["characterName"]?.ToString(),
            position = playerData["position"]?.ToObject<PositionData>(),
            raca = playerData["raca"]?.ToString(),
            classe = playerData["classe"]?.ToString(),
            level = playerData["level"]?.ToObject<int>() ?? 1,
            health = playerData["health"]?.ToObject<int>() ?? 100,
            maxHealth = playerData["maxHealth"]?.ToObject<int>() ?? 100
        };
        OnPlayerJoined?.Invoke(data);
    }

    private void HandlePlayerDisconnected(JObject json)
    {
        var playerId = json["playerId"]?.ToString();
        OnPlayerDisconnected?.Invoke(playerId);
    }

    private void OnDestroy()
    {
        if (ClientManager.Instance != null)
        {
            ClientManager.Instance.OnMessageReceived -= HandleMessage;
        }
    }

}
