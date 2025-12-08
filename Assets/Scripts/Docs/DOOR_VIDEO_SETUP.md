# 門開啟白光與影片播放系統設置指南

本指南說明如何設置門開啟後的白光閃爍效果和影片播放功能。

## 功能概述

當玩家按下 Z 鍵用鑰匙開門後，系統會：
1. 播放門開啟動畫
2. 畫面顯示滿版白光閃爍（持續 1 秒）
3. 播放指定的影片

## 設置步驟

### 步驟 1: 設置白光閃爍效果

1. **在 Canvas 上創建白光 UI**
   - 在場景的 Canvas 下，右鍵選擇 `UI > Image`
   - 重新命名為 `WhiteFlashImage`

2. **設置 Image 屬性**
   - 將 RectTransform 設置為滿版：
     - Anchor Presets: 選擇「Stretch」（右下角的全螢幕選項）
     - Left: 0, Right: 0, Top: 0, Bottom: 0
   - Color: 白色 (R:255, G:255, B:255, A:0)
     - **重要：Alpha 必須設為 0**
   - Raycast Target: 取消勾選（避免阻擋其他 UI 互動）

3. **添加 WhiteFlashEffect 腳本**
   - 選擇 `WhiteFlashImage` 物件
   - 在 Inspector 中點擊「Add Component」
   - 搜尋並添加 `WhiteFlashEffect` 腳本

### 步驟 2: 設置影片播放器

1. **創建 VideoPlayer 物件**
   - 在 Canvas 下，右鍵選擇 `Create Empty`
   - 重新命名為 `VideoPlayer`

2. **添加 VideoPlayer 組件**
   - 選擇 `VideoPlayer` 物件
   - 點擊「Add Component」
   - 搜尋並添加 Unity 內建的 `Video Player` 組件

3. **創建影片顯示 UI**
   - 在 Canvas 下，右鍵選擇 `UI > Raw Image`
   - 重新命名為 `VideoDisplay`

4. **設置 RawImage 屬性**
   - 將 RectTransform 設置為滿版：
     - Anchor Presets: 選擇「Stretch」
     - Left: 0, Right: 0, Top: 0, Bottom: 0
   - Color: 白色 (R:255, G:255, B:255, A:255)
   - Raycast Target: 可以勾選（如果想讓影片播放時阻擋其他互動）

5. **添加 VideoPlayerController 腳本**
   - 選擇 `VideoPlayer` 物件
   - 點擊「Add Component」
   - 搜尋並添加 `VideoPlayerController` 腳本

6. **設置 VideoPlayerController 參數**
   - Video Clip: 拖入你的影片檔案（例如：`漫畫轉動畫：兄妹相擁.mov`）
   - Video Display: 拖入剛才創建的 `VideoDisplay` RawImage
   - Hide UI While Playing: 勾選（可選）
   - Auto Hide After Play: 勾選（建議）

### 步驟 3: 設置門物件

1. **選擇你的門物件**
   - 在 Hierarchy 中找到你的門物件（應該已經有 `PhysicalDoor` 腳本）

2. **設置 PhysicalDoor 參數**

   **基本設置（如果還沒設置）：**
   - Required Key Tag: 設置所需的鑰匙標籤（例如：`key1`）
   - Door Opened Sprite: 門開啟時的 Sprite（可選）
   - Open Transparency: 門開啟時的透明度（如果不使用 Sprite）
   - Animation Duration: 門開啟動畫的持續時間

   **新增的白光與影片設置：**
   - Enable White Flash: 勾選（啟用白光閃爍）
   - Flash Duration: 1（白光持續 1 秒）
   - Enable Video Playback: 勾選（啟用影片播放）
   - Video Clip: 拖入你的影片檔案（例如：`Assets/tileset/3 Animated objects/漫畫轉動畫：兄妹相擁.mov`）

## UI 層級順序建議

為了確保效果正確顯示，建議的 Canvas 子物件順序（從上到下）：

```
Canvas
├── [其他 UI 元素]
├── VideoDisplay (Raw Image)
└── WhiteFlashImage (Image) ← 應該在最後，確保在最上層
```

