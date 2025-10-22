using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

/// <summary>
/// ✅ CORRIGIDO - SkillManager com validações e cancelamento de skill
/// </summary>
public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [Header("UI")]
    public Transform skillHotbarContainer;
    public GameObject skillSlotPrefab;

    [Header("Visual Effects")]
    public GameObject defaultSkillEffectPrefab;
    
    private List<SkillSlotUI> skillSlots = new List<SkillSlotUI>();
    private Dictionary<int, LearnedSkillData> learnedSkills = new Dictionary<int, LearnedSkillData>();
    
    private int currentTargetMonsterId = -1;
    
    // ✅ NOVO - Sistema de casttime
    private bool isCasting = false;
    private float castStartTime = 0f;
    private int castingSkillId = 0;
    private UseSkillRequest pendingSkillRequest;
    
    // Sistema de movimento para skill
    private bool movingToUseSkill = false;
    private int pendingSkillId = 0;
    private int pendingSlotNumber = 0;
    private string pendingTargetId = null;
    private Vector3 targetPositionForSkill;
    private float skillRange = 0f;
    
    private const float RANGE_BUFFER = 0.5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CreateSkillSlots();
        RegisterMessageHandlers();
    }

    private void Update()
    {
        // ✅ NOVO - Processa casttime
        if (isCasting)
        {
            UpdateCasting();
        }
        
        if (movingToUseSkill)
        {
            CheckSkillRangeAndUse();
        }
    }

    /// <summary>
    /// ✅ NOVO - Sistema de casttime
    /// </summary>
    private void UpdateCasting()
    {
        if (!learnedSkills.TryGetValue(castingSkillId, out var skill))
        {
            CancelCasting();
            return;
        }

        float elapsed = Time.time - castStartTime;
        
        if (elapsed >= skill.template.castTime)
        {
            // Cast completo - executa skill
            ExecuteSkillAfterCast();
            isCasting = false;
        }
    }

    /// <summary>
    /// ✅ NOVO - Cancela cast se mover ou target morrer
    /// </summary>
    public void CancelCasting()
    {
        if (isCasting)
        {
            Debug.Log($"❌ Cast cancelled: {castingSkillId}");
            
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog("<color=yellow>❌ Conjuração cancelada!</color>");
            }
            
            isCasting = false;
            castingSkillId = 0;
            pendingSkillRequest = null;
        }
    }

    private void ExecuteSkillAfterCast()
    {
        if (pendingSkillRequest == null)
            return;

        // Envia para servidor
        var message = new
        {
            type = "useSkill",
            skillId = pendingSkillRequest.skillId,
            slotNumber = pendingSkillRequest.slotNumber,
            targetId = pendingSkillRequest.targetId,
            targetType = pendingSkillRequest.targetType,
            targetPosition = pendingSkillRequest.targetPosition
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
        
        pendingSkillRequest = null;
    }

    private void CreateSkillSlots()
    {
        if (skillSlotPrefab == null || skillHotbarContainer == null)
        {
            Debug.LogError("SkillManager: Missing prefab or container!");
            return;
        }

        for (int i = 1; i <= 9; i++)
        {
            GameObject slotObj = Instantiate(skillSlotPrefab, skillHotbarContainer);
            SkillSlotUI slot = slotObj.GetComponent<SkillSlotUI>();
            
            if (slot != null)
            {
                slot.slotNumber = i;
                skillSlots.Add(slot);
            }
        }

        Debug.Log($"✅ Created {skillSlots.Count} skill slots");
    }

    private void RegisterMessageHandlers()
    {
        if (MessageHandler.Instance != null)
        {
            MessageHandler.Instance.OnSelectCharacterResponse += HandleCharacterSelected;
        }
    }

    private void HandleCharacterSelected(SelectCharacterResponseData data)
    {
        if (data.success && data.character != null)
        {
            RequestSkills();
        }
    }

    public void RequestSkills()
    {
        var message = new
        {
            type = "getSkills"
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    public void UpdateSkills(List<LearnedSkillData> skills)
    {
        learnedSkills.Clear();

        foreach (var skill in skills)
        {
            learnedSkills[skill.skillId] = skill;
        }

        RefreshHotbar();
        
        Debug.Log($"📚 Loaded {learnedSkills.Count} skills");
    }

    private void RefreshHotbar()
    {
        foreach (var slot in skillSlots)
        {
            slot.Clear();
        }

        foreach (var kvp in learnedSkills)
        {
            var skill = kvp.Value;
            
            if (skill.slotNumber >= 1 && skill.slotNumber <= 9)
            {
                var slot = skillSlots.FirstOrDefault(s => s.slotNumber == skill.slotNumber);
                
                if (slot != null)
                {
                    slot.SetSkill(skill);
                }
            }
        }
    }

    public void SetCurrentTarget(int monsterId)
    {
        currentTargetMonsterId = monsterId;
        Debug.Log($"🎯 SkillManager: Target set: Monster ID {monsterId}");
    }

    public void ClearCurrentTarget()
    {
        currentTargetMonsterId = -1;
        
        if (movingToUseSkill)
        {
            movingToUseSkill = false;
            pendingSkillId = 0;
            Debug.Log($"🎯 SkillManager: Cancelled skill movement");
        }
        
        // ✅ NOVO - Cancela cast também
        CancelCasting();
    }

    /// <summary>
    /// ✅ CORRIGIDO - UseSkill com validações e casttime
    /// </summary>
    public void UseSkill(int skillId, int slotNumber)
    {
        if (!learnedSkills.TryGetValue(skillId, out var skill))
        {
            Debug.LogWarning($"❌ Skill {skillId} not learned!");
            return;
        }

        if (skill.template == null)
        {
            Debug.LogWarning($"❌ Skill {skillId} has no template!");
            return;
        }

        // ✅ NOVO - Não pode usar skill enquanto casta
        if (isCasting)
        {
            Debug.Log("❌ Cannot use skill while casting!");
            return;
        }

        if (skill.template.targetType == "enemy")
        {
            if (currentTargetMonsterId <= 0)
            {
                Debug.Log("❌ No target selected!");
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog("<color=yellow>❌ Selecione um alvo!</color>");
                }
                return;
            }

            var monsterObj = FindMonsterByIdInScene(currentTargetMonsterId);
            
            if (monsterObj == null)
            {
                Debug.LogWarning($"❌ Monster {currentTargetMonsterId} not found!");
                return;
            }

            var monsterController = monsterObj.GetComponent<MonsterController>();
            
            if (monsterController == null || !monsterController.isAlive)
            {
                Debug.LogWarning($"❌ Monster {currentTargetMonsterId} is dead!");
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            
            if (player == null)
            {
                Debug.LogError("❌ Local player not found!");
                return;
            }

            float distance = Vector3.Distance(player.transform.position, monsterObj.transform.position);
            float range = skill.template.range;

            if (distance > range)
            {
                Debug.Log($"🏃 Too far! Moving to range...");
                
                movingToUseSkill = true;
                pendingSkillId = skillId;
                pendingSlotNumber = slotNumber;
                pendingTargetId = currentTargetMonsterId.ToString();
                targetPositionForSkill = monsterObj.transform.position;
                skillRange = range - RANGE_BUFFER;
                
                SendMoveToSkillRange(player.transform.position, monsterObj.transform.position, skillRange);
                return;
            }
        }

        // ✅ NOVO - Se tem casttime, inicia cast
        if (skill.template.castTime > 0)
        {
            StartCasting(skillId, slotNumber, skill.template);
        }
        else
        {
            // Instant cast
            ExecuteSkill(skillId, slotNumber, skill.template);
        }
    }

    /// <summary>
    /// ✅ NOVO - Inicia casting
    /// </summary>
    private void StartCasting(int skillId, int slotNumber, SkillTemplateData template)
    {
        isCasting = true;
        castingSkillId = skillId;
        castStartTime = Time.time;
        
        // Prepara request
        pendingSkillRequest = new UseSkillRequest
        {
            skillId = skillId,
            slotNumber = slotNumber,
            targetId = currentTargetMonsterId > 0 ? currentTargetMonsterId.ToString() : null,
            targetType = template.targetType == "enemy" ? "monster" : "player",
            targetPosition = null
        };
        
        Debug.Log($"⏳ Casting {template.name} ({template.castTime}s)...");
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCombatLog($"<color=cyan>⏳ Conjurando {template.name}...</color>");
        }
    }

    private void SendMoveToSkillRange(Vector3 playerPos, Vector3 monsterPos, float range)
    {
        Vector3 direction = (monsterPos - playerPos).normalized;
        Vector3 targetPos = monsterPos - (direction * range);
        
        if (TerrainHelper.Instance != null)
        {
            targetPos = TerrainHelper.Instance.ClampToGround(targetPos, 0f);
        }

        var message = new
        {
            type = "moveRequest",
            targetPosition = new
            {
                x = targetPos.x,
                y = targetPos.y,
                z = targetPos.z
            }
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    private void CheckSkillRangeAndUse()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        
        if (player == null)
        {
            movingToUseSkill = false;
            return;
        }

        GameObject monsterObj = null;
        
        if (!string.IsNullOrEmpty(pendingTargetId))
        {
            monsterObj = FindMonsterByIdInScene(int.Parse(pendingTargetId));
        }
        
        // ✅ CORRIGIDO - Verifica se target ainda existe e está vivo
        if (monsterObj == null)
        {
            Debug.LogWarning($"❌ Target lost! Cancelling skill {pendingSkillId}");
            movingToUseSkill = false;
            pendingSkillId = 0;
            pendingTargetId = null;
            return;
        }

        var monsterController = monsterObj.GetComponent<MonsterController>();
        if (monsterController == null || !monsterController.isAlive)
        {
            Debug.LogWarning($"❌ Target died! Cancelling skill");
            movingToUseSkill = false;
            pendingSkillId = 0;
            pendingTargetId = null;
            return;
        }
        
        float distance = Vector3.Distance(player.transform.position, monsterObj.transform.position);

        if (distance <= skillRange + 0.2f)
        {
            Debug.Log($"✅ Reached skill range! Using skill {pendingSkillId}");
            
            if (learnedSkills.TryGetValue(pendingSkillId, out var skill))
            {
                ExecuteSkill(pendingSkillId, pendingSlotNumber, skill.template);
                
                var slot = skillSlots.FirstOrDefault(s => s.slotNumber == pendingSlotNumber);
                if (slot != null)
                {
                    slot.StartCooldown(skill.template.cooldown);
                }
            }
            
            movingToUseSkill = false;
            pendingSkillId = 0;
            pendingTargetId = null;
        }
        else if (distance > skillRange + 3f)
        {
            Debug.Log($"⚠️ Target moved too far, recalculating path...");
            SendMoveToSkillRange(player.transform.position, monsterObj.transform.position, skillRange);
        }
    }

    private void ExecuteSkill(int skillId, int slotNumber, SkillTemplateData template)
    {
        string targetId = null;
        Vector3? targetPosition = null;

        switch (template.targetType)
        {
            case "enemy":
                if (currentTargetMonsterId > 0)
                {
                    targetId = currentTargetMonsterId.ToString();
                }
                break;

            case "self":
                targetId = ClientManager.Instance.PlayerId;
                break;

            case "area":
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    targetPosition = player.transform.position;
                }
                break;

            case "ally":
                targetId = ClientManager.Instance.PlayerId;
                break;
        }

        var message = new
        {
            type = "useSkill",
            skillId = skillId,
            slotNumber = slotNumber,
            targetId = targetId,
            targetType = targetId != null ? "monster" : "player",
            targetPosition = targetPosition != null ? new
            {
                x = targetPosition.Value.x,
                y = targetPosition.Value.y,
                z = targetPosition.Value.z
            } : null
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);

        Debug.Log($"⚔️ Using skill: {template.name} (Level {learnedSkills[skillId].currentLevel})");
    }

    private GameObject FindMonsterByIdInScene(int monsterId)
    {
        var monsters = GameObject.FindGameObjectsWithTag("Monster");
        
        foreach (var monsterObj in monsters)
        {
            var controller = monsterObj.GetComponent<MonsterController>();
            
            if (controller != null && controller.monsterId == monsterId)
            {
                return monsterObj;
            }
        }
        
        return null;
    }

    public bool IsMovingToUseSkill()
    {
        return movingToUseSkill;
    }

    public void LearnSkill(int skillId, int slotNumber)
    {
        var message = new
        {
            type = "learnSkill",
            skillId = skillId,
            slotNumber = slotNumber
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    public void LevelUpSkill(int skillId)
    {
        var message = new
        {
            type = "levelUpSkill",
            skillId = skillId
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    public void PlaySkillEffect(int skillId, Vector3 position, string targetType)
    {
        if (!learnedSkills.TryGetValue(skillId, out var skill))
            return;

        if (skill.template == null)
            return;

        GameObject effectPrefab = null;
        
        if (!string.IsNullOrEmpty(skill.template.effectPrefab))
        {
            effectPrefab = Resources.Load<GameObject>(skill.template.effectPrefab);
        }

        if (effectPrefab == null)
        {
            effectPrefab = defaultSkillEffectPrefab;
        }

        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        if (!string.IsNullOrEmpty(skill.template.soundEffect))
        {
            Debug.Log($"🔊 Playing sound: {skill.template.soundEffect}");
        }
    }

    private void OnDestroy()
    {
        if (MessageHandler.Instance != null)
        {
            MessageHandler.Instance.OnSelectCharacterResponse -= HandleCharacterSelected;
        }
    }
}

/// <summary>
/// ✅ NOVO - Request de skill com validação
/// </summary>
[System.Serializable]
public class UseSkillRequest
{
    public int skillId;
    public int slotNumber;
    public string targetId;
    public string targetType;
    public Vector3? targetPosition;
}
