using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// 觸發器：當玩家走到某個地點時，強制玩家往前走嘗試跳過深坑，但能量不足導致跌入深坑
/// 包含完整的過場動畫序列：黑畫面閃爍 → 同時移動跳躍 → 長時間跌落 → 醒來
/// </summary>
public class PitFallTrigger : MonoBehaviour
{
    [Header("觸發設定")]
    [SerializeField] private bool canTriggerMultipleTimes = false; // 是否可以多次觸發


    [Header("跌落設定")]
    [SerializeField] private float fallingDuration = 3f; // 跌落持續時間
    [SerializeField] private float wakeUpDelay = 2f; // 落地後到醒來的延遲時間

    [Header("畫面效果")]
    [SerializeField] private Image screenFadeImage; // 用於畫面淡化的UI Image
    [SerializeField] private float screenFlashDuration = 0.2f; // 初始黑畫面閃爍時間
    [SerializeField] private float jumpEndFadeDuration = 0.5f; // 跳躍結束後畫面變暗時間
    [SerializeField] private float landingFadeDuration = 0.3f; // 落地瞬間黑畫面時間
    [SerializeField] private float wakeUpFadeDuration = 1f; // 醒來時畫面變亮時間

    private bool hasTriggered = false;
    private PlayerController playerController;
    private PlayerMovement playerMovement;
    private PlayerEnergy playerEnergy;
    private PlayerStateMachine stateMachine;
    private PlayerAnimationController animationController;
    private EnergyUIDisplay energyUI;

    // 臨時變數，用於在不同方法間共享數據
    private Rigidbody2D currentRigidbody;

    void Start()
    {
        // 確保有 Collider2D 組件
        Collider2D collider = GetComponent<Collider2D>();
        if (collider == null)
        {
            Debug.LogError($"PitFallTrigger: No Collider2D component found on '{gameObject.name}'! Please add a Collider2D component and set it as a trigger.");
            return;
        }

        // 檢查 Collider2D 是否設置為 Trigger
        if (!collider.isTrigger)
        {
            Debug.LogError($"PitFallTrigger: Collider2D on '{gameObject.name}' is not set as trigger! Please check 'Is Trigger' in the collider component.");
            collider.isTrigger = true; // 自動修復
            Debug.Log($"PitFallTrigger: Automatically set collider as trigger on '{gameObject.name}'.");
        }

        Debug.Log($"PitFallTrigger: Initialized on '{gameObject.name}' with collider '{collider.GetType().Name}' (IsTrigger: {collider.isTrigger})");

        // Initialize EnergyUI reference at start
        InitializeEnergyUI();
    }

