using UnityEngine;
using UnityEngine.Video;
using System.Collections;

/// <summary>
/// 實體門腳本 - 有碰撞的門，需要鑰匙才能開啟
/// 當玩家碰撞到門時，如果有正確的鑰匙，會在背包鑰匙物件上方顯示按鍵提示
/// 玩家按下 Z 鍵可以使用鑰匙開門
/// 開門後會播放白光閃爍效果，接著播放影片
/// </summary>
public class PhysicalDoor : MonoBehaviour
{
    [Header("門的配置")]
    [Tooltip("開啟門所需的鑰匙標籤")]
    public string requiredKeyTag = "key1";
    
    [Tooltip("門開啟時的 Sprite（可選，如果設置則使用此 Sprite 而非透明度動畫）")]
    public Sprite doorOpenedSprite;
    
    [Tooltip("門開啟時的透明度 (0-1)")]
    [Range(0f, 1f)]
    public float openTransparency = 0.5f;
    
    [Tooltip("門開啟動畫的持續時間")]
    public float animationDuration = 1f;
    
    [Header("音效")]
    [Tooltip("門開啟時的音效")]
    public AudioClip openSound;
    
    [Tooltip("門開啟音效的音量 (0.0 到 1.0)")]
    [Range(0f, 1f)]
    public float openSoundVolume = 1f;
    
    [Tooltip("門關閉時的音效")]
    public AudioClip closeSound;
    
    [Tooltip("門關閉音效的音量 (0.0 到 1.0)")]
    [Range(0f, 1f)]
    public float closeSoundVolume = 1f;
    
    [Header("視覺效果")]
    [Tooltip("門開啟時的粒子效果")]
    public GameObject openEffect;
    
    [Header("白光與影片設置")]
    [Tooltip("是否在開門後播放白光閃爍效果")]
    public bool enableWhiteFlash = true;
    
    [Tooltip("白光持續時間（秒）")]
    public float flashDuration = 1f;
    
    [Tooltip("是否在白光後播放影片")]
    public bool enableVideoPlayback = true;
    
    [Tooltip("要播放的影片檔案")]
    public VideoClip videoClip;
    
    private bool isOpened = false;
    private bool isAnimating = false;
    private bool isPlayerNearby = false;
    private PlayerController nearbyPlayer = null;
    private SpriteRenderer spriteRenderer;
    private Collider2D doorCollider;
    private AudioSource audioSource;
    private SoundManager soundManager;
    private Color originalColor;
    private Color targetColor;
    private InventoryPromptManager promptManager;
    
    void Start()
    {
        // 獲取組件
        spriteRenderer = GetComponent<SpriteRenderer>();
        doorCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        soundManager = SoundManager.GetInstance();
        promptManager = InventoryPromptManager.GetInstance();
        
        // 如果沒有 AudioSource，添加一個（作為 fallback）
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // 檢查必要組件
        if (spriteRenderer == null)
        {
            Debug.LogError($"PhysicalDoor: {gameObject.name} 缺少 SpriteRenderer 組件！");
        }
        
        if (doorCollider == null)
        {
            Debug.LogError($"PhysicalDoor: {gameObject.name} 缺少 Collider2D 組件！");
        }
        else
        {
            // 確保碰撞器不是觸發器
            doorCollider.isTrigger = false;
        }
        
        // 儲存原始顏色
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }
    
