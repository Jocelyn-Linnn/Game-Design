using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System;

/// <summary>
/// 影片播放控制器
/// 使用方法：
/// 1. 在 Canvas 上創建一個 RawImage 用於顯示影片
/// 2. 創建一個 GameObject 並附加此腳本
/// 3. 在 Inspector 中設置影片檔案和 RawImage 引用
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class VideoPlayerController : MonoBehaviour
{
    private static VideoPlayerController instance;
    
    [Header("影片設置")]
    [Tooltip("要播放的影片檔案（用於 Editor 和其他平台）")]
    public VideoClip videoClip;
    
    [Tooltip("影片 URL（用於 WebGL 平台，例如：StreamingAssets/video.mp4）")]
    public string videoUrl;
    
    [Tooltip("是否優先使用 URL（WebGL 必須使用 URL）")]
    public bool useUrlForWebGL = true;
    
    [Header("UI 設置")]
    [Tooltip("用於顯示影片的 RawImage")]
    public RawImage videoDisplay;
    
    [Tooltip("影片播放時是否隱藏 UI")]
    public bool hideUIWhilePlaying = true;
    
    [Tooltip("播放完畢後是否自動隱藏影片")]
    public bool autoHideAfterPlay = true;
    
    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private bool isPlaying = false;
    private System.Action onVideoEndCallback;
    private bool isUsingUrl = false;
    
    // Default video dimensions for URL-based playback (when dimensions are unknown)
    private const int DEFAULT_VIDEO_WIDTH = 1920;
    private const int DEFAULT_VIDEO_HEIGHT = 1080;
    
    void Awake()
    {
        // 設置單例
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("VideoPlayerController: 場景中已存在另一個 VideoPlayerController 實例！");
            Destroy(gameObject);
            return;
        }
        
        // 獲取 VideoPlayer 組件
        videoPlayer = GetComponent<VideoPlayer>();
        
        if (videoPlayer == null)
        {
            Debug.LogError("VideoPlayerController: 找不到 VideoPlayer 組件！");
            return;
        }
        
        // 初始化 VideoPlayer 設置
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.skipOnDrop = true; // 允許跳幀以保持同步
        videoPlayer.waitForFirstFrame = true; // 等待第一幀準備好
        
        // 在 WebGL 平台上，必須使用 URL 方式
        #if UNITY_WEBGL && !UNITY_EDITOR
        useUrlForWebGL = true;
        #endif
        
        // 註冊影片結束事件
        videoPlayer.loopPointReached += OnVideoEnd;
        
        // 初始化時隱藏影片顯示
        if (videoDisplay != null)
        {
            videoDisplay.gameObject.SetActive(false);
        }
        
        Debug.Log("VideoPlayerController: 初始化完成");
    }
    
    void Start()
    {
        // 在 WebGL 平台上，使用 URL 方式
        if (useUrlForWebGL && !string.IsNullOrEmpty(videoUrl))
        {
            // URL 方式不需要在 Start 中預先載入
            // 將在 PlayVideo 時設置
        }
        // 其他平台，如果設置了影片檔案，預先載入
        else if (videoClip != null)
        {
            videoPlayer.clip = videoClip;
            PrepareRenderTexture();
        }
    }
    
    /// <summary>
    /// 獲取單例實例
    /// </summary>
    public static VideoPlayerController GetInstance()
    {
        return instance;
    }
    
    /// <summary>
    /// 播放影片（使用 VideoClip）
    /// </summary>
    /// <param name="clip">要播放的影片（可選，如果為 null 則使用已設置的影片）</param>
    /// <param name="onComplete">播放完成後的回調函數</param>
    public void PlayVideo(VideoClip clip = null, System.Action onComplete = null)
    {
        if (videoPlayer == null)
        {
            Debug.LogError("VideoPlayerController: VideoPlayer 組件未找到！");
            onComplete?.Invoke();
            return;
        }
        
        // 在 WebGL 平台上，如果設置了 URL，使用 URL 方式
        if (useUrlForWebGL && !string.IsNullOrEmpty(videoUrl))
        {
            PlayVideoByUrl(videoUrl, onComplete);
            return;
        }
        
        // 如果提供了新的影片檔案，使用它
        if (clip != null)
        {
            videoClip = clip;
        }
        
        // 檢查是否有影片可播放
        if (videoClip == null)
        {
            Debug.LogError("VideoPlayerController: 沒有設置影片檔案！請設置 videoClip 或 videoUrl。");
            onComplete?.Invoke();
            return;
        }
        
        // 設置 VideoClip
        isUsingUrl = false;
        videoPlayer.clip = videoClip;
        videoPlayer.url = null; // 清除 URL
        
        // 保存回調
        onVideoEndCallback = onComplete;
        
        // 準備 RenderTexture
        PrepareRenderTexture();
        
        // 顯示影片畫面
        if (videoDisplay != null)
        {
            videoDisplay.gameObject.SetActive(true);
            videoDisplay.transform.SetAsLastSibling(); // 確保在最上層
        }
        
        // 開始播放影片
        isPlaying = true;
        StartCoroutine(PrepareAndPlayVideo());
        
        Debug.Log($"VideoPlayerController: 準備播放影片 '{videoClip.name}'");
    }
    
    /// <summary>
    /// 播放影片（使用 URL，適用於 WebGL）
    /// </summary>
    /// <param name="url">影片的 URL 路徑（相對於 StreamingAssets 或完整 URL）</param>
    /// <param name="onComplete">播放完成後的回調函數</param>
    public void PlayVideoByUrl(string url, System.Action onComplete = null)
    {
        if (videoPlayer == null)
        {
            Debug.LogError("VideoPlayerController: VideoPlayer 組件未找到！");
            onComplete?.Invoke();
            return;
        }
        
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("VideoPlayerController: URL 為空！");
            onComplete?.Invoke();
            return;
        }
        
        // 構建完整的 URL 路徑
        // 如果是相對路徑，假設在 StreamingAssets 資料夾中
        string fullUrl = url;
        if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("file://"))
        {
            // 構建完整路徑
            // 使用 Path.Combine 來組合路徑，然後使用 Path.GetFullPath 來獲取絕對路徑
            string filePath = Path.Combine(Application.streamingAssetsPath, url);
            
            // 轉換為絕對路徑（處理相對路徑）
            try
            {
                filePath = Path.GetFullPath(filePath);
            }
            catch
            {
                // 如果 GetFullPath 失敗，使用原始路徑
                Debug.LogWarning($"VideoPlayerController: 無法獲取絕對路徑，使用原始路徑: {filePath}");
            }
            
            #if UNITY_WEBGL && !UNITY_EDITOR
            // 在 WebGL 中，StreamingAssets 的內容會被複製到 build 的根目錄下的 StreamingAssets 資料夾
            // Application.streamingAssetsPath 在 WebGL 中返回 "StreamingAssets"
            // 需要構建相對路徑，例如 "StreamingAssets/video.mp4"
            // 注意：在 WebGL 中，路徑需要進行 URL 編碼以處理中文字元
            string relativePath = Path.Combine(Application.streamingAssetsPath, url).Replace("\\", "/");
            
            // 對路徑進行 URL 編碼
            // 需要分別編碼每個路徑段，保留路徑分隔符
            string[] pathSegments = relativePath.Split('/');
            System.Text.StringBuilder encodedPath = new System.Text.StringBuilder();
            
            for (int i = 0; i < pathSegments.Length; i++)
            {
                if (!string.IsNullOrEmpty(pathSegments[i]))
                {
                    if (i > 0)
                    {
                        encodedPath.Append("/");
                    }
                    // 對每個路徑段進行 URL 編碼
                    encodedPath.Append(UnityWebRequest.EscapeURL(pathSegments[i]));
                }
            }
            
            fullUrl = encodedPath.ToString();
            #elif UNITY_EDITOR
            // 在 Editor 中，VideoPlayer 可以直接使用絕對路徑，不需要 file:// 前綴
            // 或者使用 file:// 前綴，但需要正確格式化
            filePath = filePath.Replace("\\", "/");
            
            // 確保路徑是絕對路徑
            if (!Path.IsPathRooted(filePath))
            {
                Debug.LogError($"VideoPlayerController: 路徑不是絕對路徑: {filePath}");
            }
            
            // 在 Editor 中，嘗試使用 file:// URL 格式
            try
            {
                // 對於 Windows 路徑（C:/path），需要特殊處理
                if (filePath.Length > 2 && filePath[1] == ':')
                {
                    // Windows 路徑：file:///C:/path/to/file
                    Uri uri = new Uri("file:///" + filePath);
                    fullUrl = uri.AbsoluteUri;
                }
                else if (filePath.StartsWith("/"))
                {
                    // Unix/Mac 路徑：file:///Users/path/to/file
                    Uri uri = new Uri("file://" + filePath);
                    fullUrl = uri.AbsoluteUri;
                }
                else
                {
                    // 如果路徑格式不正確，直接使用原始路徑
                    fullUrl = filePath;
                }
            }
            catch (UriFormatException e)
            {
                Debug.LogWarning($"VideoPlayerController: Uri 構建失敗，使用絕對路徑。錯誤: {e.Message}");
                // 如果 Uri 構建失敗，直接使用絕對路徑（某些情況下 VideoPlayer 可能接受）
                fullUrl = filePath;
            }
            #elif UNITY_STANDALONE
            // 在 Editor 和 Standalone 平台，需要使用 file:// 前綴
            // 使用 Uri 類別來正確構建 file:// URL
            filePath = filePath.Replace("\\", "/");
            
            // 確保路徑是絕對路徑格式
            // 移除可能存在的 file:// 前綴（如果 Application.streamingAssetsPath 已經包含）
            if (filePath.StartsWith("file://"))
            {
                filePath = filePath.Substring(7); // 移除 "file://"
            }
            else if (filePath.StartsWith("file:"))
            {
                filePath = filePath.Substring(5); // 移除 "file:"
            }
            
            // 使用 Uri 類別來正確構建 file:// URL
            // Uri 會自動處理 URL 編碼和路徑格式
            try
            {
                // 對於 Windows 路徑（C:/path），需要特殊處理
                if (filePath.Length > 2 && filePath[1] == ':')
                {
                    // Windows 路徑：file:///C:/path/to/file
                    Uri uri = new Uri("file:///" + filePath);
                    fullUrl = uri.AbsoluteUri;
                }
                else
                {
                    // Unix/Mac 路徑：file:///Users/path/to/file
                    // 注意：Mac 路徑以 / 開頭，所以 file:// 後面需要三個斜線
                    Uri uri = new Uri("file://" + filePath);
                    fullUrl = uri.AbsoluteUri;
                }
            }
            catch (UriFormatException e)
            {
                Debug.LogWarning($"VideoPlayerController: Uri 構建失敗，使用手動構建。錯誤: {e.Message}");
                // 如果 Uri 構建失敗，使用手動方式
                filePath = filePath.Replace("\\", "/");
                if (filePath.StartsWith("/"))
                {
                    fullUrl = "file://" + filePath;
                }
                else
                {
                    fullUrl = "file:///" + filePath;
                }
                
                // 手動進行 URL 編碼
                string[] segments = fullUrl.Substring(7).Split('/');
                System.Text.StringBuilder sb = new System.Text.StringBuilder("file://");
                foreach (string segment in segments)
                {
                    if (!string.IsNullOrEmpty(segment))
                    {
                        sb.Append("/");
                        sb.Append(UnityWebRequest.EscapeURL(segment));
                    }
                }
                fullUrl = sb.ToString();
            }
            #else
            // 其他平台
            fullUrl = filePath.Replace("\\", "/");
            #endif
        }
        
        // 設置 URL
        isUsingUrl = true;
        videoPlayer.url = fullUrl;
        videoPlayer.clip = null; // 清除 VideoClip
        
        // 保存回調
        onVideoEndCallback = onComplete;
        
        // 準備 RenderTexture（使用預設尺寸，因為 URL 模式下無法提前知道尺寸）
        PrepareRenderTextureForUrl();
        
        // 顯示影片畫面
        if (videoDisplay != null)
        {
            videoDisplay.gameObject.SetActive(true);
            videoDisplay.transform.SetAsLastSibling(); // 確保在最上層
        }
        
        // 開始播放影片
        isPlaying = true;
        StartCoroutine(PrepareAndPlayVideo());
        
        Debug.Log($"VideoPlayerController: ===== 影片播放資訊 =====");
        Debug.Log($"VideoPlayerController: 原始 URL 輸入: '{url}'");
        Debug.Log($"VideoPlayerController: Application.streamingAssetsPath: '{Application.streamingAssetsPath}'");
        Debug.Log($"VideoPlayerController: 構建後的完整 URL: '{fullUrl}'");
        Debug.Log($"VideoPlayerController: 是否使用 URL 模式: {isUsingUrl}");
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log($"VideoPlayerController: [WebGL] 相對路徑: '{fullUrl}'");
        Debug.Log($"VideoPlayerController: [WebGL] 請確認 StreamingAssets 資料夾已正確部署到 build 目錄");
        #endif
        
        // 檢查檔案是否存在（僅在 Editor 中）
        #if UNITY_EDITOR
        // 獲取實際的檔案路徑（不包含 file:// 前綴）
        string localPath = fullUrl;
        if (localPath.StartsWith("file://"))
        {
            localPath = localPath.Substring(7);
        }
        else if (localPath.StartsWith("file:"))
        {
            localPath = localPath.Substring(5);
        }
        
        // 解碼 URL 編碼的路徑
        try
        {
            localPath = Uri.UnescapeDataString(localPath);
        }
        catch { }
        
        if (File.Exists(localPath))
        {
            Debug.Log($"VideoPlayerController: ✓ 檔案存在於: '{localPath}'");
        }
        else
        {
            Debug.LogWarning($"VideoPlayerController: ⚠ 檔案不存在於: '{localPath}'");
            // 嘗試另一個路徑（直接使用 Application.streamingAssetsPath）
            string altPath = Path.Combine(Application.streamingAssetsPath, url);
            if (File.Exists(altPath))
            {
                Debug.LogWarning($"VideoPlayerController: 但檔案存在於替代路徑: '{altPath}'");
            }
        }
        #endif
        Debug.Log($"VideoPlayerController: ==========================");
    }
    
    /// <summary>
    /// 準備並播放影片的協程
    /// </summary>
    private IEnumerator PrepareAndPlayVideo()
    {
        // 先準備影片
        videoPlayer.Prepare();
        
        // 等待影片準備完成
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }
        
        // 如果是 URL 模式，嘗試獲取實際影片尺寸
        if (isUsingUrl && videoPlayer.targetTexture != null)
        {
            // 等待幾幀讓 VideoPlayer 更新 texture
            yield return null;
            yield return null;
            
            // 嘗試從 targetTexture 獲取尺寸（如果 VideoPlayer 已經更新了它）
            // 注意：在 URL 模式下，VideoPlayer 可能會自動調整 targetTexture 的尺寸
            // 但這取決於 VideoPlayer 的實作，所以我們先使用預設尺寸
            // 如果實際播放時發現尺寸不對，可以在後續更新
        }
        
        Debug.Log("VideoPlayerController: 影片準備完成，開始播放");
        
        // 播放影片
        videoPlayer.Play();
    }
    
    /// <summary>
    /// 停止播放影片
    /// </summary>
    public void StopVideo()
    {
        if (videoPlayer != null && isPlaying)
        {
            videoPlayer.Stop();
            isPlaying = false;
            
            if (videoDisplay != null)
            {
                videoDisplay.gameObject.SetActive(false);
            }
            
            Debug.Log("VideoPlayerController: 影片已停止");
        }
    }
    
    /// <summary>
    /// 準備 RenderTexture（用於 VideoClip）
    /// </summary>
    private void PrepareRenderTexture()
    {
        if (videoPlayer.clip == null) return;
        
        // 創建或更新 RenderTexture
        int width = (int)videoPlayer.clip.width;
        int height = (int)videoPlayer.clip.height;
        
        Debug.Log($"VideoPlayerController: 創建 RenderTexture ({width} x {height})");
        
        CreateOrUpdateRenderTexture(width, height);
    }
    
    /// <summary>
    /// 準備 RenderTexture（用於 URL，使用預設尺寸）
    /// </summary>
    private void PrepareRenderTextureForUrl()
    {
        // URL 模式下，無法提前知道影片尺寸，使用預設尺寸
        // 實際尺寸會在影片準備完成後更新
        Debug.Log($"VideoPlayerController: 為 URL 模式創建 RenderTexture（預設尺寸 {DEFAULT_VIDEO_WIDTH} x {DEFAULT_VIDEO_HEIGHT}）");
        
        CreateOrUpdateRenderTexture(DEFAULT_VIDEO_WIDTH, DEFAULT_VIDEO_HEIGHT);
    }
    
    /// <summary>
    /// 創建或更新 RenderTexture
    /// </summary>
    private void CreateOrUpdateRenderTexture(int width, int height)
    {
        if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }
            
            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            
            Debug.Log($"VideoPlayerController: RenderTexture 已創建 - isCreated: {renderTexture.IsCreated()}");
        }
        
        // 設置 VideoPlayer 的輸出
        videoPlayer.targetTexture = renderTexture;
        
        // 將 RenderTexture 指定給 RawImage
        if (videoDisplay != null)
        {
            videoDisplay.texture = renderTexture;
            Debug.Log($"VideoPlayerController: RenderTexture 已指定給 RawImage");
        }
        else
        {
            Debug.LogError("VideoPlayerController: VideoDisplay (RawImage) 未設置！");
        }
    }
    
    /// <summary>
    /// 影片播放結束時調用
    /// </summary>
    private void OnVideoEnd(VideoPlayer vp)
    {
        Debug.Log("VideoPlayerController: 影片播放完畢");
        
        isPlaying = false;
        
        // 自動隱藏影片
        if (autoHideAfterPlay && videoDisplay != null)
        {
            videoDisplay.gameObject.SetActive(false);
        }
        
        // 執行回調
        onVideoEndCallback?.Invoke();
        onVideoEndCallback = null;
    }
    
    /// <summary>
    /// 檢查是否正在播放
    /// </summary>
    public bool IsPlaying()
    {
        return isPlaying;
    }
    
    void OnDestroy()
    {
        // 清理資源
        if (renderTexture != null)
        {
            renderTexture.Release();
            renderTexture = null;
        }
        
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
    }
}

