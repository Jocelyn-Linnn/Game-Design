using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnergyUIDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerEnergy energySystem;

    [Header("UI Setup")]
    [SerializeField] private Transform energyBlockContainer;
    [SerializeField] private GameObject energyBlockPrefab;

    [Header("Visual Settings")]
    [SerializeField] private Vector2 blockSize = new Vector2(30, 30);
    [SerializeField] private float blockSpacing = 3f;

    [Header("Color Settings")]
    [SerializeField] private Color fullColor = Color.white; // 顯示原圖顏色
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.2f); // 變暗

    [Header("Animation Settings")]
    [SerializeField] private float colorLerpDuration = 0.3f;
    [SerializeField] private float scalePulseDuration = 0.2f;
    [SerializeField] private float gainScaleMultiplier = 1.5f; // More dramatic for gaining
    [SerializeField] private float lossScaleMultiplier = 0.7f; // Shrink when losing
    [SerializeField] private float bounceAmount = 0.15f; // Bounce effect intensity
    [SerializeField] private bool enableFlashEffect = true;
    [SerializeField] private Color flashColor = new Color(1f, 1f, 0.5f, 1f); // Yellow flash
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private float cascadeDelay = 0.08f; // Delay between each block when gaining multiple

    private Image[] energyBlocks;
    private Coroutine[] animCoroutines;
    private int previousEnergy = -1; // Track previous energy to detect which block changed

    public static EnergyUIDisplay Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("EnergyUIDisplay: Multiple instances found! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (energySystem == null)
            energySystem = FindFirstObjectByType<PlayerEnergy>();

        if (energySystem == null)
        {
            Debug.LogError("EnergyUIDisplay: No PlayerEnergy system found!");
            enabled = false;
            return;
        }

        energySystem.OnEnergyChanged += UpdateEnergyDisplay;
        CreateEnergyBlocks();
        UpdateEnergyDisplay(energySystem.GetCurrentEnergy(), energySystem.GetMaxEnergy());

        // 一開始在陸地上隱藏UI，直到跌落後才顯示
        gameObject.SetActive(false);
        Debug.Log("EnergyUIDisplay: UI hidden at game start - will show after pitfall");
    }

    void OnDestroy()
    {
        if (energySystem != null)
            energySystem.OnEnergyChanged -= UpdateEnergyDisplay;
    }

    private void CreateEnergyBlocks()
    {
        int maxEnergy = energySystem.GetMaxEnergy();
        energyBlocks = new Image[maxEnergy];
        animCoroutines = new Coroutine[maxEnergy];

        for (int i = 0; i < maxEnergy; i++)
        {
            GameObject block = Instantiate(energyBlockPrefab, energyBlockContainer);
            RectTransform rt = block.GetComponent<RectTransform>();
            rt.sizeDelta = blockSize;
            energyBlocks[i] = block.GetComponent<Image>();
        }

        HorizontalLayoutGroup layout = energyBlockContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = energyBlockContainer.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.spacing = blockSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
    }

    private void UpdateEnergyDisplay(int current, int max)
    {
        // If UI is inactive, just update without animation to avoid coroutine errors
        if (!gameObject.activeSelf)
        {
            // Update all blocks instantly without animation
            for (int i = 0; i < energyBlocks.Length; i++)
            {
                if (energyBlocks[i] == null) continue;

                bool shouldBeFull = i < current;
                energyBlocks[i].color = shouldBeFull ? fullColor : emptyColor;
                energyBlocks[i].rectTransform.localScale = Vector3.one;
            }
            previousEnergy = current;
            return;
        }

        // First time initialization - set all blocks without animation
        if (previousEnergy == -1)
        {
            previousEnergy = current;
            for (int i = 0; i < energyBlocks.Length; i++)
            {
                if (energyBlocks[i] == null) continue;
                
                bool shouldBeFull = i < current;
                energyBlocks[i].color = shouldBeFull ? fullColor : emptyColor;
                energyBlocks[i].rectTransform.localScale = Vector3.one;
            }
            return;
        }

        bool isGaining = current > previousEnergy;
        bool isLosing = current < previousEnergy;

        // Update all blocks
        for (int i = 0; i < energyBlocks.Length; i++)
        {
            bool shouldBeFull = i < current;
            Image img = energyBlocks[i];
            if (img == null) continue;

            Color targetColor = shouldBeFull ? fullColor : emptyColor;

            bool shouldAnimate = false;
            float animationDelay = 0f;

            if (isGaining)
            {
                // Animate all blocks that were just gained (between previousEnergy and current)
                if (i >= previousEnergy && i < current)
                {
                    shouldAnimate = true;
                    // Cascade effect: delay each block slightly
                    animationDelay = (i - previousEnergy) * cascadeDelay;
                }
            }
            else if (isLosing)
            {
                // Only animate the single block that was lost
                if (i == current) // The first empty block
                {
                    shouldAnimate = true;
                    animationDelay = 0f;
                }
            }

            if (shouldAnimate && gameObject.activeSelf)
            {
                // Stop previous animation
                if (animCoroutines[i] != null)
                    StopCoroutine(animCoroutines[i]);

                // Start animation with delay
                animCoroutines[i] = StartCoroutine(AnimateBlockWithDelay(img, targetColor, isGaining, animationDelay));
            }
            else
            {
                // Other blocks - just update color instantly without animation
                img.color = targetColor;
                img.rectTransform.localScale = Vector3.one;
            }
        }

        previousEnergy = current;
    }

    private IEnumerator AnimateBlockWithDelay(Image img, Color targetColor, bool gainedEnergy, float delay)
    {
        // Safety check - if gameObject became inactive during animation, stop
        if (!gameObject.activeSelf) yield break;

        // Wait for cascade delay
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        // Safety check again after delay
        if (!gameObject.activeSelf) yield break;

        // Start the actual animation
        yield return StartCoroutine(AnimateBlock(img, targetColor, gainedEnergy));
    }

    private IEnumerator AnimateBlock(Image img, Color targetColor, bool gainedEnergy)
    {
        RectTransform rt = img.rectTransform;
        Color startColor = img.color;
        
        if (gainedEnergy)
        {
            // ENERGY GAIN ANIMATION - Pop, flash, and bounce
            yield return StartCoroutine(AnimateEnergyGain(img, rt, startColor, targetColor));
        }
        else
        {
            // ENERGY LOSS ANIMATION - Shrink and fade out
            yield return StartCoroutine(AnimateEnergyLoss(img, rt, startColor, targetColor));
        }

        // Ensure final state
        img.color = targetColor;
        rt.localScale = Vector3.one;
    }

    private IEnumerator AnimateEnergyGain(Image img, RectTransform rt, Color startColor, Color targetColor)
    {
        // Flash effect
        if (enableFlashEffect)
        {
            float flashTime = 0f;
            while (flashTime < flashDuration)
            {
                flashTime += Time.deltaTime;
                float t = flashTime / flashDuration;
                img.color = Color.Lerp(flashColor, targetColor, t);
                yield return null;
            }
        }

        // Pop up with overshoot (elastic bounce)
        float popTime = 0f;
        float totalPopDuration = scalePulseDuration * 2f;
        
        while (popTime < totalPopDuration)
        {
            popTime += Time.deltaTime;
            float t = popTime / totalPopDuration;
            
            // Elastic ease-out for bouncy effect
            float scale = EaseOutElastic(t, 1f, gainScaleMultiplier);
            rt.localScale = Vector3.one * scale;
            
            // Color transition
            img.color = Color.Lerp(startColor, targetColor, t);
            
            yield return null;
        }

        // Settle back to normal with slight bounce
        float settleTime = 0f;
        float settleDuration = scalePulseDuration;
        
        while (settleTime < settleDuration)
        {
            settleTime += Time.deltaTime;
            float t = settleTime / settleDuration;
            
            // Bounce back effect
            float scale = Mathf.Lerp(gainScaleMultiplier, 1f, EaseOutBack(t));
            rt.localScale = Vector3.one * scale;
            
            yield return null;
        }
    }

    private IEnumerator AnimateEnergyLoss(Image img, RectTransform rt, Color startColor, Color targetColor)
    {
        // PHASE 1: Quick enlarge (like a burst before disappearing)
        float enlargeTime = 0f;
        float enlargeDuration = scalePulseDuration * 0.5f;
        float enlargeScale = 1.3f; // Pop up before shrinking
        
        while (enlargeTime < enlargeDuration)
        {
            enlargeTime += Time.deltaTime;
            float t = enlargeTime / enlargeDuration;
            
            // Quick pop up
            float scale = Mathf.Lerp(1f, enlargeScale, EaseOutQuad(t));
            rt.localScale = Vector3.one * scale;
            
            yield return null;
        }

        // PHASE 2: Rapid shrink
        float shrinkTime = 0f;
        float shrinkDuration = scalePulseDuration * 0.8f;
        
        while (shrinkTime < shrinkDuration)
        {
            shrinkTime += Time.deltaTime;
            float t = shrinkTime / shrinkDuration;
            
            // Ease-in for quick shrink
            float scale = Mathf.Lerp(enlargeScale, lossScaleMultiplier, EaseInCubic(t));
            rt.localScale = Vector3.one * scale;
            
            yield return null;
        }

        // PHASE 3: Fade color while settling to normal size
        float fadeTime = 0f;
        
        while (fadeTime < colorLerpDuration)
        {
            fadeTime += Time.deltaTime;
            float t = fadeTime / colorLerpDuration;
            
            // Color fade
            img.color = Color.Lerp(startColor, targetColor, t);
            
            // Subtle bounce back to normal size
            float scale = Mathf.Lerp(lossScaleMultiplier, 1f, EaseOutQuad(t));
            rt.localScale = Vector3.one * scale;
            
            yield return null;
        }
    }

    // Easing functions for smooth animations
    private float EaseOutElastic(float t, float start, float end)
    {
        float c4 = (2f * Mathf.PI) / 3f;
        
        if (t == 0f) return start;
        if (t == 1f) return end;
        
        float value = start + (end - start) * (Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f);
        return value;
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    /// <summary>
    /// 強制顯示UI並刷新顯示狀態
    /// </summary>
    public void ForceShowUI()
    {
        gameObject.SetActive(true);
        if (energySystem != null)
        {
            UpdateEnergyDisplay(energySystem.GetCurrentEnergy(), energySystem.GetMaxEnergy());
        }
        Debug.Log("EnergyUIDisplay: UI force shown and refreshed");
    }
}