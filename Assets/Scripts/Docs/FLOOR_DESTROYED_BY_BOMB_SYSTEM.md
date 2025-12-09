# Floor_destroy_by_bomb 系統說明

## 概述
簡化的系統：**炸彈爆炸時，整個 "Floor_destroy_by_bomb" Tilemap 會自動消失**。

## 運作流程

### 1. 炸彈爆炸 (`Bomb_explode.cs`) ⭐ 核心功能
當炸彈爆炸時（`OnExplosionEnd()`）：
1. 尋找名為 **"Floor_destroy_by_bomb"** 的 GameObject
2. **直接讓整個 Tilemap 消失**（`SetActive(false)`）
3. 摧毀範圍內的石頭（呼叫 `Stone.Explode()`）

### 2. 石頭掉落 (`Stone.cs`)
當石頭被炸彈炸中後：
1. 開始掉落（從 Kinematic 變為 Dynamic）
2. 如果找到 "Floor_destroy_by_bomb" Tilemap，會預先忽略碰撞（但通常 Tilemap 已經消失了）
3. 在 `FixedUpdate` 中持續破壞石頭周圍的磚塊（如果 Tilemap 還存在的話）
4. 碰到實體地板時停止掉落

## 重要設定

### Tilemap 命名要求
- **必須命名為**：`Floor_destroy_by_bomb`
- 名稱必須完全匹配或包含這個字串

### Tilemap 組件要求
- `Tilemap` 組件（必需）
- `TilemapCollider2D` 組件（必需，用於碰撞檢測）

### 石頭參數設定
在石頭的 Inspector 中：
- **Target Tilemap Name** (string): 目標 Tilemap 名稱（預設 "Floor_destroy_by_bomb"）
- **Impact Radius** (float): 破壞範圍半徑（預設 1.5）

### 炸彈參數設定
在炸彈的 Inspector 中：
- **Explosion Radius** (float): 爆炸範圍半徑（預設 2.5）

## 除錯訊息

### 炸彈爆炸時
```
💥 Bomb_explode: 找到目標 Tilemap 'Floor_destroy_by_bomb'，讓它消失
✅ Floor_destroy_by_bomb 已消失！
💣 Bomb_explode: 摧毀石頭 'Stone'
```

### 石頭掉落時
```
💣 Stone 'Stone' 被炸彈炸中！開始掉落...
✅ Stone 找到目標 Tilemap: Floor_destroy_by_bomb
🚫 已設定忽略 Tilemap 碰撞: Floor_destroy_by_bomb
🪨 Stone 'Stone' 已解除固定，開始受重力影響
```

### 石頭破壞磚塊時
```
💥 Stone 在 'Floor_destroy_by_bomb' 中破壞了 X 個磚塊
```

### 石頭停止時
```
🪨 Stone 碰到實體地板 'Ground'，停止掉落
```

## 常見問題

### Q: 炸彈沒有讓 Tilemap 消失？
A: 檢查：
1. Tilemap 是否命名為 "Floor_destroy_by_bomb"（注意拼寫）
2. 檢查 Console 是否有 "找不到名為 'Floor_destroy_by_bomb' 的 Tilemap" 警告
3. 確認 GameObject 名稱完全匹配

### Q: 石頭沒有穿透 Tilemap？
A: 檢查：
1. Tilemap 是否有 `TilemapCollider2D` 組件
2. 檢查 Console 是否有 "已設定忽略 Tilemap 碰撞" 訊息
3. 確認石頭已被炸彈炸中（`hasExploded = true`）
4. 注意：如果炸彈已經讓 Tilemap 消失，石頭就不會碰到它了

### Q: 石頭沒有破壞磚塊？
A: 檢查：
1. 確認石頭正在掉落（`isFalling = true`）
2. 調整 `Impact Radius` 參數增加破壞範圍
3. 檢查 Console 是否有破壞磚塊的訊息
4. 注意：如果 Tilemap 已經被炸彈消失，就不會有磚塊可破壞

### Q: 想要改變目標 Tilemap 名稱？
A: 在石頭的 Inspector 中修改 `Target Tilemap Name` 參數

## 技術細節

### 炸彈破壞邏輯
```csharp
// 尋找並讓整個 Tilemap 消失
GameObject targetTilemapObj = GameObject.Find("Floor_destroy_by_bomb");
targetTilemapObj.SetActive(false);
```

### 石頭穿透邏輯
```csharp
// 預先忽略碰撞
Physics2D.IgnoreCollision(stoneCollider, tilemapCollider, true);

// 持續破壞（在 FixedUpdate 中）
if (isFalling && targetTilemap != null)
{
    DestroyTilesInRadius(targetTilemap, transform.position);
}
```

## 相關檔案
- `/Assets/Scripts/Items/Bomb_explode.cs` - 炸彈爆炸邏輯
- `/Assets/Scripts/Items/Stone.cs` - 石頭行為

