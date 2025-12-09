# 炸彈與石頭系統說明 (更新版)

## 概述
此系統允許玩家使用炸彈來摧毀石頭和周圍的 Autolayer 磚塊。石頭會穿透 Autolayer 並破壞磚塊，但會被實體地板阻擋。

## 功能

### 1. 炸彈爆炸效果 (`Bomb_explode.cs`)

當炸彈爆炸時（透過 Animation Event 呼叫 `OnExplosionEnd()`），會執行以下操作：

#### 爆炸範圍偵測
- **爆炸半徑**：預設 2.5 單位（可在 Inspector 中調整 `explosionRadius`）
- **視覺除錯**：在 Unity 編輯器中選擇炸彈時，會顯示橘色的爆炸範圍球體

#### 摧毀石頭
- 使用 `Physics2D.OverlapCircleAll()` 偵測爆炸範圍內的所有物件
- 檢查物件是否有 `Stone` 組件
- 呼叫 `Stone.Explode()` 來讓石頭開始掉落

#### 破壞 Tilemap 磚塊
- 尋找場景中所有的 `Tilemap` 組件
- 計算爆炸範圍內的所有磚塊位置
- 使用 `tilemap.SetTile(position, null)` 來移除磚塊
- 只破壞在爆炸半徑內的磚塊（使用距離檢查）

#### 舊功能保留
- 仍然會摧毀特定名稱的地板物件：
  - `Floor_destroy_by_bomb`
  - `grass1`
- 觸發 `HintTrigger.OnLinkedFloorDestroyed()` 事件

### 2. 石頭掉落與穿透系統 (`Stone.cs`) ⭐ 重新設計

#### 初始狀態
- 石頭預設為 **Kinematic** 狀態（固定，不受重力影響）
- 可在 Inspector 中設定 `startKinematic` 來控制是否初始固定

#### 被炸後的行為
當 `Explode()` 被呼叫時：
1. 將 Rigidbody2D 從 `Kinematic` 改為 `Dynamic`
2. 設定 `isFalling = true`（開始掉落狀態）
3. 石頭開始受重力影響並掉落
4. 給予一個小的初始向下速度（-0.5 單位）
5. 通知玩家離開石頭互動範圍

#### 智慧穿透系統 ⭐ 核心功能
石頭掉落時會區分兩種地板：

**AutoLayer（可穿透）**：
- 石頭會**穿透**並**破壞**這些磚塊
- 使用 `Physics2D.IgnoreCollision()` 忽略碰撞
- 在 `FixedUpdate` 中持續破壞經過的 AutoLayer 磚塊
- 識別方式：檢查 GameObject 名稱是否包含 "AutoLayer"、"Autolayer" 或 "autolayer"

**實體地板（不可穿透）**：
- 石頭會被阻擋並停止
- 設定 `isFalling = false` 停止破壞磚塊
- 如果是名為 `Floor_destroy_by_stone` 的地板，則會被摧毀

#### 持續破壞機制 ⭐ 新功能
- 在 `FixedUpdate` 中，只要 `isFalling = true`，就會持續檢查並破壞範圍內的 AutoLayer 磚塊
- 破壞範圍預設為 1.5 單位（可在 Inspector 調整 `impactRadius`）
- 使用圓形範圍偵測，破壞範圍內所有 AutoLayer 磚塊

#### AutoLayer 識別系統
可透過三種方式識別 AutoLayer：
1. 檢查物件名稱是否包含關鍵字（可在 Inspector 自訂 `autoLayerNames` 陣列）
2. 檢查父物件名稱是否包含關鍵字
3. 預設關鍵字：`AutoLayer`、`Autolayer`、`autolayer`

#### 防止重複爆炸
- 使用 `hasExploded` 旗標防止同一顆石頭被炸多次

## 使用方式

### 設定炸彈預製體
1. 確保炸彈預製體上有 `Bomb_explode` 組件
2. 在 Inspector 中調整以下參數：
   - **Explosion Radius**：爆炸範圍半徑（預設 2.5）
   - **Show Explosion Gizmo**：是否顯示除錯用的爆炸範圍
   - **Explosion Sound**：爆炸音效
   - **Explosion Volume**：音量（0.0 - 1.0）
   - **Sound Delay**：音效延遲時間

