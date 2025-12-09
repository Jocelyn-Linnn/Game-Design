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
    [Tooltip("石頭經過時破壞磚塊的範圍半徑")]
    [SerializeField] private float impactRadius = 1.5f;

    [Tooltip("是否顯示碰撞破壞範圍 (僅用於除錯)")]
    [SerializeField] private bool showImpactGizmo = true;

    [Tooltip("Autolayer 的 GameObject 名稱（用於識別可穿透的圖層）")]
    [SerializeField] private string[] autoLayerNames = { "AutoLayer", "Autolayer", "autolayer" };

    private PlayerUseBomb nearbyPlayer;   // 記錄可互動的玩家
    private Rigidbody2D rb;
    private bool hasExploded = false;
    private bool isFalling = false;  // 是否正在掉落中

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
        // 如果石頭正在掉落，持續檢查並破壞 autolayer 磚塊
        if (isFalling)
        {
            DestroyAutoLayersAtPosition();
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

        // 檢查是否碰到 AutoLayer（可穿透的圖層）
        bool isAutoLayer = IsAutoLayer(collision.gameObject);
        
        if (isAutoLayer)
        {
            Debug.Log($"🪨 Stone 碰到 AutoLayer '{collision.gameObject.name}'，忽略碰撞繼續穿透");
            // AutoLayer 不會阻擋石頭，忽略此碰撞
            // 磚塊破壞在 FixedUpdate 中持續處理
            
            // 忽略這個 collider 的碰撞
            Collider2D stoneCollider = GetComponent<Collider2D>();
            Collider2D autoLayerCollider = collision.collider;
            if (stoneCollider != null && autoLayerCollider != null)
            {
                Physics2D.IgnoreCollision(stoneCollider, autoLayerCollider, true);
            }
            
            return;
        }

        // 1. 處理特定名稱的地板物件（實體地板）
        if (collision.gameObject.name == "Floor_destroy_by_stone")
        {
            collision.gameObject.SetActive(false);
            Debug.Log("💥 Floor_destroy_by_stone 被石頭砸到後消失！");
        }

        // 2. 碰到實體地板，停止掉落
        Debug.Log($"🪨 Stone 碰到實體地板 '{collision.gameObject.name}'，停止掉落");
        isFalling = false;
    }

    /// <summary>
    /// 檢查物件是否是 AutoLayer（可穿透的圖層）
    /// </summary>
    private bool IsAutoLayer(GameObject obj)
    {
        // 檢查物件名稱是否包含 AutoLayer 關鍵字
        foreach (string layerName in autoLayerNames)
        {
            if (obj.name.Contains(layerName))
            {
                return true;
            }
        }

        // 檢查父物件名稱
        Transform parent = obj.transform.parent;
        if (parent != null)
        {
            foreach (string layerName in autoLayerNames)
            {
                if (parent.name.Contains(layerName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 在石頭當前位置持續破壞 AutoLayer 磚塊
    /// </summary>
    private void DestroyAutoLayersAtPosition()
    {
        // 尋找場景中所有的 Tilemap
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        
        if (tilemaps.Length == 0)
        {
            return;
        }

        Vector3 stonePosition = transform.position;

        foreach (var tilemap in tilemaps)
        {
            // 只破壞 AutoLayer 的磚塊
            if (IsAutoLayer(tilemap.gameObject))
            {
                DestroyTilesInRadius(tilemap, stonePosition);
            }
        }
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
                        Debug.Log($"💥 Stone 破壞磚塊於 {cellPosition} (距離: {distance:F2})");
                    }
                }
            }
        }

        if (tilesDestroyed > 0)
        {
            Debug.Log($"💥 Stone 共破壞了 {tilesDestroyed} 個磚塊 (Tilemap: {tilemap.gameObject.name})");
        }
        else
        {
            Debug.Log($"⚠️ Stone 碰撞範圍內沒有磚塊可破壞 (Tilemap: {tilemap.gameObject.name})");
        }
    }

    /// <summary>
    /// 破壞碰撞點附近所有 Tilemap 的磚塊
    /// </summary>
    private void DestroyNearbyTilemaps(Vector3 position)
    {
        // 這個方法不再需要，因為在 FixedUpdate 中持續處理
        // 保留以防需要
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

