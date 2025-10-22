using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// Script para cada entrada de skill no SkillBook
/// Coloque em: MMOClient/Scripts/UI/Skills/SkillEntryUI.cs
/// 
/// COMO USAR:
/// 1. Crie um prefab com este layout:
///    - Image (background)
///    - Image (icon)
///    - TextMeshProUGUI (nameText)
///    - TextMeshProUGUI (levelText)
///    - TextMeshProUGUI (slotText) - opcional
///    - Button (componente)
/// 2. Adicione este script no prefab
/// 3. Arraste os componentes nos campos públicos
/// 4. Atribua o prefab no SkillBookUI
/// </summary>
public class SkillEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI slotText; // Opcional - só para learned skills
    public Button button;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1f, 1f, 0.7f);
    public Color selectedColor = new Color(0.7f, 1f, 0.7f);
    public Color cannotLearnColor = new Color(0.5f, 0.5f, 0.5f);

    // Dados da skill
    private LearnedSkillData learnedSkill;
    private SkillTemplateData availableSkill;
    private bool isLearned;
    private bool canLearn;

    // Eventos
    public event Action<SkillEntryUI> OnClicked;

    private Image backgroundImage;
    private bool isSelected = false;

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
        
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    /// <summary>
    /// Configura entry para skill aprendida
    /// </summary>
    public void SetLearnedSkill(LearnedSkillData skill)
    {
        learnedSkill = skill;
        isLearned = true;
        canLearn = false;

        if (skill.template == null)
        {
            Debug.LogWarning("⚠️ Skill template is null!");
            return;
        }

        // Nome
        if (nameText != null)
            nameText.text = skill.template.name;

        // Nível
        if (levelText != null)
            levelText.text = $"Lv. {skill.currentLevel}/{skill.template.maxLevel}";

        // Slot (se tiver)
        if (slotText != null)
        {
            if (skill.slotNumber > 0)
                slotText.text = $"Slot {skill.slotNumber}";
            else
                slotText.text = "Sem slot";
        }

        // Ícone
        if (iconImage != null)
        {
            iconImage.sprite = LoadSkillIcon(skill.template.iconPath);
            iconImage.color = Color.white;
        }

        // Cor normal
        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }

    /// <summary>
    /// Configura entry para skill disponível (ainda não aprendida)
    /// </summary>
    public void SetAvailableSkill(SkillTemplateData skill, bool canLearn)
    {
        availableSkill = skill;
        isLearned = false;
        this.canLearn = canLearn;

        // Nome
        if (nameText != null)
            nameText.text = skill.name;

        // Requisito
        if (levelText != null)
        {
            string color = canLearn ? "lime" : "red";
            levelText.text = $"<color={color}>Requer Lv. {skill.requiredLevel}</color>";
        }

        // Sem slot (ainda não aprendida)
        if (slotText != null)
            slotText.text = "";

        // Ícone
        if (iconImage != null)
        {
            iconImage.sprite = LoadSkillIcon(skill.iconPath);
            
            // Deixa cinza se não pode aprender
            if (!canLearn)
                iconImage.color = new Color(0.5f, 0.5f, 0.5f);
            else
                iconImage.color = Color.white;
        }

        // Cor de fundo
        if (backgroundImage != null)
        {
            if (!canLearn)
                backgroundImage.color = cannotLearnColor;
            else
                backgroundImage.color = normalColor;
        }

        // Desabilita botão se não pode aprender
        if (button != null)
            button.interactable = canLearn;
    }

    private void OnButtonClick()
    {
        OnClicked?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (backgroundImage != null && canLearn)
            backgroundImage.color = hoverColor;

        // Mostra tooltip
        if (SkillTooltip.Instance != null)
        {
            if (isLearned && learnedSkill != null)
            {
                SkillTooltip.Instance.Show(learnedSkill, transform);
            }
            // TODO: Mostrar tooltip para available skills também
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (backgroundImage != null && !isSelected)
        {
            if (!canLearn)
                backgroundImage.color = cannotLearnColor;
            else
                backgroundImage.color = normalColor;
        }

        if (SkillTooltip.Instance != null)
        {
            SkillTooltip.Instance.Hide();
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        
        if (backgroundImage != null)
        {
            if (selected)
                backgroundImage.color = selectedColor;
            else if (!canLearn)
                backgroundImage.color = cannotLearnColor;
            else
                backgroundImage.color = normalColor;
        }
    }

    private Sprite LoadSkillIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(iconPath);
        
        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>("Icons/Skills/default_skill");
        }

        return sprite;
    }

    // Getters
    public LearnedSkillData GetLearnedSkill() => learnedSkill;
    public SkillTemplateData GetAvailableSkill() => availableSkill;
    public bool IsLearned() => isLearned;
    public bool CanLearn() => canLearn;
}