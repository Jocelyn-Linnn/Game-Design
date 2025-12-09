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

        // 2. 偵測並摧毀範圍內的石頭
        DestroyNearbyStones();

        // 3. 偵測並破壞範圍內的 Tilemap 磚塊
        DestroyNearbyTiles();

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
    /// 破壞爆炸範圍內的 Tilemap 磚塊
    /// </summary>
    private void DestroyNearbyTiles()
    {
        // 尋找場景中所有的 Tilemap
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        
        if (tilemaps.Length == 0)
        {
            Debug.Log("Bomb_explode: 場景中沒有找到 Tilemap");
            return;
        }

        int tilesDestroyed = 0;
        Vector3 bombPosition = transform.position;

        foreach (var tilemap in tilemaps)
        {
            // 計算爆炸範圍涵蓋的磚塊座標範圍
            Vector3Int centerCell = tilemap.WorldToCell(bombPosition);
            int radiusInCells = Mathf.CeilToInt(explosionRadius / tilemap.cellSize.x);

            // 遍歷範圍內的所有磚塊
            for (int x = -radiusInCells; x <= radiusInCells; x++)
            {
                for (int y = -radiusInCells; y <= radiusInCells; y++)
                {
                    Vector3Int cellPosition = centerCell + new Vector3Int(x, y, 0);
                    
                    // 檢查這個位置是否有磚塊
                    if (tilemap.HasTile(cellPosition))
                    {
                        // 計算磚塊中心與炸彈的距離
                        Vector3 tileWorldPos = tilemap.GetCellCenterWorld(cellPosition);
                        float distance = Vector3.Distance(bombPosition, tileWorldPos);

                        // 如果在爆炸範圍內，就摧毀這個磚塊
                        if (distance <= explosionRadius)
                        {
                            tilemap.SetTile(cellPosition, null);
                            tilesDestroyed++;
                            Debug.Log($"💥 Bomb_explode: 破壞磚塊於 {cellPosition} (距離: {distance:F2})");
                        }
                    }
                }
            }
        }

        if (tilesDestroyed > 0)
        {
            Debug.Log($"💥 Bomb_explode: 共破壞了 {tilesDestroyed} 個磚塊！");
        }
        else
        {
            Debug.Log("Bomb_explode: 爆炸範圍內沒有磚塊可破壞");
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
