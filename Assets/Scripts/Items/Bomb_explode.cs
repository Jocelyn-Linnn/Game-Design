using UnityEngine;

public class Bomb_explode : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Audio clip to play when bomb explodes")]
    [SerializeField] private AudioClip explosionSound;
    
    [Tooltip("Volume of the explosion sound (0.0 to 1.0)")]
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 1f;
    
    [Tooltip("Delay before playing explosion sound (in seconds)")]
    [SerializeField] private float soundDelay = 1f;
    
    [Tooltip("Whether to play explosion sound automatically when bomb spawns")]
    [SerializeField] private bool playOnStart = true;

    private bool hasPlayedSound = false;

    void Start()
    {
        // Play explosion sound on start if enabled
        if (playOnStart)
        {
            PlayExplosionSound();
        }
    }

    void Update()
    {

    }

    /// <summary>
    /// Play explosion sound effect at bomb position with delay
    /// Can be called via Animation Event or automatically on Start
    /// </summary>
    public void PlayExplosionSound()
    {
        // Prevent playing sound multiple times
        if (hasPlayedSound)
        {
            return;
        }

        hasPlayedSound = true;

        // Schedule sound playback with delay
        if (soundDelay > 0f)
        {
            Invoke(nameof(PlaySoundDelayed), soundDelay);
            Debug.Log($"Bomb_explode: Scheduled explosion sound to play in {soundDelay} seconds");
        }
        else
        {
            // Play immediately if no delay
            PlaySoundDelayed();
        }
    }

    /// <summary>
    /// Internal method to actually play the sound
    /// Called after delay by Invoke
    /// </summary>
    private void PlaySoundDelayed()
    {
        if (explosionSound != null)
        {
            // Play 3D sound at bomb position
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
            
            Debug.Log($"Bomb_explode: Playing explosion sound '{explosionSound.name}' at volume {explosionVolume}");
        }
        else
        {
            Debug.LogWarning("Bomb_explode: No explosion sound assigned!");
        }
    }

    // 🔥 這個函式會在爆炸動畫結束時由 Animation Event 呼叫
    public void OnExplosionEnd()
    {
        // 尋找場景中名為 floor(10) 的物件
        GameObject targetFloor = GameObject.Find("Floor_destroy_by_bomb");
        GameObject targetFloor1 = GameObject.Find("grass1");

        if (targetFloor != null)
        {
            // 讓該物件消失
            targetFloor.SetActive(false);
            targetFloor1.SetActive(false);
            var hint = FindObjectOfType<HintTrigger>();
            if (hint != null)
            {
                hint.OnLinkedFloorDestroyed();
            }
            Debug.Log("💥 Floor_destroy_by_bomb 已消失！");
        }
        else
        {
            Debug.LogWarning("⚠️ 找不到名為 Floor_destroy_by_bomb 的物件！");
        }

        // （可選）摧毀炸彈物件本身
        Destroy(gameObject, 0.2f);
    }
}
