using UnityEngine;
using TMPro;

/// <summary>
/// Tooltip que aparece ao passar o mouse sobre uma skill
/// Coloque em: MMOClient/Scripts/UI/Skills/SkillTooltip.cs
/// </summary>
public class SkillTooltip : MonoBehaviour
{
    public static SkillTooltip Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillDescriptionText;
    public TextMeshProUGUI skillStatsText;
    public RectTransform tooltipRect;

    [Header("Settings")]
    public Vector2 offset = new Vector2(10f, 10f);
    public float followSpeed = 15f;

    private Canvas canvas;
    private Camera uiCamera;
    private bool isVisible = false;

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

        canvas = GetComponentInParent<Canvas>();
        
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            uiCamera = canvas.worldCamera;
        }

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private void Update()
    {
        if (isVisible)
        {
            UpdatePosition();
        }
    }

    /// <summary>
    /// Mostra tooltip com informações da skill
    /// </summary>
    public void Show(LearnedSkillData skill, Transform slotTransform)
    {
        if (skill == null || skill.template == null)
            return;

        var template = skill.template;

        // Nome da skill
        if (skillNameText != null)
        {
            string levelInfo = $" (Lv. {skill.currentLevel}/{template.maxLevel})";
            skillNameText.text = template.name + levelInfo;
        }

        // Descrição
        if (skillDescriptionText != null)
        {
            skillDescriptionText.text = template.description;
        }

        // Estatísticas
        if (skillStatsText != null)
        {
            string stats = BuildStatsText(skill);
            skillStatsText.text = stats;
        }

        // Ativa tooltip
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(true);
        }

        isVisible = true;
        UpdatePosition();
    }

    /// <summary>
    /// Esconde tooltip
    /// </summary>
    public void Hide()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }

        isVisible = false;
    }

    /// <summary>
    /// Constrói texto com estatísticas da skill
    /// </summary>
    private string BuildStatsText(LearnedSkillData skill)
    {
        var template = skill.template;
        var levelData = GetLevelData(template, skill.currentLevel);

        string stats = "";

        // Tipo
        stats += $"<color=yellow>Tipo:</color> {TranslateSkillType(template.skillType)}\n";

        // Range
        if (template.range > 0)
        {
            stats += $"<color=cyan>Alcance:</color> {template.range}m\n";
        }

        // Área
        if (template.areaRadius > 0)
        {
            stats += $"<color=cyan>Raio:</color> {template.areaRadius}m\n";
        }

        // Custos
        if (template.manaCost > 0)
        {
            stats += $"<color=blue>Mana:</color> {template.manaCost}\n";
        }

        if (template.healthCost > 0)
        {
            stats += $"<color=red>HP:</color> {template.healthCost}\n";
        }

        // Cooldown
        if (template.cooldown > 0)
        {
            stats += $"<color=orange>Cooldown:</color> {template.cooldown}s\n";
        }

        // Cast time
        if (template.castTime > 0)
        {
            stats += $"<color=gray>Conjuração:</color> {template.castTime}s\n";
        }

        stats += "\n";

        // Dano/Cura do nível atual
        if (levelData != null)
        {
            if (levelData.baseDamage > 0)
            {
                string damageType = template.damageType == "magical" ? "Dano Mágico" : "Dano Físico";
                stats += $"<color=red>{damageType}:</color> {levelData.baseDamage}";
                
                if (levelData.damageMultiplier > 0)
                {
                    int percent = Mathf.RoundToInt(levelData.damageMultiplier * 100);
                    stats += $" (+{percent}% ATK)";
                }
                stats += "\n";
            }

            if (levelData.baseHealing > 0)
            {
                stats += $"<color=lime>Cura:</color> {levelData.baseHealing}";
                
                if (levelData.damageMultiplier > 0)
                {
                    int percent = Mathf.RoundToInt(levelData.damageMultiplier * 100);
                    stats += $" (+{percent}% MATK)";
                }
                stats += "\n";
            }

            if (levelData.critChanceBonus > 0)
            {
                int critPercent = Mathf.RoundToInt(levelData.critChanceBonus * 100);
                stats += $"<color=yellow>Crítico:</color> +{critPercent}%\n";
            }
        }

        // Próximo nível
        if (skill.currentLevel < template.maxLevel)
        {
            var nextLevelData = GetLevelData(template, skill.currentLevel + 1);
            
            if (nextLevelData != null)
            {
                stats += $"\n<color=gray>Próximo nível ({skill.currentLevel + 1}):</color>\n";
                
                if (nextLevelData.baseDamage > levelData.baseDamage)
                {
                    int increase = nextLevelData.baseDamage - levelData.baseDamage;
                    stats += $"  Dano: <color=green>+{increase}</color>\n";
                }

                if (nextLevelData.baseHealing > levelData.baseHealing)
                {
                    int increase = nextLevelData.baseHealing - levelData.baseHealing;
                    stats += $"  Cura: <color=green>+{increase}</color>\n";
                }

                stats += $"  <color=yellow>Custo: {nextLevelData.statusPointCost} SP</color>";
            }
        }
        else
        {
            stats += "\n<color=green>✓ Nível máximo</color>";
        }

        return stats;
    }

    private SkillLevelData GetLevelData(SkillTemplateData template, int level)
    {
        if (template.levels == null || template.levels.Length == 0)
            return null;

        foreach (var data in template.levels)
        {
            if (data.level == level)
                return data;
        }

        return null;
    }

    private string TranslateSkillType(string type)
    {
        return type switch
        {
            "active" => "Ativa",
            "passive" => "Passiva",
            "buff" => "Buff",
            _ => type
        };
    }

    /// <summary>
    /// Atualiza posição do tooltip para seguir o mouse
    /// </summary>
    private void UpdatePosition()
    {
        if (tooltipRect == null)
            return;

        Vector2 mousePosition = Input.mousePosition;
        Vector2 targetPosition = mousePosition + offset;

        // Converte para coordenadas do canvas
        if (canvas != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                targetPosition,
                uiCamera,
                out Vector2 localPoint
            );

            // Suaviza movimento
            Vector2 currentPos = tooltipRect.anchoredPosition;
            tooltipRect.anchoredPosition = Vector2.Lerp(currentPos, localPoint, followSpeed * Time.deltaTime);

            // Mantém dentro da tela
            ClampToScreen();
        }
    }

    /// <summary>
    /// Garante que tooltip não sai da tela
    /// </summary>
    private void ClampToScreen()
    {
        if (canvas == null || tooltipRect == null)
            return;

        Vector3[] corners = new Vector3[4];
        tooltipRect.GetWorldCorners(corners);

        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector3[] canvasCorners = new Vector3[4];
        canvasRect.GetWorldCorners(canvasCorners);

        // Verifica limites
        float overflowRight = corners[2].x - canvasCorners[2].x;
        float overflowTop = corners[1].y - canvasCorners[1].y;
        float overflowLeft = canvasCorners[0].x - corners[0].x;
        float overflowBottom = canvasCorners[0].y - corners[0].y;

        Vector2 adjustment = Vector2.zero;

        if (overflowRight > 0)
            adjustment.x -= overflowRight;
        if (overflowLeft > 0)
            adjustment.x += overflowLeft;
        if (overflowTop > 0)
            adjustment.y -= overflowTop;
        if (overflowBottom > 0)
            adjustment.y += overflowBottom;

        tooltipRect.anchoredPosition += adjustment;
    }
}