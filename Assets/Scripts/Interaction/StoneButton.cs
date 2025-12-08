using System.Collections;
using UnityEngine;

public class StoneButton : MonoBehaviour
{
    [Header("Stone Wall")]
    public StoneWall stoneWall;

    [Header("Camera")]
    public Camera mainCamera;
    public float cameraMoveDuration = 1f;

    [Header("Camera Follow Script")]
    public CameraFollow followScript;

    private bool isActivated = false;

    private Vector3 originalCameraPos;
    private PlayerMovement cachedMovement;

    private SpriteRenderer spriteRenderer;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (followScript == null)
            followScript = mainCamera.GetComponent<CameraFollow>();

        // ⭐ 取得 SpriteRenderer（因為你現在不用 Animator）
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;
        if (isActivated) return;

        isActivated = true;
        cachedMovement = player.GetComponent<PlayerMovement>();

        // ⭐ 1. 鎖住玩家
        cachedMovement.StopMovement();
        cachedMovement.SetMovementLocked(true);
        cachedMovement.SetGravityEnabled(false);

        originalCameraPos = mainCamera.transform.position;

        // ⭐ 2. 按鈕水平翻轉（取代動畫）
        if (spriteRenderer != null)
            spriteRenderer.flipX = true;

        // ⭐ 3. 開始鏡頭流程
        StartCoroutine(CameraFlow());
    }

    private IEnumerator CameraFlow()
    {
        // 暫停鏡頭跟隨
        if (followScript != null) followScript.enabled = false;

        yield return MoveCamera(mainCamera.transform.position, stoneWall.transform.position);

        yield return stoneWall.PlayRaiseAnimation();

        yield return MoveCamera(mainCamera.transform.position, originalCameraPos);

        // ⭐ 解除鎖定
        cachedMovement.SetMovementLocked(false);
        cachedMovement.SetGravityEnabled(true);

        // 恢復鏡頭跟隨
        if (followScript != null) followScript.enabled = true;
    }

    private IEnumerator MoveCamera(Vector3 from, Vector3 to)
    {
        float elapsed = 0f;
        Vector3 start = from;
        Vector3 end = to;
        end.z = start.z;

        while (elapsed < cameraMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cameraMoveDuration);

            mainCamera.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        mainCamera.transform.position = end;
    }
}