    void Update()
    {
        // 如果玩家在附近且有鑰匙，按 Z 鍵開門
        if (isPlayerNearby && !isOpened && !isAnimating && nearbyPlayer != null)
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (HasRequiredKey(nearbyPlayer))
                {
                    ConsumeKeyAndOpenDoor(nearbyPlayer);
                    HideKeyPrompt();
                }
            }
        }
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 如果門已經開啟或正在動畫中，忽略碰撞
        if (isOpened || isAnimating) return;
        
        // 檢查碰撞的物件是否是玩家
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null) return;
        
        isPlayerNearby = true;
        nearbyPlayer = player;
        
        // 檢查玩家是否有鑰匙
        if (HasRequiredKey(player))
        {
            // 顯示背包鑰匙物件上方的按鍵提示
            ShowKeyPrompt(player);
        }
        else
        {
            // 玩家沒有鑰匙，顯示提示
            ShowKeyRequiredMessage();
        }
    }
    
    void OnCollisionExit2D(Collision2D collision)
    {
        // 檢查離開的物件是否是玩家
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null) return;
        
        isPlayerNearby = false;
        nearbyPlayer = null;
        
        // 隱藏按鍵提示
        HideKeyPrompt();
    }
    
    /// <summary>
    /// 檢查玩家是否有所需的鑰匙
    /// </summary>
    private bool HasRequiredKey(PlayerController player)
    {
        // 方法1: 檢查 InventorySystem (如果使用物品系統)
        InventorySystem inventory = player.GetComponent<InventorySystem>();
        if (inventory != null)
        {
            ItemData keyItem = inventory.GetItemForTag(requiredKeyTag);
            if (keyItem != null)
            {
                return true;
            }
        }
        
        // 方法2: 檢查 KeyInventory (如果使用鑰匙系統)
        KeyInventory keyInventory = player.GetComponent<KeyInventory>();
        if (keyInventory != null)
        {
            return keyInventory.HasKey(requiredKeyTag);
        }
        
        return false;
    }
    
    /// <summary>
    /// 消耗鑰匙並開啟門
    /// </summary>
    private void ConsumeKeyAndOpenDoor(PlayerController player)
    {
        if (isOpened || isAnimating) return;
        
        Debug.Log($"門被開啟！消耗鑰匙: {requiredKeyTag}");
        
        // 先隱藏提示（在消耗鑰匙之前）
        HideKeyPrompt();
        
        // 消耗鑰匙
        ConsumeKey(player);
        
        // 開始開啟動畫
        StartCoroutine(OpenDoorAnimation());
    }
    
    /// <summary>
    /// 消耗鑰匙
    /// </summary>
    private void ConsumeKey(PlayerController player)
    {
        // 方法1: 從 InventorySystem 消耗物品
        InventorySystem inventory = player.GetComponent<InventorySystem>();
        if (inventory != null)
        {
            ItemData keyItem = inventory.GetItemForTag(requiredKeyTag);
            if (keyItem != null)
            {
                inventory.RemoveItem(keyItem, 1);
                Debug.Log($"從物品欄消耗鑰匙: {keyItem.itemName}");
                return;
            }
        }
        
        // 方法2: 從 KeyInventory 消耗鑰匙
        KeyInventory keyInventory = player.GetComponent<KeyInventory>();
        if (keyInventory != null)
        {
            if (keyInventory.HasKey(requiredKeyTag))
            {
                keyInventory.RemoveKey(requiredKeyTag);
                Debug.Log($"從鑰匙欄消耗鑰匙: {requiredKeyTag}");
            }
        }
    }
    
    /// <summary>
    /// Show message that key is required to open the door
    /// </summary>
    private void ShowKeyRequiredMessage()
    {
        Debug.Log($"Key required to open this door: {requiredKeyTag}");
        
        // Can add UI prompt here
        // For example, show floating text or UI prompt
        FloatingTextManager floatingTextManager = FloatingTextManager.GetInstance();
        if (floatingTextManager != null)
        {
            floatingTextManager.ShowFloatingText("Key required!", transform.position, Color.red);
        }
    }
    
    /// <summary>
    /// 在背包鑰匙物件上方顯示按鍵提示
    /// </summary>
    private void ShowKeyPrompt(PlayerController player)
    {
        if (promptManager == null)
        {
            promptManager = InventoryPromptManager.GetInstance();
        }
        
        if (promptManager != null)
        {
            InventorySystem inventory = player.GetComponent<InventorySystem>();
            if (inventory != null)
            {
                // 根據 tag 顯示提示
                promptManager.ShowPromptForTag(requiredKeyTag, inventory);
                Debug.Log($"PhysicalDoor: 顯示鑰匙按鍵提示 (tag: {requiredKeyTag})");
            }
        }
    }
    
    /// <summary>
    /// 隱藏背包鑰匙物件上方的按鍵提示
    /// </summary>
    private void HideKeyPrompt()
    {
        if (promptManager != null && nearbyPlayer != null)
        {
            InventorySystem inventory = nearbyPlayer.GetComponent<InventorySystem>();
            if (inventory != null)
            {
                promptManager.HidePromptForTag(requiredKeyTag, inventory);
                Debug.Log($"PhysicalDoor: 隱藏鑰匙按鍵提示 (tag: {requiredKeyTag})");
            }
        }
    }
    
    /// <summary>
    /// 開啟門的動畫協程
    /// </summary>
    private IEnumerator OpenDoorAnimation()
    {
        isAnimating = true;
        isOpened = true;
        
        // 播放開啟音效（優先使用 SoundManager，有 fallback）
        if (openSound != null)
        {
            if (soundManager != null)
            {
                // Use SoundManager for unified volume control
                soundManager.PlaySound(openSound, transform.position, openSoundVolume);
            }
            else if (audioSource != null)
            {
                // Fallback to direct playback if SoundManager is not available
                audioSource.PlayOneShot(openSound, openSoundVolume);
            }
        }
        
        // 播放粒子效果
        if (openEffect != null)
        {
            GameObject effect = Instantiate(openEffect, transform.position, Quaternion.identity);
            Destroy(effect, 5f); // 5秒後銷毀粒子效果
        }
        
        // 如果有設置開啟 Sprite，直接切換 Sprite
        if (doorOpenedSprite != null && spriteRenderer != null)
        {
            Debug.Log("PhysicalDoor: 切換到開啟 Sprite");
            spriteRenderer.sprite = doorOpenedSprite;
            // 確保顏色完全不透明
            spriteRenderer.color = Color.white;
        }
        else
        {
            // 計算目標顏色（透明）
            targetColor = new Color(originalColor.r, originalColor.g, originalColor.b, openTransparency);
            
            // 動畫參數
            float elapsedTime = 0f;
            Color startColor = originalColor;
            
            // 透明度動畫
            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / animationDuration;
                
                // 使用平滑插值
                Color currentColor = Color.Lerp(startColor, targetColor, progress);
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = currentColor;
                }
                
                yield return null;
            }
            
            // 確保最終顏色正確
            if (spriteRenderer != null)
            {
                spriteRenderer.color = targetColor;
            }
        }
        
        // 禁用碰撞器，讓玩家可以穿過
        if (doorCollider != null)
        {
            doorCollider.enabled = false;
        }
        
        isAnimating = false;
        
        Debug.Log("門已完全開啟！");
        
        // 播放白光閃爍效果和影片
        if (enableWhiteFlash || enableVideoPlayback)
        {
            StartCoroutine(PlayFlashAndVideo());
        }
    }
    
    /// <summary>
    /// 播放白光閃爍和影片的協程
    /// </summary>
    private IEnumerator PlayFlashAndVideo()
    {
        // 1. 播放白光閃爍效果
        if (enableWhiteFlash)
        {
            WhiteFlashEffect flashEffect = WhiteFlashEffect.GetInstance();
            
            if (flashEffect != null)
            {
                Debug.Log("PhysicalDoor: 開始播放白光閃爍效果");
                
                bool flashCompleted = false;
                flashEffect.PlayFlash(flashDuration, 0.1f, 0.3f, () => 
                {
                    flashCompleted = true;
                });
                
                // 等待白光效果完成
                while (!flashCompleted)
                {
                    yield return null;
                }
                
                Debug.Log("PhysicalDoor: 白光閃爍效果完成");
            }
            else
            {
                Debug.LogWarning("PhysicalDoor: 找不到 WhiteFlashEffect 實例！");
                // 如果找不到白光效果，等待指定的時間
                yield return new WaitForSeconds(flashDuration);
            }
        }
        
        // 2. 播放影片
        if (enableVideoPlayback && videoClip != null)
        {
            VideoPlayerController videoController = VideoPlayerController.GetInstance();
            
            if (videoController != null)
            {
                Debug.Log("PhysicalDoor: 開始播放影片");
                
                bool videoCompleted = false;
                videoController.PlayVideo(videoClip, () => 
                {
                    videoCompleted = true;
                });
                
                // 等待影片播放完成
                while (!videoCompleted)
                {
                    yield return null;
                }
                
                Debug.Log("PhysicalDoor: 影片播放完成");
            }
            else
            {
                Debug.LogWarning("PhysicalDoor: 找不到 VideoPlayerController 實例！");
            }
        }
        else if (enableVideoPlayback && videoClip == null)
        {
            Debug.LogWarning("PhysicalDoor: 已啟用影片播放但未設置影片檔案！");
        }
    }
    
    /// <summary>
    /// 關閉門（可選功能）
    /// </summary>
    public void CloseDoor()
    {
        if (!isOpened || isAnimating) return;
        
        StartCoroutine(CloseDoorAnimation());
    }
    
    /// <summary>
    /// 關閉門的動畫協程
    /// </summary>
    private IEnumerator CloseDoorAnimation()
    {
        isAnimating = true;
        
        // 播放關閉音效（優先使用 SoundManager，有 fallback）
        if (closeSound != null)
        {
            if (soundManager != null)
            {
                // Use SoundManager for unified volume control
                soundManager.PlaySound(closeSound, transform.position, closeSoundVolume);
            }
            else if (audioSource != null)
            {
                // Fallback to direct playback if SoundManager is not available
                audioSource.PlayOneShot(closeSound, closeSoundVolume);
            }
        }
        
        // 重新啟用碰撞器
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }
        
        // 計算目標顏色（不透明）
        Color startColor = spriteRenderer != null ? spriteRenderer.color : originalColor;
        
        // 動畫參數
        float elapsedTime = 0f;
        
        // 透明度動畫
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            
            // 使用平滑插值
            Color currentColor = Color.Lerp(startColor, originalColor, progress);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = currentColor;
            }
            
            yield return null;
        }
        
        // 確保最終顏色正確
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        isOpened = false;
        isAnimating = false;
        
        Debug.Log("門已關閉！");
    }
    
    /// <summary>
    /// 檢查門是否已開啟
    /// </summary>
    public bool IsOpened()
    {
        return isOpened;
    }
    
    /// <summary>
    /// 重置門的狀態（用於測試或重新開始）
    /// </summary>
    public void ResetDoor()
    {
        isOpened = false;
        isAnimating = false;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }
        
        Debug.Log("門已重置！");
    }
}
