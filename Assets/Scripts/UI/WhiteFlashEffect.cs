using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 白光閃爍效果 - 創建滿版白色閃光
/// 使用方法：
/// 1. 在 Canvas 上創建一個 UI Image (滿版)
/// 2. 設置 Image 顏色為白色，Alpha 設為 0
/// 3. 將此腳本附加到該 Image 上
/// </summary>
[RequireComponent(typeof(Image))]
public class WhiteFlashEffect : MonoBehaviour
{
    private static WhiteFlashEffect instance;
    private Image flashImage;
    private Coroutine currentFlashCoroutine;
    
    void Awake()
    {
        // 設置單例
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("WhiteFlashEffect: 場景中已存在另一個 WhiteFlashEffect 實例！");
            Destroy(gameObject);
            return;
        }
        
        // 獲取 Image 組件
        flashImage = GetComponent<Image>();
        
        if (flashImage == null)
        {
            Debug.LogError("WhiteFlashEffect: 找不到 Image 組件！");
            return;
        }
        
        // 確保初始狀態為完全透明
        Color color = flashImage.color;
        color.a = 0f;
        flashImage.color = color;
        
        // 確保物件在最上層
        transform.SetAsLastSibling();
    }
    
    /// <summary>
    /// 獲取單例實例
    /// </summary>
    public static WhiteFlashEffect GetInstance()
    {
        return instance;
    }
    
    /// <summary>
    /// 播放白光閃爍效果
    /// </summary>
    /// <param name="duration">閃光持續時間（秒）</param>
    /// <param name="fadeInTime">淡入時間（秒），預設為 0.1 秒</param>
    /// <param name="fadeOutTime">淡出時間（秒），預設為 0.3 秒</param>
    /// <param name="callback">完成後的回調函數</param>
    public void PlayFlash(float duration, float fadeInTime = 0.1f, float fadeOutTime = 0.3f, System.Action callback = null)
    {
        if (flashImage == null)
        {
            Debug.LogError("WhiteFlashEffect: Image 組件未找到！");
            callback?.Invoke();
            return;
        }
        
        // 如果已經有正在播放的閃光效果，先停止它
        if (currentFlashCoroutine != null)
        {
            StopCoroutine(currentFlashCoroutine);
        }
        
        currentFlashCoroutine = StartCoroutine(FlashCoroutine(duration, fadeInTime, fadeOutTime, callback));
    }
    
    /// <summary>
    /// 閃光效果協程
    /// </summary>
    private IEnumerator FlashCoroutine(float duration, float fadeInTime, float fadeOutTime, System.Action callback)
    {
        // 淡入（變白）
        float elapsedTime = 0f;
        Color color = flashImage.color;
        
        while (elapsedTime < fadeInTime)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeInTime);
            color.a = alpha;
            flashImage.color = color;
            yield return null;
        }
        
        // 確保完全白色
        color.a = 1f;
        flashImage.color = color;
        
        // 保持白色一段時間
        yield return new WaitForSeconds(duration);
        
        // 淡出（變透明）
        elapsedTime = 0f;
        while (elapsedTime < fadeOutTime)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeOutTime);
            color.a = alpha;
            flashImage.color = color;
            yield return null;
        }
        
        // 確保完全透明
        color.a = 0f;
        flashImage.color = color;
        
        currentFlashCoroutine = null;
        
        // 執行回調
        callback?.Invoke();
    }
    
    /// <summary>
    /// 停止當前的閃光效果
    /// </summary>
    public void StopFlash()
    {
        if (currentFlashCoroutine != null)
        {
            StopCoroutine(currentFlashCoroutine);
            currentFlashCoroutine = null;
        }
        
        // 重置為透明
        if (flashImage != null)
        {
            Color color = flashImage.color;
            color.a = 0f;
            flashImage.color = color;
        }
    }
}

