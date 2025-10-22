using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// Slot individual de skill na hotbar
/// Coloque em: MMOClient/Scripts/UI/Skills/SkillSlotUI.cs
/// </summary>
public class SkillSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public Image cooldownOverlay;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI hotkeyText;
    public GameObject lockedOverlay;
    
    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1f, 1f, 0.5f);
    public Color pressedColor = new Color(0.8f, 0.8f, 0.8f);
    public Color notEnoughManaColor = new Color(1f, 0.3f, 0.3f);
    
    [Header("Data")]
    public int slotNumber; // 1-9
    public LearnedSkillData learnedSkill;
    
    private Image backgroundImage;
    private bool isOnCooldown = false;
    private float cooldownEndTime = 0f;
    private float cooldownDuration = 0f;
    
    //public event Action<SkillSlotUI> OnSkillClicked;

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
        
        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 0f;
            cooldownOverlay.gameObject.SetActive(false);
        }
        
        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (hotkeyText != null)
            hotkeyText.text = slotNumber.ToString();
    }

    private void Update()
    {
        UpdateCooldown();
        CheckHotkey();
    }

    /// <summary>
    /// Define a skill no slot
    /// </summary>
    public void SetSkill(LearnedSkillData skill)
    {
        learnedSkill = skill;

        if (skill == null || skill.template == null)
        {
            Clear();
            return;
        }

        // Ativa ícone
        if (iconImage != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = LoadSkillIcon(skill.template.iconPath);
        }

        // Remove lock se tinha
        if (lockedOverlay != null)
            lockedOverlay.SetActive(false);
    }

    /// <summary>
    /// Limpa o slot
    /// </summary>
    public void Clear()
    {
        learnedSkill = null;

        if (iconImage != null)
            iconImage.enabled = false;

        if (cooldownOverlay != null)
            cooldownOverlay.gameObject.SetActive(false);

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);
        
        if (lockedOverlay != null)
            lockedOverlay.SetActive(true);
    }

    /// <summary>
    /// Carrega sprite do ícone
    /// </summary>
    private Sprite LoadSkillIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        // Tenta carregar de Resources
        Sprite sprite = Resources.Load<Sprite>(iconPath);
        
        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>("Icons/Skills/default_skill");
        }

        return sprite;
    }

    /// <summary>
    /// Verifica se a hotkey foi pressionada
    /// </summary>
    private void CheckHotkey()
    {
        // Verifica teclas 1-9
        if (Input.GetKeyDown(KeyCode.Alpha1) && slotNumber == 1) UseSkill();
        if (Input.GetKeyDown(KeyCode.Alpha2) && slotNumber == 2) UseSkill();
        if (Input.GetKeyDown(KeyCode.Alpha3) && slotNumber == 3) UseSkill();
        if (Input.GetKeyDown(KeyCode.Alpha4) && slotNumber == 4) UseSkill();
        if (Input.GetKeyDown(KeyCode.Alpha5) && slotNumber == 5) UseSkill();
        if (Input.GetKeyDown(KeyCode.Alpha6) && slotNumber == 6) UseSkill();
        if (Input.GetKeyDown(KeyCode.Alpha7) && slotNumber == 7) UseSkill();
        if (Input.GetKeyDown(KeyCode.Alpha8) && slotNumber == 8) UseSkill();
        if (Input.GetKeyDown(KeyCode.Alpha9) && slotNumber == 9) UseSkill();
    }

    /// <summary>
    /// Usa a skill
    /// </summary>
    public void UseSkill()
    {
        if (learnedSkill == null || learnedSkill.template == null)
            return;

        if (isOnCooldown)
        {
            Debug.Log($"⏳ {learnedSkill.template.name} está em cooldown!");
            return;
        }

        // Valida requisitos
        var player = WorldManager.Instance?.GetLocalCharacterData();
        
        if (player == null)
            return;

        // Verifica mana
        if (player.mana < learnedSkill.template.manaCost)
        {
            Debug.Log($"❌ Mana insuficiente! Precisa de {learnedSkill.template.manaCost}");
            FlashNotEnoughMana();
            return;
        }

        // Verifica HP (se skill consome HP)
        if (player.health <= learnedSkill.template.healthCost)
        {
            Debug.Log($"❌ HP insuficiente!");
            return;
        }

        // Envia para o servidor
        SkillManager.Instance?.UseSkill(learnedSkill.skillId, slotNumber);
        
        // Inicia cooldown local (confirmação visual imediata)
        StartCooldown(learnedSkill.template.cooldown);
    }

    /// <summary>
    /// Inicia cooldown visual
    /// </summary>
    public void StartCooldown(float duration)
    {
        isOnCooldown = true;
        cooldownDuration = duration;
        cooldownEndTime = Time.time + duration;

        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(true);
            cooldownOverlay.fillAmount = 1f;
        }

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(true);
    }

    /// <summary>
    /// Atualiza cooldown
    /// </summary>
    private void UpdateCooldown()
    {
        if (!isOnCooldown)
            return;

        float remaining = cooldownEndTime - Time.time;

        if (remaining <= 0)
        {
            // Cooldown terminou
            isOnCooldown = false;
            
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = 0f;
                cooldownOverlay.gameObject.SetActive(false);
            }

            if (cooldownText != null)
                cooldownText.gameObject.SetActive(false);
        }
        else
        {
            // Atualiza fill amount
            if (cooldownOverlay != null)
            {
                float percent = remaining / cooldownDuration;
                cooldownOverlay.fillAmount = percent;
            }

            // Atualiza texto
            if (cooldownText != null)
            {
                if (remaining < 1f)
                    cooldownText.text = remaining.ToString("0.0");
                else
                    cooldownText.text = Mathf.CeilToInt(remaining).ToString();
            }
        }
    }

    /// <summary>
    /// Flash vermelho quando não tem mana
    /// </summary>
    private void FlashNotEnoughMana()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = notEnoughManaColor;
            Invoke(nameof(ResetColor), 0.2f);
        }
    }

    private void ResetColor()
    {
        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }

    // ==================== EVENTOS DE MOUSE ====================

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            UseSkill();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Clique direito - mostrar info
            ShowSkillInfo();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (backgroundImage != null)
            backgroundImage.color = hoverColor;

        // Mostra tooltip
        if (learnedSkill != null && learnedSkill.template != null)
        {
            SkillTooltip.Instance?.Show(learnedSkill, transform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (backgroundImage != null && !isOnCooldown)
            backgroundImage.color = normalColor;

        SkillTooltip.Instance?.Hide();
    }

    private void ShowSkillInfo()
    {
        if (learnedSkill != null && learnedSkill.template != null)
        {
            Debug.Log($"📖 Skill: {learnedSkill.template.name}\n" +
                     $"Level: {learnedSkill.currentLevel}/{learnedSkill.template.maxLevel}\n" +
                     $"Mana: {learnedSkill.template.manaCost}\n" +
                     $"Cooldown: {learnedSkill.template.cooldown}s");
        }
    }
}

/// <summary>
/// Dados de skill aprendida (synced com servidor)
/// </summary>
[Serializable]
public class LearnedSkillData
{
    public int skillId;
    public int currentLevel;
    public int slotNumber;
    public long lastUsedTime;
    public SkillTemplateData template;
}

/// <summary>
/// Template de skill (estrutura do JSON)
/// </summary>
[Serializable]
public class SkillTemplateData
{
    public int id;
    public string name;
    public string description;
    public string skillType;
    public string damageType;
    public string targetType;
    public int requiredLevel;
    public string requiredClass;
    public int maxLevel;
    public int manaCost;
    public int healthCost;
    public float cooldown;
    public float castTime;
    public float duration;
    public float range;
    public float areaRadius;
    public string animationTrigger;
    public string effectPrefab;
    public string soundEffect;
    public string iconPath;
    public SkillLevelData[] levels;
}

[Serializable]
public class SkillLevelData
{
    public int level;
    public int baseDamage;
    public int baseHealing;
    public float damageMultiplier;
    public float critChanceBonus;
    public int statusPointCost;
}