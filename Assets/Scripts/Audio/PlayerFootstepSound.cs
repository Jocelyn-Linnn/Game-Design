using UnityEngine;

/// <summary>
/// Manages footstep sound effects for the player
/// Plays footstep sounds when the player is walking on the ground
/// Integrates with SoundManager for unified audio management
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerFootstepSound : MonoBehaviour
{
    [Header("Footstep Sound Configuration")]
    [Tooltip("Array of audio clips to randomly play when player takes a step")]
    [SerializeField] private AudioClip[] footstepSounds;
    
    [Tooltip("Volume of footstep sounds (0.0 to 2.0, can exceed 1.0 for louder sounds)")]
    [SerializeField, Range(0f, 2f)] private float footstepVolume = 1.5f;

    [Header("Timing Settings")]
    [Tooltip("Minimum time between footstep sounds (in seconds)")]
    [SerializeField] private float minStepInterval = 0.3f;
    
    [Tooltip("Maximum time between footstep sounds (in seconds)")]
    [SerializeField] private float maxStepInterval = 0.5f;
    
    [Tooltip("Minimum movement speed to trigger footstep sounds")]
    [SerializeField] private float minMovementSpeed = 0.1f;

    [Header("Advanced Settings")]
    [Tooltip("Whether to adjust step interval based on movement speed")]
    [SerializeField] private bool adjustIntervalBySpeed = true;
    
    [Tooltip("Speed multiplier for step interval adjustment")]
    [SerializeField] private float speedMultiplier = 1f;

    // Component references
    private PlayerController playerController;
    private PlayerMovement playerMovement;
    private PlayerStateMachine stateMachine;
    private SoundManager soundManager;
    private AudioSource audioSource;

    // Footstep timing
    private float timeSinceLastStep = 0f;
    private float currentStepInterval = 0.5f;
    private bool wasMoving = false;

    void Awake()
    {
        // Get component references
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("PlayerFootstepSound: PlayerController component not found!");
            return;
        }

        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogError("PlayerFootstepSound: PlayerMovement component not found!");
            return;
        }

        stateMachine = GetComponent<PlayerStateMachine>();
        if (stateMachine == null)
        {
            Debug.LogError("PlayerFootstepSound: PlayerStateMachine component not found!");
            return;
        }

        // Initialize SoundManager reference
        soundManager = SoundManager.GetInstance();

        // Initialize AudioSource component for footstep sounds
        InitializeAudioSource();
    }

    /// <summary>
    /// Initialize the AudioSource component for footstep sound playback
    /// Configured as 2D sound to avoid distance attenuation issues
    /// </summary>
    private void InitializeAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure AudioSource for 2D playback (no spatialization)
        // This ensures footstep sounds are not affected by distance attenuation
        audioSource.spatialBlend = 0f; // 0 = 2D sound, 1 = 3D sound
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = footstepVolume;
    }

    void Update()
    {
        // Check if player is moving and grounded
        bool isMoving = IsPlayerMoving();
        bool isGrounded = playerMovement.IsGrounded();

        // Only play footstep sounds when moving and grounded
        if (isMoving && isGrounded)
        {
            // Update step interval based on speed if enabled
            if (adjustIntervalBySpeed)
            {
                UpdateStepInterval();
            }

            // Accumulate time
            timeSinceLastStep += Time.deltaTime;

            // Play footstep sound when interval is reached
            if (timeSinceLastStep >= currentStepInterval)
            {
                PlayFootstepSound();
                timeSinceLastStep = 0f;
                currentStepInterval = Random.Range(minStepInterval, maxStepInterval);
            }

            wasMoving = true;
        }
        else
        {
            // Reset timer when player stops moving
            if (wasMoving && !isMoving)
            {
                timeSinceLastStep = 0f;
            }
            wasMoving = false;
        }
    }

    /// <summary>
    /// Check if the player is currently moving
    /// </summary>
    private bool IsPlayerMoving()
    {
        if (stateMachine == null || playerMovement == null)
        {
            return false;
        }

        // Check if player is in moving state
        if (stateMachine.CurrentState != PlayerState.Moving)
        {
            return false;
        }

        // Check movement speed
        Vector2 velocity = playerMovement.GetVelocity();
        float horizontalSpeed = Mathf.Abs(velocity.x);
        
        return horizontalSpeed >= minMovementSpeed;
    }

    /// <summary>
    /// Update step interval based on current movement speed
    /// Faster movement = shorter interval between steps
    /// </summary>
    private void UpdateStepInterval()
    {
        if (playerMovement == null)
        {
            return;
        }

        Vector2 velocity = playerMovement.GetVelocity();
        float horizontalSpeed = Mathf.Abs(velocity.x);

        // Calculate adjusted interval based on speed
        // Faster speed = shorter interval
        float speedFactor = Mathf.Clamp01(horizontalSpeed / 10f); // Normalize to 0-1 range
        float adjustedInterval = Mathf.Lerp(maxStepInterval, minStepInterval, speedFactor * speedMultiplier);
        
        currentStepInterval = Mathf.Clamp(adjustedInterval, minStepInterval, maxStepInterval);
    }

    /// <summary>
    /// Play a footstep sound
    /// Randomly selects one sound from the footstepSounds array
    /// Uses dedicated AudioSource component for better volume control
    /// Configured as 2D sound to avoid distance attenuation
    /// </summary>
    private void PlayFootstepSound()
    {
        // Check if footstep sounds array is valid
        if (footstepSounds == null || footstepSounds.Length == 0)
        {
            return;
        }

        // Ensure AudioSource is initialized
        if (audioSource == null)
        {
            InitializeAudioSource();
        }

        // Randomly select a footstep sound from the array
        int randomIndex = Random.Range(0, footstepSounds.Length);
        AudioClip selectedSound = footstepSounds[randomIndex];

        // Skip if the selected clip is null
        if (selectedSound == null)
        {
            return;
        }

        // Set AudioSource volume to footstepVolume (can be 0.0 to 2.0)
        // AudioSource.volume can exceed 1.0 to amplify sound beyond normal range
        audioSource.volume = footstepVolume;

        // Play sound using AudioSource (2D sound, no distance attenuation)
        // Use volume scale of 1.0 since AudioSource.volume already controls the amplification
        audioSource.PlayOneShot(selectedSound, 1f);
    }

    /// <summary>
    /// Set the footstep sound clips array
    /// </summary>
    public void SetFootstepSounds(AudioClip[] clips)
    {
        footstepSounds = clips;
    }

    /// <summary>
    /// Set a single footstep sound clip (converts to array with one element)
    /// Provided for backwards compatibility
    /// </summary>
    public void SetFootstepSound(AudioClip clip)
    {
        footstepSounds = new AudioClip[] { clip };
    }

    /// <summary>
    /// Get the footstep sound clips array
    /// </summary>
    public AudioClip[] GetFootstepSounds()
    {
        return footstepSounds;
    }

    /// <summary>
    /// Get the first footstep sound clip (if exists)
    /// Provided for backwards compatibility
    /// </summary>
    public AudioClip GetFootstepSound()
    {
        if (footstepSounds != null && footstepSounds.Length > 0)
        {
            return footstepSounds[0];
        }
        return null;
    }

    /// <summary>
    /// Set the footstep volume (0.0 to 2.0)
    /// Updates the AudioSource volume immediately if it exists
    /// </summary>
    public void SetFootstepVolume(float volume)
    {
        footstepVolume = Mathf.Clamp(volume, 0f, 2f);
        
        // Update AudioSource volume if it exists
        if (audioSource != null)
        {
            audioSource.volume = footstepVolume;
        }
    }

    /// <summary>
    /// Get the current footstep volume
    /// </summary>
    public float GetFootstepVolume()
    {
        return footstepVolume;
    }

    /// <summary>
    /// Enable or disable footstep sounds
    /// </summary>
    public void SetFootstepEnabled(bool enabled)
    {
        this.enabled = enabled;
        if (!enabled)
        {
            timeSinceLastStep = 0f;
        }
    }
}