**提示：** 在 Hierarchy 中，最下面的物件會顯示在最上層。

## 測試步驟

1. **確認設置**
   - 白光 Image 的 Alpha 為 0
   - 影片顯示 RawImage 初始是隱藏的（VideoPlayerController 會自動處理）

2. **開始遊戲**
   - 讓玩家角色靠近門
   - 確保玩家有正確的鑰匙

3. **按 Z 鍵開門**
   - 應該看到門開啟動畫
   - 接著畫面變白（1 秒）
   - 然後播放影片
   - 影片播放完畢後自動隱藏

## 自訂調整

### 調整白光效果

在 `WhiteFlashEffect.PlayFlash()` 中可以調整：
- `duration`: 白光維持的時間
- `fadeInTime`: 淡入時間（預設 0.1 秒）
- `fadeOutTime`: 淡出時間（預設 0.3 秒）

目前在 `PhysicalDoor` 中的設置：
```csharp
flashEffect.PlayFlash(flashDuration, 0.1f, 0.3f, callback);
```

### 只要白光不要影片

在門物件的 Inspector 中：
- Enable White Flash: 勾選
- Enable Video Playback: 取消勾選

### 只要影片不要白光

在門物件的 Inspector 中：
- Enable White Flash: 取消勾選
- Enable Video Playback: 勾選

### 不同的門播放不同的影片

每個門物件可以設置不同的影片檔案，只需在各自的 `PhysicalDoor` 組件中設置不同的 `Video Clip` 即可。

## 常見問題

### Q: 白光沒有顯示？
A: 確認以下項目：
1. Canvas 中有 WhiteFlashImage 物件
2. 該物件有 WhiteFlashEffect 腳本
3. Image 的顏色是白色（R:255, G:255, B:255）
4. 初始 Alpha 為 0

### Q: 影片沒有播放？
A: 確認以下項目：
1. VideoPlayerController 的 Video Clip 欄位有設置影片
2. VideoPlayerController 的 Video Display 欄位有連結到 RawImage
3. 影片檔案格式是 Unity 支援的格式（.mov, .mp4 等）
4. 門物件的 PhysicalDoor 組件中也有設置 Video Clip

### Q: 影片播放完後沒有隱藏？
A: 確認 VideoPlayerController 的 `Auto Hide After Play` 有勾選。

### Q: 白光或影片顯示在其他 UI 下面？
A: 確認 Canvas 中的 UI 層級順序，WhiteFlashImage 應該在最底部（最上層顯示）。

## 腳本說明

### WhiteFlashEffect.cs
- 管理白光閃爍效果
- 單例模式，全域唯一
- 提供 `PlayFlash()` 方法播放白光

### VideoPlayerController.cs
- 管理影片播放
- 單例模式，全域唯一
- 提供 `PlayVideo()` 方法播放影片
- 自動管理 RenderTexture

### PhysicalDoor.cs（已修改）
- 整合白光和影片播放功能
- 在門開啟後自動觸發效果序列

## 進階使用

如果你想在其他地方也使用白光或影片播放功能，可以直接調用：

```csharp
// 播放白光
WhiteFlashEffect flashEffect = WhiteFlashEffect.GetInstance();
flashEffect.PlayFlash(1f, 0.1f, 0.3f, () => {
    Debug.Log("白光完成！");
});

// 播放影片
VideoPlayerController videoController = VideoPlayerController.GetInstance();
videoController.PlayVideo(yourVideoClip, () => {
    Debug.Log("影片完成！");
});
```

## 注意事項

1. **影片檔案大小**：請注意影片檔案的大小，過大的影片可能會影響遊戲效能和載入時間。

2. **影片格式**：建議使用 Unity 原生支援的格式（.mp4 或 .mov），並確保編碼格式相容。

3. **Canvas Render Mode**：系統支援所有 Canvas Render Mode（Screen Space - Overlay, Screen Space - Camera, World Space）。

4. **多場景使用**：如果你的遊戲有多個場景，每個場景都需要設置 WhiteFlashEffect 和 VideoPlayerController。

5. **效能考量**：白光效果使用 UI Image，效能消耗很小。影片播放會使用較多資源，請在目標平台上測試效能。

