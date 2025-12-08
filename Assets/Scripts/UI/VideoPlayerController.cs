using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;

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
    [Tooltip("要播放的影片檔案")]
    public VideoClip videoClip;
    
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
        // 如果設置了影片檔案，預先載入
        if (videoClip != null)
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
    /// 播放影片
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
        
        // 如果提供了新的影片檔案，使用它
        if (clip != null)
        {
            videoClip = clip;
            videoPlayer.clip = clip;
        }
        
        // 檢查是否有影片可播放
        if (videoPlayer.clip == null)
        {
            Debug.LogError("VideoPlayerController: 沒有設置影片檔案！");
            onComplete?.Invoke();
            return;
        }
        
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
        
        Debug.Log($"VideoPlayerController: 準備播放影片 '{videoPlayer.clip.name}'");
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
    /// 準備 RenderTexture
    /// </summary>
    private void PrepareRenderTexture()
    {
        if (videoPlayer.clip == null) return;
        
        // 創建或更新 RenderTexture
        int width = (int)videoPlayer.clip.width;
        int height = (int)videoPlayer.clip.height;
        
        Debug.Log($"VideoPlayerController: 創建 RenderTexture ({width} x {height})");
        
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