### 設定石頭物件
1. 石頭物件必須有以下組件：
   - `Stone` 腳本
   - `Rigidbody2D`（會自動要求）
   - `Collider2D`（會自動要求）

2. 在 Inspector 中設定：
   - **Detect Radius**：偵測玩家的範圍（預設 2.0）
   - **Show Gizmo**：顯示偵測範圍（除錯用，紅色線框）
   - **Start Kinematic**：是否初始固定（預設 true）
   - **Impact Radius**：經過時破壞磚塊的範圍（預設 1.5）⭐
   - **Show Impact Gizmo**：顯示破壞範圍（除錯用，橘色線框）⭐
   - **Auto Layer Names**：識別 AutoLayer 的關鍵字陣列 ⭐ 新增

3. Rigidbody2D 設定建議：
   - **Body Type**：會在程式中自動設定
   - **Gravity Scale**：1.0（或根據需要調整）
   - **Mass**：根據石頭大小調整
   - **Collision Detection**：建議設為 **Continuous**（避免高速穿透）

### 設定 Tilemap 和 AutoLayer
1. **AutoLayer 命名規則** ⭐ 重要：
   - 確保你的 AutoLayer GameObject 名稱包含 "AutoLayer"、"Autolayer" 或 "autolayer"
   - 例如：`AutoLayer_Ground`、`Autolayer_1`、`autolayer_walls`
   - 如果你的命名不同，可以在石頭的 Inspector 中修改 `Auto Layer Names` 陣列

2. **實體地板**：
   - 不要在名稱中包含 "AutoLayer" 關鍵字
   - 這些地板會阻擋石頭

3. 確保場景中有 `Tilemap` 組件
4. 使用 LDtk 或 Unity Tilemap 系統建立地圖

## 除錯資訊

### Console 訊息
系統會輸出詳細的 Debug Log：

**炸彈相關**：
- `💣 Bomb_explode: 摧毀石頭 'XXX'`
- `💥 Bomb_explode: 共摧毀了 X 顆石頭！`
- `💥 Bomb_explode: 破壞磚塊於 (x, y) (距離: X.XX)`
- `💥 Bomb_explode: 共破壞了 X 個磚塊！`

**石頭相關**：
- `💣 Stone 'XXX' 被炸彈炸中！開始掉落...`
- `🪨 Stone 'XXX' 已解除固定，開始受重力影響`
- `🪨 Stone 'XXX' 碰撞到：XXX`
- `🪨 Stone 碰到 AutoLayer 'XXX'，忽略碰撞繼續穿透` ⭐ 新增
- `🪨 Stone 碰到實體地板 'XXX'，停止掉落` ⭐ 新增
- `💥 Stone 共破壞了 X 個磚塊 (Tilemap: XXX)`
- `💥 Stone 破壞磚塊於 (x, y) (距離: X.XX)`

### 視覺除錯
- 在 Unity 編輯器中選擇炸彈：會顯示**橘色半透明球體**（爆炸範圍）
- 在 Unity 編輯器中選擇石頭：
  - **紅色線框球體**：玩家偵測範圍
  - **橘色線框球體**：破壞磚塊範圍 ⭐

## 常見問題

### Q: 石頭不會掉落？
A: 檢查以下項目：
1. 石頭是否有 Rigidbody2D 組件
2. Rigidbody2D 的 Gravity Scale 是否 > 0
3. 炸彈是否在石頭的爆炸範圍內
4. 檢查 Console 是否有 "Stone 被炸彈炸中" 的訊息

### Q: 石頭被 AutoLayer 阻擋了，無法穿透？ ⭐ 重要
A: 檢查以下項目：
1. **AutoLayer 的命名**：確保 GameObject 名稱包含 "AutoLayer"、"Autolayer" 或 "autolayer"
2. 如果命名不同，在石頭的 Inspector 中修改 `Auto Layer Names` 陣列，加入你的命名規則
3. 檢查 Console 是否有 "Stone 碰到 AutoLayer" 的訊息
4. 確認石頭已被炸過（`hasExploded = true`）

### Q: 石頭沒有破壞 AutoLayer 磚塊？ ⭐
A: 檢查以下項目：
1. 確認 AutoLayer 的 Tilemap 名稱包含關鍵字（同上）
2. 調整 `Impact Radius` 參數來增加破壞範圍
3. 檢查 Console 是否有破壞磚塊的訊息
4. 確認石頭處於掉落狀態（`isFalling = true`）