    private void InitializeEnergyUI()
    {
        if (energyUI == null)
        {
            // First try to use the static singleton instance
            if (EnergyUIDisplay.Instance != null)
            {
                energyUI = EnergyUIDisplay.Instance;
            }
            // If not found (e.g. if Awake hasn't run yet or other issues), try finding by type including inactive
            else
            {
                // Try to find by GameObject name first
                GameObject energyUIGameObject = GameObject.Find("PlayerEnergyUI");
                if (energyUIGameObject != null)
                {
                    energyUI = energyUIGameObject.GetComponent<EnergyUIDisplay>();
                }
                
                // Fallback to finding by type (including inactive)
                if (energyUI == null)
                {
                    energyUI = FindFirstObjectByType<EnergyUIDisplay>(FindObjectsInactive.Include);
                }
            }

            Debug.Log($"PitFallTrigger: EnergyUIDisplay initialized: {(energyUI != null ? "SUCCESS" : "FAILED")}");
        }
    }
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"PitFallTrigger: Trigger entered by '{collision.gameObject.name}' with tag '{collision.gameObject.tag}'");

        // 檢查是否是玩家進入
        if (collision.CompareTag("Player"))
        {
            if (!canTriggerMultipleTimes && hasTriggered)
            {
                Debug.Log("PitFallTrigger: Already triggered, ignoring...");
                return;
            }

            Debug.Log("PitFallTrigger: Player entered trigger zone - checking jump state...");

            // 檢查玩家是否正在跳躍（垂直速度 > 0）
            Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
            if (playerRb != null && playerRb.linearVelocity.y > 0.1f)
            {
                Debug.Log($"PitFallTrigger: Player is jumping (velocity: {playerRb.linearVelocity}), triggering pitfall!");
                StartPitfallEvent();
            }
            else
            {
                Debug.Log($"PitFallTrigger: Player not jumping (velocity: {playerRb?.linearVelocity ?? Vector2.zero}), ignoring trigger");
            }
        }
        else
        {
            Debug.Log($"PitFallTrigger: Ignoring non-player object '{collision.gameObject.name}'");
        }
    }

    private void StartPitfallEvent()
    {
        hasTriggered = true;

        // 獲取玩家組件引用
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError("PitFallTrigger: PlayerController not found!");
                return;
            }
        }

        if (playerMovement == null)
        {
            playerMovement = playerController.GetComponent<PlayerMovement>();
        }

        if (playerEnergy == null)
        {
            playerEnergy = playerController.GetComponent<PlayerEnergy>();
        }

        if (stateMachine == null)
        {
            stateMachine = playerController.GetComponent<PlayerStateMachine>();
        }

        if (animationController == null)
        {
            animationController = playerController.GetComponent<PlayerAnimationController>();
        }

        // EnergyUI should already be initialized in Start(), but double-check
        if (energyUI == null)
        {
            InitializeEnergyUI();
        }

        // 初始化畫面效果
        if (screenFadeImage == null)
        {
            // 如果沒有指定，嘗試找到場景中的ScreenFadeImage
            GameObject fadeObj = GameObject.Find("ScreenFadeImage");
            if (fadeObj != null)
            {
                screenFadeImage = fadeObj.GetComponent<Image>();
                // 確保初始狀態是透明的
                if (screenFadeImage != null)
                {
                    screenFadeImage.color = new Color(0f, 0f, 0f, 0f);
                }
            }
            else
            {
                Debug.LogWarning("PitFallTrigger: ScreenFadeImage not found! Screen effects will be disabled. Please create a Canvas > UI > Image named 'ScreenFadeImage' for screen fade effects.");
            }
        }

        // 開始事件協程
        StartCoroutine(PitfallSequence());
    }

    private IEnumerator PitfallSequence()
    {
        Debug.Log("PitFallTrigger: Starting pitfall sequence - player jump interrupted!");

        // 1. 立即鎖定玩家控制
        stateMachine.ChangeState(PlayerState.Cutscene);
        playerMovement.SetMovementLocked(true); // 鎖定移動，玩家無法控制

        // 隱藏能量UI
        if (energyUI != null)
        {
            energyUI.gameObject.SetActive(false);
            Debug.Log("PitFallTrigger: Energy UI hidden during fall");
        }

        // 2. 移除玩家的垂直向上力，讓玩家開始下落
        currentRigidbody = playerMovement.GetComponent<Rigidbody2D>();
        if (currentRigidbody != null)
        {
            // 將垂直速度設為負值或0，讓玩家停止向上跳躍
            float currentHorizontal = currentRigidbody.linearVelocity.x;
            currentRigidbody.linearVelocity = new Vector2(currentHorizontal, -1f); // 輕微向下的初始速度
            Debug.Log($"PitFallTrigger: Interrupted jump - velocity set to ({currentHorizontal}, -1)");
        }

        // 3. 畫面變暗
        Debug.Log("PitFallTrigger: Screen fading to dark...");
        yield return StartCoroutine(ScreenFadeToDark(1f));

        // 4. 等待玩家落地
        Debug.Log("PitFallTrigger: Waiting for player to land...");
        float fallStartTime = Time.time;

        while (!playerMovement.IsGrounded())
        {
            // 持續確保玩家無法控制（萬一狀態被改變）
            if (stateMachine.CurrentState != PlayerState.Cutscene)
            {
                stateMachine.ChangeState(PlayerState.Cutscene);
            }

            yield return null;

            // 安全檢查：如果掉落時間太長，強制結束
            if (Time.time - fallStartTime > 5f)
            {
                Debug.LogWarning("PitFallTrigger: Fall timeout - forcing landing sequence");
                break;
            }
        }

        Debug.Log("PitFallTrigger: Player landed! Show trip animation...");

        // 5. 進入Trip狀態並顯示跌倒動畫
        stateMachine.ChangeState(PlayerState.Tripping);
        if (animationController != null)
        {
            animationController.TriggerTrip();
        }

        // 6. 落地後延遲 0.2 秒，然後顯示100%黑畫面持續3秒
        Debug.Log("PitFallTrigger: Landing - delay 0.1s then 100% black screen for 3 seconds...");

        // 延遲 0.2 秒
        yield return new WaitForSeconds(1f);

        // 快速淡入100%黑畫面
        Debug.Log("PitFallTrigger: Quick fade to black screen...");
        yield return StartCoroutine(ScreenFadeToBlack(0.2f));

        // 在黑畫面期間顯示爬起來動畫
        if (animationController != null)
        {
            animationController.TriggerRecover();
        }

        // 等待3秒（黑畫面持續時間）
        yield return new WaitForSeconds(3f);

        // 7. 畫面漸亮，恢復控制
        Debug.Log("PitFallTrigger: Waking up player...");
        yield return StartCoroutine(ScreenFadeIn(0.5f));

        // 恢復玩家控制
        stateMachine.ChangeState(PlayerState.Idle);
        playerMovement.SetMovementLocked(false);

        // 顯示能量UI
        if (energyUI != null)
        {
            energyUI.ForceShowUI();
            Debug.Log("PitFallTrigger: Energy UI shown after fall recovery");
        }
        else
        {
            Debug.LogWarning("PitFallTrigger: Cannot show Energy UI - reference is null!");
        }

        // 關閉無限能量，開始正常消耗
        if (playerEnergy != null)
        {
            playerEnergy.SetInfiniteEnergy(false);
            Debug.Log("PitFallTrigger: Infinite energy disabled - normal consumption starts");
        }

        Debug.Log("PitFallTrigger: Pitfall sequence completed - player control restored");
    }


    /// <summary>
    /// 黑畫面閃爍效果
    /// </summary>
    private IEnumerator ScreenFlash()
    {
        if (screenFadeImage == null) yield break;

        // 快速變黑然後變回透明
        Color originalColor = screenFadeImage.color;
        Color blackColor = new Color(0f, 0f, 0f, 1f);

        // 變黑
        float halfDuration = screenFlashDuration / 2f;
        float elapsedTime = 0f;

        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / halfDuration;
            screenFadeImage.color = Color.Lerp(originalColor, blackColor, t);
            yield return null;
        }

        // 變回透明
        elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / halfDuration;
            screenFadeImage.color = Color.Lerp(blackColor, originalColor, t);
            yield return null;
        }

        screenFadeImage.color = originalColor;
    }

    /// <summary>
    /// 快速淡入完全黑畫面
    /// </summary>
    private IEnumerator ScreenFadeToBlack(float duration)
    {
        if (screenFadeImage == null) yield break;

        Color originalColor = screenFadeImage.color;
        Color blackColor = new Color(0f, 0f, 0f, 1f); // 100%黑色，完全不透明

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            screenFadeImage.color = Color.Lerp(originalColor, blackColor, t);
            yield return null;
        }

        screenFadeImage.color = blackColor;
    }

    /// <summary>
    /// 畫面逐漸變暗
    /// </summary>
    private IEnumerator ScreenFadeToDark(float duration)
    {
        if (screenFadeImage == null) yield break;

        Color originalColor = screenFadeImage.color;
        Color darkColor = new Color(0f, 0f, 0f, 0.7f); // 稍微透明的黑色

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            screenFadeImage.color = Color.Lerp(originalColor, darkColor, t);
            yield return null;
        }

        screenFadeImage.color = darkColor;
    }

    /// <summary>
    /// 畫面逐漸變亮（模擬張開眼睛的效果：眨眼 + 逐漸清晰）
    /// 透明度變化：100%(全黑) -> 50% -> 70% -> 0%(全亮)
    /// 模擬剛醒來時眼睛張開、閉上再張開的過程
    /// </summary>
    private IEnumerator ScreenFadeIn(float duration)
    {
        if (screenFadeImage == null) yield break;

        Color transparentColor = new Color(0f, 0f, 0f, 0f);

        // 階段1: 第一次張開眼睛 (100% -> 50% 黑) - 視野模糊
        // 持續時間佔總時間的 40%
        float stage1Duration = duration * 0.4f;
        yield return StartCoroutine(FadeAlpha(1f, 0.5f, stage1Duration));

        // 階段2: 眼睛疲勞稍微閉上 (50% -> 70% 黑)
        // 持續時間佔總時間的 20%
        float stage2Duration = duration * 0.3f;
        yield return StartCoroutine(FadeAlpha(0.5f, 0.7f, stage2Duration));

        // 階段3: 完全張開眼睛 (70% -> 0% 黑) - 視野變清晰
        // 持續時間佔總時間的 40%
        float stage3Duration = duration * 0.3f;
        yield return StartCoroutine(FadeAlpha(0.7f, 0f, stage3Duration));

        screenFadeImage.color = transparentColor;
    }

    /// <summary>
    /// 漸變透明度
    /// </summary>
    private IEnumerator FadeAlpha(float startAlpha, float endAlpha, float duration)
    {
        float elapsedTime = 0f;
        Color baseColor = new Color(0f, 0f, 0f, 1f);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, t);
            screenFadeImage.color = new Color(0f, 0f, 0f, currentAlpha);
            yield return null;
        }

        screenFadeImage.color = new Color(0f, 0f, 0f, endAlpha);
    }
}

