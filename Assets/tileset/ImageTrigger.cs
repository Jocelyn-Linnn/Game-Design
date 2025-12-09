using UnityEngine;

/// <summary>
/// 圖片觸發器
/// 當玩家碰到這個物件時，會觸發顯示一張圖片
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ImageTrigger : MonoBehaviour
{
    [Header("圖片設置")]
    [Tooltip("要顯示的圖片")]
    [SerializeField] private Sprite imageToShow;
    
    [Header("觸發設置")]
    [Tooltip("是否只觸發一次")]
    [SerializeField] private bool triggerOnce = true;
    
    [Tooltip("玩家的 Layer 名稱")]
    [SerializeField] private string playerLayerName = "Player";
    
    [Tooltip("是否需要按鍵互動（如果為 false，碰到就觸發）")]
    [SerializeField] private bool requireInteraction = false;
    
    [Tooltip("互動按鍵")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    
    [Header("視覺設置")]
    [Tooltip("觸發後是否隱藏物件")]
    [SerializeField] private bool hideAfterTrigger = false;
    
    [Tooltip("觸發後是否銷毀物件")]
    [SerializeField] private bool destroyAfterTrigger = false;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;
    
    private bool hasTriggered = false;
    private bool playerInRange = false;
    private int playerLayer;
    private SpriteRenderer spriteRenderer;
    
    void Start()
    {
        // Get player layer
        playerLayer = LayerMask.NameToLayer(playerLayerName);
        
        // Get sprite renderer if exists
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Ensure collider is trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            if (showDebugMessages)
            {
                Debug.Log($"ImageTrigger: Set collider to trigger on {gameObject.name}");
            }
        }
        
        // Validate setup
        if (imageToShow == null)
        {
            Debug.LogWarning($"ImageTrigger: No image assigned on {gameObject.name}!");
        }
        
        if (ImagePopupManager.Instance == null)
        {
            Debug.LogError("ImageTrigger: ImagePopupManager not found in scene! Please add one.");
        }
    }
    
    void Update()
    {
        // Handle interaction if required
        if (requireInteraction && playerInRange && !hasTriggered)
        {
            if (Input.GetKeyDown(interactionKey))
            {
                TriggerImage();
            }
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (showDebugMessages)
        {
            Debug.Log($"ImageTrigger: 偵測到物件進入 '{other.gameObject.name}' (Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
            Debug.Log($"ImageTrigger: 玩家 Layer 應該是 '{playerLayerName}' (ID: {playerLayer})");
        }
        
        // Check if it's the player
        if (other.gameObject.layer != playerLayer)
        {
            if (showDebugMessages)
            {
                Debug.Log($"ImageTrigger: ❌ Layer 不符合，忽略此物件");
            }
            return;
        }
        
        playerInRange = true;
        
        if (showDebugMessages)
        {
            Debug.Log($"✅ ImageTrigger: 玩家進入觸發區域 on {gameObject.name}");
        }
        
        // If no interaction required, trigger immediately
        if (!requireInteraction && !hasTriggered)
        {
            TriggerImage();
        }
    }
    
    void OnTriggerExit2D(Collider2D other)
    {
        // Check if it's the player
        if (other.gameObject.layer != playerLayer)
            return;
        
        playerInRange = false;
        
        if (showDebugMessages)
        {
            Debug.Log($"ImageTrigger: Player left trigger zone on {gameObject.name}");
        }
    }
    
    /// <summary>
    /// 觸發圖片顯示
    /// </summary>
    private void TriggerImage()
    {
        // Check if already triggered
        if (triggerOnce && hasTriggered)
            return;
        
        // Check if image is assigned
        if (imageToShow == null)
        {
            Debug.LogWarning($"ImageTrigger: Cannot show image - no sprite assigned on {gameObject.name}!");
            return;
        }
        
        // Check if manager exists
        if (ImagePopupManager.Instance == null)
        {
            Debug.LogError("ImageTrigger: ImagePopupManager not found!");
            return;
        }
        
        // Mark as triggered
        hasTriggered = true;
        
        if (showDebugMessages)
        {
            Debug.Log($"ImageTrigger: Showing image from {gameObject.name}");
        }
        
        // Show the image
        ImagePopupManager.Instance.ShowImage(imageToShow);
        
        // Handle post-trigger actions
        if (hideAfterTrigger && spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
        
        if (destroyAfterTrigger)
        {
            Destroy(gameObject, 0.1f); // Small delay to ensure trigger completes
        }
    }
    
    /// <summary>
    /// 手動重置觸發狀態（用於測試或特殊需求）
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
    }
    
    // Visual feedback in editor
    void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = hasTriggered ? Color.gray : Color.yellow;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}