### Q: 石頭穿透了實體地板？
A: 檢查以下項目：
1. 確保實體地板的名稱**不包含** "AutoLayer" 關鍵字
2. 檢查石頭的 Collision Detection 設定（建議設為 Continuous）
3. 確認實體地板有 Collider2D 組件
4. 檢查 Physics2D 的 Layer Collision Matrix 設定

### Q: 爆炸範圍太小或太大？
A: 在炸彈預製體的 Inspector 中調整 `Explosion Radius` 參數

### Q: 石頭破壞範圍太小或太大？ ⭐
A: 在石頭物件的 Inspector 中調整 `Impact Radius` 參數

## 技術細節

### 爆炸偵測原理
```csharp
// 使用物理系統偵測圓形範圍內的所有碰撞體
Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
```

### Tilemap 破壞原理
```csharp
// 1. 將世界座標轉換為格子座標
Vector3Int centerCell = tilemap.WorldToCell(bombPosition);

// 2. 計算需要檢查的格子範圍
int radiusInCells = Mathf.CeilToInt(explosionRadius / tilemap.cellSize.x);

// 3. 移除格子中的磚塊
tilemap.SetTile(cellPosition, null);
```

### 石頭狀態轉換
```csharp
// 初始狀態：Kinematic（固定）
rb.bodyType = RigidbodyType2D.Kinematic;

// 爆炸後：Dynamic（可移動）+ 掉落狀態
rb.bodyType = RigidbodyType2D.Dynamic;
isFalling = true;

// 碰到實體地板：停止掉落
isFalling = false;
```

### 智慧穿透原理 ⭐ 核心邏輯
```csharp
// 1. 碰撞發生時，檢查是否為 AutoLayer
bool isAutoLayer = IsAutoLayer(collision.gameObject);

// 2. 如果是 AutoLayer，忽略碰撞
if (isAutoLayer)
{
    Physics2D.IgnoreCollision(stoneCollider, autoLayerCollider, true);
    return; // 不停止掉落
}

// 3. 如果不是 AutoLayer，停止掉落
isFalling = false;
```

### 持續破壞機制 ⭐
```csharp
// 在 FixedUpdate 中持續執行
void FixedUpdate()
{
    if (isFalling)
    {
        // 尋找所有 Tilemap
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>();
        
        // 只破壞 AutoLayer 的磚塊
        foreach (var tilemap in tilemaps)
        {
            if (IsAutoLayer(tilemap.gameObject))
            {
                DestroyTilesInRadius(tilemap, transform.position);
            }
        }
    }
}
```

### AutoLayer 識別原理
```csharp
private bool IsAutoLayer(GameObject obj)
{
    // 檢查物件名稱
    foreach (string layerName in autoLayerNames)
    {
        if (obj.name.Contains(layerName))
            return true;
    }
    
    // 檢查父物件名稱
    if (obj.transform.parent != null)
    {
        foreach (string layerName in autoLayerNames)
        {
            if (obj.transform.parent.name.Contains(layerName))
                return true;
        }
    }
    
    return false;
}
```

## 工作流程示例

### 完整流程
1. **玩家使用炸彈** 
   - 炸彈爆炸，破壞範圍內的第一層磚塊
   - 呼叫 `Stone.Explode()`

2. **石頭開始掉落**
   - 從 Kinematic 變為 Dynamic
   - 設定 `isFalling = true`

3. **持續破壞 AutoLayer**
   - 每個物理幀（FixedUpdate）檢查石頭周圍的 AutoLayer
   - 破壞範圍內的磚塊

4. **碰到 AutoLayer**
   - 識別為可穿透圖層
   - 使用 `Physics2D.IgnoreCollision()` 忽略碰撞
   - 石頭繼續掉落

5. **碰到實體地板**
   - 識別為不可穿透
   - 設定 `isFalling = false`
   - 石頭停止並停留在地板上

## 相關檔案
- `/Assets/Scripts/Items/Bomb_explode.cs` - 炸彈爆炸邏輯
- `/Assets/Scripts/Items/Stone.cs` - 石頭行為（穿透與破壞）
- `/Assets/Scripts/Player/PlayerUseBomb.cs` - 玩家使用炸彈
