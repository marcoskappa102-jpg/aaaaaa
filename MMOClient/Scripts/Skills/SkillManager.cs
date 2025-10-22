using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

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

    // ✅ CORREÇÃO: Controle de movimento para skill COM RANGE CORRETO
    private bool movingToUseSkill = false;
    private int pendingSkillId = 0;
    private int pendingSlotNumber = 0;
    private string pendingTargetId = null;
    private Vector3 targetPositionForSkill;
    private float skillRange = 0f;
    
    // ✅ NOVO: Distância mínima segura para parar ANTES do alvo
    private const float RANGE_BUFFER = 0.5f; // Para 0.5m antes do range máximo

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
        // ✅ Verifica se chegou no range para usar skill
        if (movingToUseSkill)
        {
            CheckSkillRangeAndUse();
        }
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
        
        // ✅ CORREÇÃO: Só cancela movimento se estava indo usar skill
        if (movingToUseSkill)
        {
            movingToUseSkill = false;
            pendingSkillId = 0;
            Debug.Log($"🎯 SkillManager: Cancelled skill movement");
        }
    }

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

        // ✅ VALIDAÇÃO: Verifica se precisa de target
        if (skill.template.targetType == "enemy")
        {
            if (currentTargetMonsterId <= 0)
            {
                Debug.Log("❌ Nenhum alvo selecionado!");
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog("<color=yellow>❌ Selecione um alvo primeiro!</color>");
                }
                return;
            }

            var monsterObj = GameObject.Find($"Monster_{currentTargetMonsterId}") ?? 
                            FindMonsterByIdInScene(currentTargetMonsterId);
            
            if (monsterObj == null)
            {
                Debug.LogWarning($"❌ Monster {currentTargetMonsterId} not found in scene!");
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog("<color=yellow>❌ Alvo não encontrado!</color>");
                }
                return;
            }

            var monsterController = monsterObj.GetComponent<MonsterController>();
            
            if (monsterController == null || !monsterController.isAlive)
            {
                Debug.LogWarning($"❌ Monster {currentTargetMonsterId} is dead or invalid!");
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog("<color=yellow>❌ Alvo inválido!</color>");
                }
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

            Debug.Log($"📏 Distance to target: {distance:F2}m, Skill range: {range:F2}m");

            // ✅ CORREÇÃO: Usa buffer para parar ANTES do range máximo
            float effectiveRange = range - RANGE_BUFFER;

            if (distance > range)
            {
                Debug.Log($"🏃 Too far! Moving to range first...");
                
                movingToUseSkill = true;
                pendingSkillId = skillId;
                pendingSlotNumber = slotNumber;
                pendingTargetId = currentTargetMonsterId.ToString();
                targetPositionForSkill = monsterObj.transform.position;
                skillRange = effectiveRange; // ✅ USA RANGE COM BUFFER
                
                // ✅ CORREÇÃO: Move para UMA POSIÇÃO NO RANGE, não para o alvo
                SendMoveToSkillRange(player.transform.position, monsterObj.transform.position, effectiveRange);
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog($"<color=cyan>🏃 Aproximando do alvo...</color>");
                }
                
                return;
            }
        }

        // ✅ ESTÁ NO RANGE - USA A SKILL (mas NÃO inicia cooldown visual aqui)
        ExecuteSkill(skillId, slotNumber, skill.template);
    }

    /// <summary>
    /// ✅ CORRIGIDO: Calcula posição DENTRO DO RANGE (não na posição exata do alvo)
    /// </summary>
    private void SendMoveToSkillRange(Vector3 playerPos, Vector3 monsterPos, float range)
    {
        // Calcula direção player -> monstro
        Vector3 direction = (monsterPos - playerPos).normalized;
        
        // Calcula ponto NO RANGE (não no monstro)
        Vector3 targetPos = monsterPos - (direction * range);
        
        // Ajusta ao terreno
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
        
        Debug.Log($"🏃 Moving to skill range position: ({targetPos.x:F1}, {targetPos.z:F1})");
    }

    /// <summary>
    /// ✅ CORRIGIDO: Verifica range continuamente e usa skill quando chega
    /// </summary>
    private void CheckSkillRangeAndUse()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        
        if (player == null)
        {
            movingToUseSkill = false;
            return;
        }

        // ✅ Busca o alvo NOVAMENTE (pode ter se movido)
        GameObject monsterObj = null;
        
        if (!string.IsNullOrEmpty(pendingTargetId))
        {
            monsterObj = GameObject.Find($"Monster_{pendingTargetId}") ?? 
                        FindMonsterByIdInScene(int.Parse(pendingTargetId));
        }
        
        if (monsterObj == null)
        {
            Debug.LogWarning($"❌ Target lost! Cancelling skill {pendingSkillId}");
            movingToUseSkill = false;
            pendingSkillId = 0;
            pendingTargetId = null;
            return;
        }

        // ✅ CORREÇÃO: Não atualiza posição do alvo constantemente (causa "grudamento")
        float distance = Vector3.Distance(player.transform.position, monsterObj.transform.position);

        // Chegou no range? (com buffer de segurança)
        if (distance <= skillRange + 0.2f)
        {
            Debug.Log($"✅ Reached skill range! Using skill {pendingSkillId}");
            
            if (learnedSkills.TryGetValue(pendingSkillId, out var skill))
            {
                ExecuteSkill(pendingSkillId, pendingSlotNumber, skill.template);
                
                // ✅ CORREÇÃO: SÓ AGORA inicia o cooldown visual
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
        // Se ficou muito longe (alvo se moveu muito), recalcula
        else if (distance > skillRange + 3f)
        {
            Debug.Log($"⚠️ Target moved too far, recalculating path...");
            SendMoveToSkillRange(player.transform.position, monsterObj.transform.position, skillRange);
        }
    }

    /// <summary>
    /// ✅ CORRIGIDO: Executa a skill (envia para servidor) SEM iniciar cooldown aqui
    /// </summary>
    private void ExecuteSkill(int skillId, int slotNumber, SkillTemplateData template)
    {
        // Determina target baseado no tipo de skill
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

        // Monta mensagem
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

    /// <summary>
    /// ✅ HELPER - Busca monstro por ID na cena
    /// </summary>
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

    /// <summary>
    /// ✅ Verifica se está se movendo para usar skill
    /// </summary>
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