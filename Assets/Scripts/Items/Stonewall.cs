using System.Collections;
using UnityEngine;

public class StoneWall : MonoBehaviour
{
    [Header("Stone Wall Settings")]
    public Collider2D wallCollider;

    [Header("升起動畫設定")]
   /**
    * 上升高度（你可以調）
    */
    public float raiseHeight = 3f;
    public float raiseDuration = 1f;

    private bool isRaised = false;

    Animator animator;

    private void Awake()
    {
        if (wallCollider == null)
            wallCollider = GetComponent<Collider2D>();

        animator = GetComponent<Animator>();
    }

    public IEnumerator PlayRaiseAnimation()
    {
        if (isRaised) yield break;

        isRaised = true;

        Debug.Log("StoneWall: 播放上升動畫");

        if (wallCollider != null)
            wallCollider.enabled = false;

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + new Vector3(0, raiseHeight, 0);

        float elapsed = 0f;

        while (elapsed < raiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / raiseDuration);

            transform.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }

        transform.position = endPos;

        // 🔥 升起後關掉動畫（發光效果立即停止）
        if (animator != null)
            // animator.enabled = false;
            animator.Play(0, 0, 0f);  // 播放當前動畫 (state 0)，從 0 秒開始
            animator.Update(0f);      // 立即更新畫面，避免下一幀才刷新
            animator.enabled = false; // 停在第一偵
    }


    public bool IsRaised()
    {
        return isRaised;
    }
}