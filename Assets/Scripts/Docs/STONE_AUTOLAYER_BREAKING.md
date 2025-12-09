# 石頭破壞 AutoLayer 系統 - 精確控制版

## 核心改進 🎯

石頭現在會**精確地破壞指定數量的 AutoLayer**（預設 3 層），然後停止。

## 主要功能

### 1. 精確計數系統
- 新增 `autoLayersToBreak` 參數（預設 3）
- 追蹤已破壞的 AutoLayer 數量
- 達到目標後自動停止破壞

### 2. 預先忽略碰撞
- 石頭開始掉落時，預先設定忽略所有 AutoLayer 的碰撞
- 使用 `Physics2D.IgnoreCollision()` 確保石頭可以穿透
- 只在實體地板停下

### 3. 防止重複處理
- 使用 `HashSet<Tilemap>` 追蹤已處理的 Tilemap
- 每個 Tilemap 只計算一次（算作一層）
- 確保不會重複破壞同一層

## 運作流程

```
炸彈爆炸
   ↓
石頭.Explode() 被呼叫
   ↓
1. 設定 isFalling = true
2. 重置 autoLayersBroken = 0
3. 清空 processedTilemaps
4. 預先忽略所有 AutoLayer 碰撞 ← 關鍵
   ↓
石頭開始掉落（穿透所有 AutoLayer）
   ↓
每個 FixedUpdate：
   ├─ 檢查 autoLayersBroken < 3?
   │  ↓ YES
   │  尋找未處理的 AutoLayer Tilemap
   │  ↓
   │  破壞範圍內的磚塊
   │  ↓
   │  如果有磚塊被破壞：
   │     - 加入 processedTilemaps
   │     - autoLayersBroken++
   │     - 記錄："✅ 已破壞第 X 層"
   │  ↓
   │  達到 3 層? → 停止破壞
   │
   └─ NO → 不再破壞
   ↓
碰到實體地板
   ↓
停止掉落 (isFalling = false)
   ↓
顯示最終統計
```

## Inspector 設定

### Stone 組件參數

**Collision Settings**:
- `Impact Radius` (float): 破壞範圍半徑（預設 1.5）
- `Show Impact Gizmo` (bool): 顯示破壞範圍視覺化
- `Auto Layer Names` (string[]): AutoLayer 識別關鍵字
  - 預設：`["AutoLayer", "Autolayer", "autolayer"]`
- `Auto Layers To Break` (int): **要破壞的 AutoLayer 數量（預設 3）** ⭐ 新增

## 重要設定要求

### 1. AutoLayer 必須有 TilemapCollider2D
確保你的 AutoLayer GameObject 上有：
- `Tilemap` 組件
- `TilemapCollider2D` 組件 ← **必需**

### 2. AutoLayer 命名規則
GameObject 名稱必須包含關鍵字之一：
- `AutoLayer`
- `Autolayer`  
- `autolayer`

例如：
- ✅ `AutoLayer_Ground`
- ✅ `Autolayer_1`
- ✅ `Background_autolayer`
- ❌ `Ground_Layer` （不會被識別）

### 3. 實體地板
- 不要在名稱中包含 AutoLayer 關鍵字
- 需要有 Collider2D
- 會阻擋石頭

## 除錯訊息

### 石頭爆炸時
```
💣 Stone 'Stone' 被炸彈炸中！開始掉落... (目標破壞 3 層)
🪨 Stone 'Stone' 已解除固定，開始受重力影響
🚫 已設定忽略 AutoLayer 碰撞: AutoLayer_1
🚫 已設定忽略 AutoLayer 碰撞: AutoLayer_2
🚫 已設定忽略 AutoLayer 碰撞: AutoLayer_3
🎯 共設定忽略 3 個 AutoLayer 的碰撞
```

### 破壞 AutoLayer 時
```
💥 Stone 在 'AutoLayer_1' 中破壞了 15 個磚塊
✅ 已破壞第 1 層 AutoLayer: AutoLayer_1
💥 Stone 在 'AutoLayer_2' 中破壞了 12 個磚塊
✅ 已破壞第 2 層 AutoLayer: AutoLayer_2
💥 Stone 在 'AutoLayer_3' 中破壞了 18 個磚塊
✅ 已破壞第 3 層 AutoLayer: AutoLayer_3
🎯 已達到目標：破壞了 3 層 AutoLayer
```

### 碰到地板時
```
🪨 Stone 'Stone' 碰撞到：Ground
🪨 Stone 碰到地板 'Ground'，停止掉落
📊 Stone 最終統計：破壞了 3 層 AutoLayer
```

## 常見問題

### Q: 石頭破壞了超過 3 層？
A: 檢查是否有多個 Tilemap 的磚塊重疊在同一個位置。每個 Tilemap 只會計算一次。

### Q: 石頭只破壞了 1 或 2 層？
A: 
1. 檢查是否有足夠的 AutoLayer（至少 3 層）
2. 確認所有 AutoLayer 的名稱都包含關鍵字
3. 增加 `Impact Radius` 參數

### Q: 石頭被 AutoLayer 阻擋？
A:
1. 確認 AutoLayer 上有 `TilemapCollider2D` 組件
2. 檢查 Console 是否有 "已設定忽略 AutoLayer 碰撞" 訊息
3. 確認命名符合規則

### Q: 想要破壞更多或更少層？
A: 在石頭的 Inspector 中修改 `Auto Layers To Break` 參數

## 技術細節

### 預先忽略碰撞
```csharp
private void IgnoreAutoLayerCollisions()
{
    Collider2D stoneCollider = GetComponent<Collider2D>();
    TilemapCollider2D[] allTilemapColliders = FindObjectsByType<TilemapCollider2D>();
    
    foreach (var tilemapCollider in allTilemapColliders)
    {
        if (IsAutoLayer(tilemapCollider.gameObject))
        {
            Physics2D.IgnoreCollision(stoneCollider, tilemapCollider, true);
        }
    }
}
```

### 精確計數
```csharp
// 每個 Tilemap 只處理一次
if (processedTilemaps.Contains(tilemap))
    continue;

// 如果有破壞磚塊，計為一層
if (destroyed > 0)
{
    processedTilemaps.Add(tilemap);
    autoLayersBroken++;
    
    // 達到目標即停止
    if (autoLayersBroken >= autoLayersToBreak)
        return;
}
```

## 相關檔案
- `/Assets/Scripts/Items/Stone.cs` - 石頭行為（精確破壞系統）
- `/Assets/Scripts/Items/Bomb_explode.cs` - 炸彈爆炸邏輯

