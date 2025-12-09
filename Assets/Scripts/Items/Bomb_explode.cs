using UnityEngine;
using UnityEngine.Tilemaps;

public class Bomb_explode : MonoBehaviour
{
    [Header("Explosion Settings")]
    [Tooltip("爆炸範圍半徑")]
    [SerializeField] private float explosionRadius = 2.5f;
    
    [Tooltip("是否顯示爆炸範圍 (僅用於除錯)")]
    [SerializeField] private bool showExplosionGizmo = true;

    [Header("Audio Settings")]
    [Tooltip("Audio clip to play when bomb explodes")]
    [SerializeField] private AudioClip explosionSound;
    
    [Tooltip("Volume of the explosion sound (0.0 to 1.0)")]
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 1f;
    
    [Tooltip("Delay before playing explosion sound (in seconds)")]
    [SerializeField] private float soundDelay = 1f;
    
    [Tooltip("Whether to play explosion sound automatically when bomb spawns")]
    [SerializeField] private bool playOnStart = true;

    private bool hasPlayedSound = false;

    void Start()
    {
        // Play explosion sound on start if enabled
        if (playOnStart)
        {
            PlayExplosionSound();
        }
    }

    void Update()
    {

    }

    /// <summary>
    /// Play explosion sound effect at bomb position with delay
    /// Can be called via Animation Event or automatically on Start
    /// </summary>
    public void PlayExplosionSound()
    {
        // Prevent playing sound multiple times
        if (hasPlayedSound)
        {
            return;
        }

        hasPlayedSound = true;

        // Schedule sound playback with delay
        if (soundDelay > 0f)
        {
            Invoke(nameof(PlaySoundDelayed), soundDelay);
            Debug.Log($"Bomb_explode: Scheduled explosion sound to play in {soundDelay} seconds");
        }
        else
        {
            // Play immediately if no delay
            PlaySoundDelayed();
        }
    }

    /// <summary>
    /// Internal method to actually play the sound
    /// Called after delay by Invoke
    /// </summary>
    private void PlaySoundDelayed()
    {
        if (explosionSound != null)
        {
            // Play 3D sound at bomb position
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
            
            Debug.Log($"Bomb_explode: Playing explosion sound '{explosionSound.name}' at volume {explosionVolume}");
        }
        else
        {
            Debug.LogWarning("Bomb_explode: No explosion sound assigned!");
        }
    }

    // 🔥 這個函式會在爆炸動畫結束時由 Animation Event 呼叫
    public void OnExplosionEnd()
    {
        // 1. 摧毀特定名稱的地板物件（舊邏輯保留）
        GameObject targetFloor = GameObject.Find("Floor_destroy_by_bomb");
        GameObject targetFloor1 = GameObject.Find("grass1");

        if (targetFloor != null)
        {
            targetFloor.SetActive(false);
            if (targetFloor1 != null)
            {
            targetFloor1.SetActive(false);
            }
            var hint = FindObjectOfType<HintTrigger>();
            if (hint != null)
            {
                hint.OnLinkedFloorDestroyed();
            }
            Debug.Log("💥 Floor_destroy_by_bomb 已消失！");
        }

        // 2. 讓整個 "Floor_destroy_by_bomb" Tilemap 消失
        DestroyTargetTilemap();

        // 3. 偵測並摧毀範圍內的石頭
        DestroyNearbyStones();

        // 摧毀炸彈物件本身
        Destroy(gameObject, 0.2f);
    }

    /// <summary>
    /// 摧毀爆炸範圍內的所有石頭
    /// </summary>
    private void DestroyNearbyStones()
    {
        // 使用 OverlapCircleAll 偵測範圍內的所有物件
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        
        int stonesDestroyed = 0;
        foreach (var hitCollider in hitColliders)
        {
            // 檢查是否有 Stone 組件
            Stone stone = hitCollider.GetComponent<Stone>();
            if (stone != null)
            {
                Debug.Log($"💣 Bomb_explode: 摧毀石頭 '{hitCollider.gameObject.name}'");
                stone.Explode();
                stonesDestroyed++;
            }
        }

        if (stonesDestroyed > 0)
        {
            Debug.Log($"💥 Bomb_explode: 共摧毀了 {stonesDestroyed} 顆石頭！");
        }
    }

    /// <summary>
    /// 讓整個 "Floor_destroy_by_bomb" Tilemap 消失
    /// </summary>
    private void DestroyTargetTilemap()
    {
        // 尋找名為 "Floor_destroy_by_bomb" 的 GameObject
        GameObject targetTilemapObj = GameObject.Find("Floor_destroy_by_bomb");
        
        if (targetTilemapObj != null)
        {
            Debug.Log($"💥 Bomb_explode: 找到目標 Tilemap '{targetTilemapObj.name}'，讓它消失");
            targetTilemapObj.SetActive(false);
            Debug.Log("✅ Floor_destroy_by_bomb 已消失！");
        }
        else
        {
            // 如果找不到，嘗試搜尋所有 Tilemap
            Tilemap[] allTilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (var tilemap in allTilemaps)
            {
                if (tilemap.gameObject.name == "Floor_destroy_by_bomb" || 
                    tilemap.gameObject.name.Contains("Floor_destroy_by_bomb"))
                {
                    Debug.Log($"💥 Bomb_explode: 找到目標 Tilemap '{tilemap.gameObject.name}'，讓它消失");
                    tilemap.gameObject.SetActive(false);
                    Debug.Log("✅ Floor_destroy_by_bomb 已消失！");
                    return;
                }
            }
            
            Debug.LogWarning("⚠️ Bomb_explode: 找不到名為 'Floor_destroy_by_bomb' 的 Tilemap！");
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 在編輯器中顯示爆炸範圍（僅用於除錯）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (showExplosionGizmo)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // 橘色半透明
            Gizmos.DrawSphere(transform.position, explosionRadius);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
#endif
}
