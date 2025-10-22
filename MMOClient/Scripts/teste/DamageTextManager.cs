using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ✅ OTIMIZADO - DamageTextManager com Object Pooling
/// Melhora significativa de performance em combates intensos
/// </summary>
public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    [Header("Prefab")]
    public GameObject damageTextPrefab;

    [Header("Pooling Settings")]
    [SerializeField] private int poolSize = 20;
    [SerializeField] private int maxPoolSize = 50;

    [Header("Animation Settings")]
    public float floatSpeed = 1f;
    public float floatDistance = 1.5f;
    public float fadeSpeed = 1.2f;
    public float lifetime = 2f;
    
    [Header("Bounce Effect")]
    public bool enableBounce = true;
    public float bounceScale = 1.3f;
    public AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Colors")]
    public Color normalDamageColor = Color.white;
    public Color criticalDamageColor = new Color(1f, 0.2f, 0.2f);
    public Color healColor = new Color(0.2f, 1f, 0.2f);
    public Color missColor = new Color(0.7f, 0.7f, 0.7f);
    public Color magicDamageColor = new Color(0.5f, 0.5f, 1f);

    [Header("Random Offset")]
    public float horizontalSpread = 0.5f;
    public float verticalSpread = 0.3f;

    // ✅ NOVO - Object Pool
    private Queue<GameObject> damageTextPool = new Queue<GameObject>();
    private List<GameObject> activeTexts = new List<GameObject>();
    private Transform poolContainer;

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
        InitializePool();
    }

    /// <summary>
    /// ✅ NOVO - Inicializa Object Pool
    /// </summary>
    private void InitializePool()
    {
        if (damageTextPrefab == null)
        {
            Debug.LogError("❌ DamageTextPrefab not assigned!");
            return;
        }

        // Cria container para organizar hierarchy
        poolContainer = new GameObject("DamageTextPool").transform;
        poolContainer.SetParent(transform);

        // Pre-instancia objetos
        for (int i = 0; i < poolSize; i++)
        {
            CreateNewPooledObject();
        }

        Debug.Log($"✅ DamageTextManager: Pool initialized with {poolSize} objects");
    }

    /// <summary>
    /// ✅ NOVO - Cria novo objeto no pool
    /// </summary>
    private GameObject CreateNewPooledObject()
    {
        GameObject obj = Instantiate(damageTextPrefab, poolContainer);
        obj.SetActive(false);
        damageTextPool.Enqueue(obj);
        return obj;
    }

    /// <summary>
    /// ✅ NOVO - Pega objeto do pool
    /// </summary>
    private GameObject GetPooledObject()
    {
        // Procura objeto inativo no pool
        if (damageTextPool.Count > 0)
        {
            GameObject obj = damageTextPool.Dequeue();
            obj.SetActive(true);
            activeTexts.Add(obj);
            return obj;
        }

        // Pool vazio - cria novo se não exceder limite
        if (activeTexts.Count < maxPoolSize)
        {
            GameObject obj = CreateNewPooledObject();
            damageTextPool.Dequeue(); // Remove da fila
            obj.SetActive(true);
            activeTexts.Add(obj);
            Debug.LogWarning($"⚠️ Pool expanded: {activeTexts.Count}/{maxPoolSize}");
            return obj;
        }

        // Limite atingido - reutiliza o mais antigo
        Debug.LogWarning($"⚠️ Pool limit reached! Reusing oldest text.");
        GameObject oldest = activeTexts[0];
        activeTexts.RemoveAt(0);
        ReturnToPool(oldest);
        return GetPooledObject();
    }

    /// <summary>
    /// ✅ NOVO - Retorna objeto ao pool
    /// </summary>
    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        obj.transform.SetParent(poolContainer);
        
        activeTexts.Remove(obj);
        damageTextPool.Enqueue(obj);
    }

    // ==================== MÉTODOS PÚBLICOS ====================

    public void ShowDamage(Vector3 worldPosition, int damage, bool isCritical)
    {
        ShowText(worldPosition, damage.ToString(), isCritical ? criticalDamageColor : normalDamageColor, isCritical);
    }

    public void ShowDamage(Vector3 worldPosition, int damage, bool isCritical, DamageType damageType)
    {
        Color color = damageType switch
        {
            DamageType.Physical => isCritical ? criticalDamageColor : normalDamageColor,
            DamageType.Magical => magicDamageColor,
            _ => normalDamageColor
        };

        string text = damage.ToString();
        if (isCritical) text += "!";

        ShowText(worldPosition, text, color, isCritical);
    }

    public void ShowHeal(Vector3 worldPosition, int amount)
    {
        ShowText(worldPosition, $"+{amount}", healColor, false);
    }

    public void ShowMiss(Vector3 worldPosition)
    {
        ShowText(worldPosition, "MISS", missColor, false, 32);
    }

    public void ShowStatus(Vector3 worldPosition, string status, Color color)
    {
        ShowText(worldPosition, status, color, false, 32);
    }

    public void ShowExperience(Vector3 worldPosition, int xp)
    {
        ShowText(worldPosition, $"+{xp} XP", new Color(1f, 1f, 0.3f), false, 28);
    }

    /// <summary>
    /// ✅ OTIMIZADO - Mostra texto usando object pooling
    /// </summary>
    private void ShowText(Vector3 worldPosition, string text, Color color, bool isBig, int fontSize = 36)
    {
        // Offset aleatório
        Vector3 randomOffset = new Vector3(
            Random.Range(-horizontalSpread, horizontalSpread),
            Random.Range(0f, verticalSpread),
            Random.Range(-horizontalSpread, horizontalSpread)
        );

        GameObject textObj = GetPooledObject();
        
        if (textObj == null)
        {
            Debug.LogWarning("❌ Could not get pooled object!");
            return;
        }

        textObj.transform.position = worldPosition + randomOffset;
        textObj.transform.rotation = Quaternion.identity;

        TextMeshProUGUI textComponent = textObj.GetComponentInChildren<TextMeshProUGUI>();

        if (textComponent != null)
        {
            textComponent.text = text;
            textComponent.color = color;
            textComponent.fontSize = isBig ? fontSize * 1.5f : fontSize;
        }

        StartCoroutine(AnimateDamageText(textObj, textComponent, isBig));
    }

    /// <summary>
    /// ✅ OTIMIZADO - Animação com retorno ao pool
    /// </summary>
    private IEnumerator AnimateDamageText(GameObject textObj, TextMeshProUGUI textComponent, bool isBig)
    {
        float elapsed = 0f;
        Vector3 startPos = textObj.transform.position;
        
        float actualFloatDistance = isBig ? floatDistance * 1.3f : floatDistance;
        Vector3 endPos = startPos + Vector3.up * actualFloatDistance;

        AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        Color startColor = textComponent != null ? textComponent.color : Color.white;
        Color endColor = startColor;
        endColor.a = 0f;

        Vector3 startScale = textObj.transform.localScale;
        Vector3 maxScale = startScale * bounceScale;

        while (elapsed < lifetime)
        {
            // ✅ OTIMIZAÇÃO - Para se objeto foi desativado
            if (textObj == null || !textObj.activeInHierarchy)
                yield break;

            elapsed += Time.deltaTime;
            float progress = elapsed / lifetime;

            // Move
            float curvedProgress = moveCurve.Evaluate(progress);
            textObj.transform.position = Vector3.Lerp(startPos, endPos, curvedProgress);

            // Fade
            if (progress > 0.5f && textComponent != null)
            {
                float fadeProgress = (progress - 0.5f) * 2f;
                textComponent.color = Color.Lerp(startColor, endColor, fadeProgress * fadeSpeed);
            }

            // Bounce
            if (enableBounce && progress < 0.3f)
            {
                float bounceProgress = progress / 0.3f;
                float bounce = bounceCurve.Evaluate(bounceProgress);
                textObj.transform.localScale = Vector3.Lerp(maxScale, startScale, bounce);
            }

            // Billboard
            if (Camera.main != null)
            {
                textObj.transform.LookAt(Camera.main.transform);
                textObj.transform.Rotate(0, 180, 0);
            }

            yield return null;
        }

        // ✅ NOVO - Retorna ao pool ao invés de destruir
        ReturnToPool(textObj);
    }

    /// <summary>
    /// ✅ NOVO - Estatísticas do pool (debug)
    /// </summary>
    public void LogPoolStats()
    {
        Debug.Log($"📊 DamageText Pool Stats:");
        Debug.Log($"   Available: {damageTextPool.Count}");
        Debug.Log($"   Active: {activeTexts.Count}");
        Debug.Log($"   Total: {damageTextPool.Count + activeTexts.Count}/{maxPoolSize}");
    }

    private void OnDestroy()
    {
        // Limpa pool
        while (damageTextPool.Count > 0)
        {
            GameObject obj = damageTextPool.Dequeue();
            if (obj != null)
                Destroy(obj);
        }

        foreach (var obj in activeTexts)
        {
            if (obj != null)
                Destroy(obj);
        }

        activeTexts.Clear();
    }
}

public enum DamageType
{
    Physical,
    Magical,
    True
}
