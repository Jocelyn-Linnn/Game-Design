using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 圖片彈出管理器
/// 管理圖片顯示的 UI 介面
/// </summary>
public class ImagePopupManager : MonoBehaviour
{
    public static ImagePopupManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("圖片顯示的 UI Image 組件")]
    [SerializeField] private Image imageDisplay;

    [Tooltip("背景遮罩（可選）")]
    [SerializeField] private Image backgroundOverlay;

    [Tooltip("整個彈出視窗的父物件")]
    [SerializeField] private GameObject popupPanel;

    [Header("Display Settings")]
    [Tooltip("圖片顯示的最大寬度")]
    [SerializeField] private float maxWidth = 1200f;

    [Tooltip("圖片顯示的最大高度")]
    [SerializeField] private float maxHeight = 900f;

    [Tooltip("是否保持圖片原始比例")]
    [SerializeField] private bool preserveAspectRatio = true;

    [Header("Settings")]
    [Tooltip("關閉圖片的按鍵")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Tooltip("點擊任意鍵關閉")]
    [SerializeField] private bool closeOnAnyKey = true;

    [Tooltip("自動關閉時間（秒）。設為 0 表示不自動關閉")]
    [SerializeField] private float autoCloseDelay = 5f;

    [Tooltip("淡入時間")]
    [SerializeField] private float fadeInDuration = 0.3f;

    [Tooltip("淡出時間")]
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private bool isShowing = false;
    private CanvasGroup canvasGroup;
    private Coroutine autoCloseCoroutine;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("ImagePopupManager: Multiple instances detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Validate references
        if (imageDisplay == null)
        {
            Debug.LogError("ImagePopupManager: Image Display is not assigned!");
        }

        if (popupPanel == null)
        {
            Debug.LogError("ImagePopupManager: Popup Panel is not assigned!");
        }

        // Get or add CanvasGroup for fading
        if (popupPanel != null)
        {
            canvasGroup = popupPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = popupPanel.AddComponent<CanvasGroup>();
            }
        }

        // Initially hide the popup
        HideImmediate();
    }

    void Update()
    {
        // Handle closing
        if (isShowing)
        {
            if (Input.GetKeyDown(closeKey) || (closeOnAnyKey && Input.anyKeyDown))
            {
                HideImage();
            }
        }
    }

    /// <summary>
    /// 顯示圖片
    /// </summary>
    public void ShowImage(Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.LogWarning("ImagePopupManager: Cannot show null sprite!");
            return;
        }

        if (imageDisplay == null || popupPanel == null)
        {
            Debug.LogError("ImagePopupManager: Missing references!");
            return;
        }

        if (showDebugMessages)
        {
            Debug.Log($"ImagePopupManager: Showing image '{sprite.name}'");
            Debug.Log($"ImagePopupManager: Image size: {sprite.rect.width} x {sprite.rect.height}");
        }

        // Set the image
        imageDisplay.sprite = sprite;
        
        // 確保圖片可見
        imageDisplay.enabled = true;
        Color color = imageDisplay.color;
        color.a = 1f; // 確保完全不透明
        imageDisplay.color = color;
        
        // 設置適當的大小
        RectTransform rectTransform = imageDisplay.GetComponent<RectTransform>();
        if (rectTransform != null && preserveAspectRatio)
        {
            // 保持圖片原始比例
            float imageRatio = sprite.rect.width / sprite.rect.height;
            float targetHeight = maxHeight;
            float targetWidth = targetHeight * imageRatio;
            
            // 如果太寬，限制寬度
            if (targetWidth > maxWidth)
            {
                targetWidth = maxWidth;
                targetHeight = targetWidth / imageRatio;
            }
            
            rectTransform.sizeDelta = new Vector2(targetWidth, targetHeight);
            
            if (showDebugMessages)
            {
                Debug.Log($"ImagePopupManager: Display size set to {targetWidth} x {targetHeight}");
            }
        }
        else if (rectTransform != null && !preserveAspectRatio)
        {
            // 固定大小，不保持比例
            rectTransform.sizeDelta = new Vector2(maxWidth, maxHeight);
            
            if (showDebugMessages)
            {
                Debug.Log($"ImagePopupManager: Display size set to {maxWidth} x {maxHeight} (固定大小)");
            }
        }

        // Show the popup
        popupPanel.SetActive(true);
        isShowing = true;

        // Stop any existing coroutines
        StopAllCoroutines();

        // Fade in
        StartCoroutine(FadeIn());

        // Start auto close timer if enabled
        if (autoCloseDelay > 0)
        {
            autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay());
        }
    }

    /// <summary>
    /// 自動關閉計時器
    /// </summary>
    private System.Collections.IEnumerator AutoCloseAfterDelay()
    {
        if (showDebugMessages)
        {
            Debug.Log($"ImagePopupManager: 將在 {autoCloseDelay} 秒後自動關閉");
        }

        yield return new WaitForSeconds(autoCloseDelay);

        if (showDebugMessages)
        {
            Debug.Log("ImagePopupManager: 自動關閉圖片");
        }

        HideImage();
    }

    /// <summary>
    /// 隱藏圖片
    /// </summary>
    public void HideImage()
    {
        if (!isShowing)
            return;

        if (showDebugMessages)
        {
            Debug.Log("ImagePopupManager: Hiding image");
        }

        // Stop auto close coroutine if running
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }

        // Fade out
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    /// <summary>
    /// 立即隱藏（不淡出）
    /// </summary>
    private void HideImmediate()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        isShowing = false;
    }

    /// <summary>
    /// 淡入效果
    /// </summary>
    private System.Collections.IEnumerator FadeIn()
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float elapsed = 0f;
        canvasGroup.alpha = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 淡出效果
    /// </summary>
    private System.Collections.IEnumerator FadeOut()
    {
        if (canvasGroup == null)
        {
            popupPanel.SetActive(false);
            isShowing = false;
            yield break;
        }

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        popupPanel.SetActive(false);
        isShowing = false;
    }

    /// <summary>
    /// 檢查是否正在顯示圖片
    /// </summary>
    public bool IsShowing()
    {
        return isShowing;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

