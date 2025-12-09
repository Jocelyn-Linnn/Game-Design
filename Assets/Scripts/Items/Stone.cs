// using UnityEngine;

// public class Stone : MonoBehaviour
// {
//     void Start()
//     {
//         // 可選：初始化
//     }

//     void Update()
//     {
//         // 可選：更新邏輯
//     }

//     // 當 Stone 與其他物件發生物理碰撞時執行
//     private void OnCollisionEnter2D(Collision2D collision)
//     {
//         if (collision.gameObject.name == "Floor_destroy_by_stone")
//         {   
//             Debug.Log("Stone 碰到了：" + collision.gameObject.name);
//             collision.gameObject.SetActive(false);
//             Debug.Log("🪨 Floor_destroy_by_stone 被石頭砸到後消失！");
//         }
//     }
// }


using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 可被炸彈破壞的石頭：
/// - 會掉落並與地板碰撞
/// - 可在玩家靠近時使用炸彈
/// - 可砸壞特定地板
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Stone : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("偵測玩家用的感應區域半徑")]
    [SerializeField] private float detectRadius = 2.0f;

    [Tooltip("是否顯示偵測範圍 (僅用於除錯)")]
    [SerializeField] private bool showGizmo = true;

    [Header("Physics Settings")]
    [Tooltip("石頭是否初始時被固定（炸彈炸後才會掉落）")]
    [SerializeField] private bool startKinematic = true;

    [Header("Collision Settings")]
    [Tooltip("石頭碰撞時破壞磚塊的範圍半徑")]
    [SerializeField] private float impactRadius = 1.5f;

    [Tooltip("是否顯示碰撞破壞範圍 (僅用於除錯)")]
    [SerializeField] private bool showImpactGizmo = true;

    [Tooltip("要破壞的 Tilemap 名稱")]
    [SerializeField] private string targetTilemapName = "Floor_destroy_by_bomb";

    private PlayerUseBomb nearbyPlayer;   // 記錄可互動的玩家
    private Rigidbody2D rb;
    private bool hasExploded = false;
    private bool isFalling = false;  // 是否正在掉落中
    private Tilemap targetTilemap = null;  // 目標 Tilemap 的引用

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("Stone: 缺少 Rigidbody2D！");
        }
        else
        {
            // 如果設定為初始固定，則設置為 Kinematic（不受重力影響）
            if (startKinematic)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                Debug.Log($"Stone '{gameObject.name}': 初始狀態為 Kinematic（固定）");
            }
        }

        // 確保 Collider 不是 Trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && col.isTrigger)
            col.isTrigger = false;
    }

    void Update()
    {
        DetectPlayerNearby();
    }

    void FixedUpdate()
    {
        // 如果石頭正在掉落，持續檢查並破壞目標 Tilemap 的磚塊
        if (isFalling && targetTilemap != null)
        {
            DestroyTilesAtPosition();
        }
    }

    /// <summary>
    /// 檢查玩家是否在感應範圍內
    /// </summary>
    private void DetectPlayerNearby()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                if (nearbyPlayer == null)
                {
                    nearbyPlayer = hit.GetComponent<PlayerUseBomb>();
                    if (nearbyPlayer != null)
                    {
                        nearbyPlayer.SetNearStone(true, this);
                        Debug.Log("🪨 玩家進入石頭範圍，可以使用炸彈 (Tag)");
                    }
                }
                return; // 已找到玩家，不用繼續找
            }
        }

        // 如果跑完沒有找到玩家
        if (nearbyPlayer != null)
        {
            nearbyPlayer.SetNearStone(false, null);
            nearbyPlayer = null;
            Debug.Log("🪨 玩家離開石頭範圍 (Tag)");
        }
    }

    /// <summary>
    /// 當石頭撞到地板時
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 只有在石頭已經被炸過（正在掉落）時才處理碰撞
        if (!hasExploded)
        {
            return;
        }

        Debug.Log($"🪨 Stone '{gameObject.name}' 碰撞到：{collision.gameObject.name}");

        // 檢查是否碰到目標 Tilemap
        Tilemap hitTilemap = collision.gameObject.GetComponent<Tilemap>();
        if (hitTilemap == null)
        {
            // 嘗試在父物件中尋找
            hitTilemap = collision.gameObject.GetComponentInParent<Tilemap>();
        }

        if (hitTilemap != null && (hitTilemap.gameObject.name == targetTilemapName || hitTilemap.gameObject.name.Contains(targetTilemapName)))
        {
            Debug.Log($"🪨 Stone 碰到目標 Tilemap '{hitTilemap.gameObject.name}'，破壞碰撞點的磚塊");
            // 破壞碰撞點周圍的磚塊（在 FixedUpdate 中持續處理）
            // 不停止掉落，讓石頭繼續穿透
            return;
        }

        // 處理特定名稱的地板物件（實體地板）
        if (collision.gameObject.name == "Floor_destroy_by_stone")
        {
            collision.gameObject.SetActive(false);
            Debug.Log("💥 Floor_destroy_by_stone 被石頭砸到後消失！");
        }

        // 碰到其他實體地板，停止掉落
        Debug.Log($"🪨 Stone 碰到實體地板 '{collision.gameObject.name}'，停止掉落");
        isFalling = false;
    }

    /// <summary>
    /// 在石頭當前位置持續破壞目標 Tilemap 的磚塊
    /// </summary>
    private void DestroyTilesAtPosition()
    {
        if (targetTilemap == null)
        {
            // 嘗試尋找目標 Tilemap
            targetTilemap = FindTilemapByName(targetTilemapName);
            if (targetTilemap == null)
            {
                return;
            }
        }

        Vector3 stonePosition = transform.position;
        DestroyTilesInRadius(targetTilemap, stonePosition);
    }

    /// <summary>
    /// 根據名稱尋找 Tilemap
    /// </summary>
    private Tilemap FindTilemapByName(string name)
    {
        // 先嘗試直接尋找 GameObject
        GameObject tilemapObj = GameObject.Find(name);
        if (tilemapObj != null)
        {
            Tilemap tilemap = tilemapObj.GetComponent<Tilemap>();
            if (tilemap != null)
            {
                return tilemap;
            }
        }

        // 如果找不到，搜尋所有 Tilemap
        Tilemap[] allTilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (var tilemap in allTilemaps)
        {
            if (tilemap.gameObject.name == name || tilemap.gameObject.name.Contains(name))
            {
                return tilemap;
            }
        }

        return null;
    }

    /// <summary>
    /// 在指定 Tilemap 中破壞指定位置周圍的磚塊
    /// </summary>
    private void DestroyTilesInRadius(Tilemap tilemap, Vector3 position)
    {
        int tilesDestroyed = 0;
        
        // 計算碰撞範圍涵蓋的磚塊座標範圍
        Vector3Int centerCell = tilemap.WorldToCell(position);
        int radiusInCells = Mathf.CeilToInt(impactRadius / tilemap.cellSize.x);

        // 遍歷範圍內的所有磚塊
        for (int x = -radiusInCells; x <= radiusInCells; x++)
        {
            for (int y = -radiusInCells; y <= radiusInCells; y++)
            {
                Vector3Int cellPosition = centerCell + new Vector3Int(x, y, 0);
                
                // 檢查這個位置是否有磚塊
                if (tilemap.HasTile(cellPosition))
                {
                    // 計算磚塊中心與碰撞點的距離
                    Vector3 tileWorldPos = tilemap.GetCellCenterWorld(cellPosition);
                    float distance = Vector3.Distance(position, tileWorldPos);

                    // 如果在碰撞範圍內，就摧毀這個磚塊
                    if (distance <= impactRadius)
                    {
                        tilemap.SetTile(cellPosition, null);
                        tilesDestroyed++;
                    }
                }
            }
        }

        if (tilesDestroyed > 0)
        {
            Debug.Log($"💥 Stone 在 '{tilemap.gameObject.name}' 中破壞了 {tilesDestroyed} 個磚塊");
        }
    }

    /// <summary>
    /// 被炸彈炸毀時呼叫 - 讓石頭開始掉落
    /// </summary>
    public void Explode()
    {
        if (hasExploded)
        {
            Debug.Log($"💣 Stone '{gameObject.name}' 已經被炸過了，忽略重複爆炸");
            return;
        }

        hasExploded = true;
        isFalling = true;  // 開始掉落狀態
        
        Debug.Log($"💣 Stone '{gameObject.name}' 被炸彈炸中！開始掉落...");

        // 預先尋找目標 Tilemap
        targetTilemap = FindTilemapByName(targetTilemapName);
        if (targetTilemap != null)
        {
            Debug.Log($"✅ Stone 找到目標 Tilemap: {targetTilemap.gameObject.name}");
            
            // 預先忽略目標 Tilemap 的碰撞，讓石頭可以穿透
            IgnoreTilemapCollision(targetTilemap);
        }
        else
        {
            Debug.LogWarning($"⚠️ Stone 找不到目標 Tilemap: {targetTilemapName}");
        }

        if (rb != null)
        {
            // 改變 Rigidbody2D 為 Dynamic，讓石頭受重力影響開始掉落
            rb.bodyType = RigidbodyType2D.Dynamic;
            
            // 可選：給予一個小的初始向下速度，讓掉落更明顯
            rb.linearVelocity = new Vector2(0, -0.5f);
            
            Debug.Log($"🪨 Stone '{gameObject.name}' 已解除固定，開始受重力影響");
        }
        else
        {
            Debug.LogError($"Stone '{gameObject.name}': Rigidbody2D 不存在，無法掉落！");
        }

        // 通知玩家離開石頭範圍（因為石頭即將掉落）
        if (nearbyPlayer != null)
        {
            nearbyPlayer.SetNearStone(false, null);
            nearbyPlayer = null;
        }
    }

    /// <summary>
    /// 忽略目標 Tilemap 的碰撞，讓石頭可以穿透
    /// </summary>
    private void IgnoreTilemapCollision(Tilemap tilemap)
    {
        Collider2D stoneCollider = GetComponent<Collider2D>();
        if (stoneCollider == null)
        {
            Debug.LogError("Stone: 找不到 Collider2D，無法設定碰撞忽略！");
            return;
        }

        // 尋找 Tilemap 上的 TilemapCollider2D
        TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider != null)
        {
            Physics2D.IgnoreCollision(stoneCollider, tilemapCollider, true);
            Debug.Log($"🚫 已設定忽略 Tilemap 碰撞: {tilemap.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"⚠️ Tilemap '{tilemap.gameObject.name}' 沒有 TilemapCollider2D 組件");
        }
    }

#if UNITY_EDITOR
    // 顯示偵測範圍 (僅除錯用)
    private void OnDrawGizmosSelected()
    {
        if (showGizmo)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectRadius);
        }

        if (showImpactGizmo)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // 橘色半透明
            Gizmos.DrawWireSphere(transform.position, impactRadius);
        }
    }
#endif
}

