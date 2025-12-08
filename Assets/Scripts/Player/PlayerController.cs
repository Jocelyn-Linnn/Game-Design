using UnityEngine;
using System.Collections;

/// <summary>
/// Main player controller that coordinates all player subsystems
/// Manages input, state, movement, animation, and interactions
/// Implements IInteractor to allow interaction with game objects
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerAnimationController))]
[RequireComponent(typeof(InteractionHandler))]
public class PlayerController : MonoBehaviour, IInteractor
{
    [Header("Component References")]
    private PlayerMovement movement;
    private PlayerStateMachine stateMachine;
    private PlayerAnimationController animationController;
    private InteractionHandler interactionHandler;
    private PlayerEnergy energySystem;

    [Header("Input Keys")]
    [SerializeField] private KeyCode moveLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode moveRightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode interactKey = KeyCode.Z;  // 改用 Z 鍵來互動（坐下、起立、使用鑰匙、使用炸彈等）
    [SerializeField] private KeyCode respawnKey = KeyCode.R;
    [SerializeField] private KeyCode fastTravelMenuKey = KeyCode.M;

    [Header("Rappelling Settings")]
    [SerializeField] private float rappellingDuration = 1.0f;

    [Header("Rendering Settings")]
    [Tooltip("Z index (Sorting Order) for player sprite renderers. Higher values render on top")]
    [SerializeField] private int playerZIndex = 1;

    // Chair interaction state
    private chair currentChair = null;

    // Movement state tracking for animation triggers
    private bool wasMoving = false;
    private bool lastFacingRight = true; // Track last facing direction to prevent flickering

    // Sprite renderer references for z index management
    private SpriteRenderer[] spriteRenderers;

    // R button prompt for tripping state
    private GameObject tripPromptObject;
    private SpriteRenderer tripPromptRenderer;
    [SerializeField] private Sprite rButtonSprite;
    private Vector3 tripPromptOffset = new Vector3(0f, 1.5f, 0f);
    private Vector2 tripPromptSize = new Vector2(1.5f, 0.75f);
    private float tripPromptPulseSpeed = 1.0f;  // 閃爍速度調慢
    private float tripPromptMinAlpha = 0.7f;
    private float tripPromptMaxAlpha = 1f;
    private bool tripPromptEnableFloat = true;
    private float tripPromptFloatDistance = 0.1f;
    private float tripPromptFloatSpeed = 1.2f;  // 浮動速度也調慢以保持協調
    private bool isTripPromptVisible = false;
    private Vector3 tripPromptBasePosition;
    private float tripPromptFloatTimer = 0f;

    private PlayerState lastState = PlayerState.Idle; // For debug logging
    public bool IsInCutscene = false;
    private bool isRespawnEnabled = false; // 控制是否能按R回到存档点（默认禁用，直到坐在椅子上）


    void OnEnable()
    {
        if (energySystem == null)
            energySystem = GetComponent<PlayerEnergy>();

        // Removed automatic trip on energy depletion - now only trips when pressing space with no energy
        // if (energySystem != null)
        // {
        //     energySystem.OnFirstEnergyDepletion += HandleTrip;
        // }
    }

    void OnDisable()
    {
        // Removed automatic trip on energy depletion - now only trips when pressing space with no energy
        // if (energySystem != null)
        // {
        //     energySystem.OnFirstEnergyDepletion -= HandleTrip;
        // }
    }

    void Awake()
    {
        // Get all required components
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        animationController = GetComponent<PlayerAnimationController>();
        interactionHandler = GetComponent<InteractionHandler>();
        energySystem = GetComponent<PlayerEnergy>();

        if (energySystem == null)
        {
            Debug.LogWarning("PlayerController: PlayerEnergy component not found! Jump energy system will be disabled.");
        }

        // Get all SpriteRenderer components in player and children
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        
        // Apply initial z index setting
        SetPlayerZIndex(playerZIndex);

        // Initialize trip prompt
        InitializeTripPrompt();
    }

    void Start()
    {
        // Initialize to idle state
        stateMachine.ChangeState(PlayerState.Idle);
    }

    void Update()
    {
        // Debug current state at start of each update
        if (stateMachine.CurrentState != lastState)
        {
            Debug.Log($"PlayerController: State changed from {lastState} to {stateMachine.CurrentState}");
            lastState = stateMachine.CurrentState;
        }

        // Update trip prompt animation regardless of input lock state
        UpdateTripPromptAnimation();

        // Check R key first (before any input locking)
        if (Input.GetKeyDown(respawnKey))
        {
            Debug.Log($"PlayerController: R key pressed! Current state: {stateMachine.CurrentState}");

            // Allow respawn if tripping
            if (stateMachine.CurrentState == PlayerState.Tripping)
            {
                Debug.Log("PlayerController: R key detected while Tripping! Starting recovery...");
                StartCoroutine(RecoverFromTrip());
                return;
            }
        }

        // Don't process input if in locked state
        if (stateMachine.IsInputLocked())
        {
            return;
        }

        if (IsInCutscene)
        {
            return;
        }


        HandleMovementInput();
        HandleJumpInput();
        HandleInteractionInput();
        HandleSystemInput();
        UpdateAnimations();
        UpdateMovementState();
    }

    /// <summary>
    /// Handle horizontal movement input
    /// </summary>
    private void HandleMovementInput()
    {
        if (!stateMachine.CanMove())
        {
            // Clear input when movement is not allowed (e.g., when sitting or tripping)
            // This prevents cached input from being applied in FixedUpdate
            if (stateMachine.CurrentState == PlayerState.Sitting || 
                stateMachine.CurrentState == PlayerState.Tripping)
            {
                movement.ClearInput();
            }
            return;
        }

        float horizontal = 0f;
        bool isMoving = false;
        bool isRunning = false;

        if (Input.GetKey(moveRightKey))
        {
            horizontal = 1f;
            // Only change facing direction if it's different from current
            if (!lastFacingRight)
            {
                lastFacingRight = true;
                animationController.SetFacingDirection(true);
            }
            isMoving = true;
        }
        else if (Input.GetKey(moveLeftKey))
        {
            horizontal = -1f;
            // Only change facing direction if it's different from current
            if (lastFacingRight)
            {
                lastFacingRight = false;
                animationController.SetFacingDirection(false);
            }
            isMoving = true;
        }

        // Check if run key is held while moving
        if (isMoving && Input.GetKey(runKey))
        {
            isRunning = true;
        }

        // Trigger walk animation when movement starts (transition from idle to moving)
        if (isMoving && !wasMoving && movement.IsGrounded() && animationController.IsAnimationSystemReady())
        {
            animationController.TriggerWalk();
        }

        // Update movement state tracking
        wasMoving = isMoving;

        movement.Move(horizontal, isRunning);
    }

    /// <summary>
    /// Handle jump input - supports variable height jumping with energy cost
    /// </summary>
    private void HandleJumpInput()
    {
        // Start jump when button is first pressed
        if (stateMachine.CanJump() && Input.GetKeyDown(jumpKey))
        {
            // Check if player has enough energy to jump
            if (energySystem != null)
            {
                if (!energySystem.HasEnergyToJump())
                {
                    // Player is out of energy and trying to jump - trigger trip
                    Debug.Log("Cannot jump - not enough energy! Triggering trip...");
                    HandleTrip();
                    return;
                }

                // Consume energy for the jump
                if (!energySystem.ConsumeJumpEnergy())
                {
                    return;
                }
            }

            movement.StartJump();
            if (animationController.IsAnimationSystemReady())
            {
                animationController.TriggerJump();
            }
            stateMachine.ChangeState(PlayerState.Jumping);
        }

        // Continue applying upward force while button is held
        if (Input.GetKey(jumpKey))
        {
            movement.ContinueJump();
        }

        // Stop jump early when button is released
        if (Input.GetKeyUp(jumpKey))
        {
            movement.StopJump();
        }
    }

    /// <summary>
    /// Handle interaction input (sitting on chairs, using objects, etc.)
    /// </summary>
    private void HandleInteractionInput()
    {
        if (!stateMachine.CanInteract())
        {
            Debug.Log("Cannot interact - current state: " + stateMachine.CurrentState);
            return;
        }

        // Interact with nearby objects
        if (Input.GetKeyDown(interactKey))
        {
            Debug.Log("Z key pressed - attempting interaction");
            // Try standard interaction first
            if (interactionHandler.TryInteract(this))
            {
                Debug.Log("Interaction successful!");
                if (animationController.IsAnimationSystemReady())
                {
                    animationController.TriggerInteract();
                }
            }
            else
            {
                Debug.Log("No interactable object nearby");
            }
        }

        // Exit interaction is now handled by the interact key (Z)
        // No longer need a separate exit key
    }

    /// <summary>
    /// Handle system input (R key for respawn, M key for fast travel menu, Z for chair interaction)
    /// </summary>
    private void HandleSystemInput()
    {
        // Respawn at last chair
        if (Input.GetKeyDown(respawnKey) && isRespawnEnabled)
        {
            RespawnAtLastChair();
        }

        // Toggle fast travel menu
        if (Input.GetKeyDown(fastTravelMenuKey))
        {
            ToggleFastTravelMenu();
        }

        // Handle chair interaction with Z key
        // Note: This is now handled by the interaction system (InteractionHandler)
        // The Z key is mapped to the interact key
    }

    /// <summary>
    /// Update animation parameters based on current movement
    /// </summary>
    private void UpdateAnimations()
    {
        // Only update animations if the animation system is properly configured
        if (!animationController.IsAnimationSystemReady())
        {
            return;
        }

        Vector2 velocity = movement.GetVelocity();
        bool isGrounded = movement.IsGrounded();

        // Apply a small threshold to prevent tiny velocity fluctuations from affecting animations
        float speed = Mathf.Abs(velocity.x);
        if (speed < 0.01f)
        {
            speed = 0f;
        }

        animationController.SetSpeed(speed);
        animationController.SetGrounded(isGrounded);
        animationController.SetSitting(stateMachine.CurrentState == PlayerState.Sitting);
        animationController.SetRunning(movement.IsRunning());

        // Update jump animation dynamically based on vertical velocity
        if (!isGrounded)
        {
            // Get height above ground for better animation timing
            float heightAboveGround = CalculateHeightAboveGround();

            // Get gravity scale from rigidbody
            Rigidbody2D rb = movement.GetComponent<Rigidbody2D>();
            float gravityScale = rb != null ? rb.gravityScale : 1f;

            animationController.UpdateJumpAnimation(velocity.y, isGrounded, heightAboveGround, gravityScale);
        }
    }

    /// <summary>
    /// Update movement-related state based on velocity and ground status
    /// </summary>
    private void UpdateMovementState()
    {
        // Don't update if in special states
        if (stateMachine.CurrentState == PlayerState.Sitting ||
            stateMachine.CurrentState == PlayerState.Rappelling ||
            stateMachine.CurrentState == PlayerState.Cutscene ||
            stateMachine.CurrentState == PlayerState.Tripping)
        {
            return;
        }

        Vector2 velocity = movement.GetVelocity();
        bool isGrounded = movement.IsGrounded();

        if (!isGrounded)
        {
            if (velocity.y > 0.1f)
            {
                stateMachine.ChangeState(PlayerState.Jumping);
            }
            else
            {
                stateMachine.ChangeState(PlayerState.Falling);
            }
        }
        else if (Mathf.Abs(velocity.x) > 0.1f)
        {
            stateMachine.ChangeState(PlayerState.Moving);
        }
        else
        {
            stateMachine.ChangeState(PlayerState.Idle);
        }
    }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Calculate height above ground using raycast
    /// </summary>
    private float CalculateHeightAboveGround()
    {
        // Cast ray downward to detect ground
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 50f, LayerMask.GetMask("Ground"));

        if (hit.collider != null)
        {
            return transform.position.y - hit.point.y;
        }

        // Fallback: assume reasonable height if no ground detected
        return 2f;
    }

    // ==================== PUBLIC API FOR INTERACTIONS ====================

    /// <summary>
    /// Sit down on a chair
    /// </summary>
    public void SitOnChair(chair chairToSit)
    {
        if (stateMachine.CurrentState == PlayerState.Sitting)
        {
            Debug.LogWarning("Already sitting!");
            return;
        }

        currentChair = chairToSit;
        // Don't change player position - just change state
        // transform.position = chairToSit.sitpoint.position;

        // Stop movement and lock it to prevent any input from being applied
        movement.StopMovement();
        movement.SetMovementLocked(true);
        movement.SetGravityEnabled(false);

        stateMachine.ChangeState(PlayerState.Sitting);

        // Start restoring energy while sitting
        if (energySystem != null)
        {
            energySystem.StartEnergyRestore();
        }

        Debug.Log("Player sat down on chair - energy restoring");
    }

    /// <summary>
    /// Leave the current chair and stand up
    /// </summary>
    public void LeaveChair()
    {
        if (stateMachine.CurrentState != PlayerState.Sitting)
        {
            return;
        }

        // Stop restoring energy when leaving chair
        if (energySystem != null)
        {
            energySystem.StopEnergyRestore();
        }

        // Unlock movement before changing position
        movement.SetMovementLocked(false);

        // Move slightly upward to avoid re-triggering the chair - REMOVED to prevent leaving ground
        // transform.position += new Vector3(0f, 0.5f, 0f);

        movement.SetGravityEnabled(true);
        stateMachine.ChangeState(PlayerState.Idle);

        currentChair = null;

        FastTravelUI fastTravelUI = FindFirstObjectByType<FastTravelUI>();
        if (fastTravelUI != null && fastTravelUI.IsOpen())
        {
            fastTravelUI.CloseFastTravelUI();
        }

        Debug.Log("Player left the chair");
    }

    /// <summary>
    /// Start rappelling down a cable
    /// </summary>
    public void StartRappelling(Transform targetPosition)
    {
        if (stateMachine.CurrentState == PlayerState.Rappelling)
        {
            Debug.LogWarning("Already rappelling!");
            return;
        }

        stateMachine.ChangeState(PlayerState.Rappelling);
        StartCoroutine(RappelSequence(targetPosition));
    }

    /// <summary>
    /// Coroutine that handles the rappelling sequence
    /// </summary>
    private IEnumerator RappelSequence(Transform targetPosition)
    {
        // Hide player sprite during rappelling animation
        animationController.SetVisible(false);
        movement.StopMovement();
        movement.SetMovementLocked(true);
        movement.SetGravityEnabled(false);

        Debug.Log("Rappelling started");

        // Wait for rappelling animation duration
        yield return new WaitForSeconds(rappellingDuration);

        // Teleport to target position
        movement.Teleport(targetPosition.position);
        movement.SetMovementLocked(false);
        movement.SetGravityEnabled(true);

        // Show player sprite again
        animationController.SetVisible(true);

        // Return to normal state
        stateMachine.ChangeState(PlayerState.Idle);

        Debug.Log("Rappelling completed");
    }

    // ==================== TRIGGER DETECTION ====================

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Player triggered: " + collision.gameObject.name);

        // Key pickup is now handled by Key component itself (automatic collection)

        // Handle interactable objects
        IInteractable interactable = collision.GetComponent<IInteractable>();
        if (interactable != null)
        {
            Debug.Log("Found interactable: " + collision.gameObject.name);
            interactionHandler.RegisterInteractable(interactable);
            interactable.OnInteractorEnterZone(this);
        }
        else
        {
            Debug.Log("No IInteractable component found on: " + collision.gameObject.name);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // Handle interactable objects
        IInteractable interactable = collision.GetComponent<IInteractable>();
        if (interactable != null)
        {
            interactionHandler.UnregisterInteractable(interactable);
            interactable.OnInteractorExitZone(this);
        }
    }

    // ==================== SYSTEM METHODS ====================

    /// <summary>
    /// Respawn player at the last chair they sat on
    /// Uses GameManager
    /// </summary>
    private void RespawnAtLastChair()
    {
        if (!isRespawnEnabled)
        {
            Debug.Log("PlayerController: Respawn is disabled - cannot respawn at last chair");
            return;
        }

        Debug.Log("PlayerController: RespawnAtLastChair called (normal respawn, not from trip)");

        GameManager gameManager = GameManager.GetInstance();
        if (gameManager == null)
        {
            Debug.LogError("PlayerController: GameManager not found! Cannot respawn.");
            return;
        }

        Debug.Log("PlayerController: Calling GameManager.RespawnPlayer()");
        gameManager.RespawnPlayer();
    }

    /// <summary>
    /// Toggle the fast travel menu
    /// </summary>
    private void ToggleFastTravelMenu()
    {
        Debug.Log("PlayerController: M key pressed - attempting to toggle fast travel menu");

        if (!IsSitting())
        {
            Debug.Log("PlayerController: Fast travel menu unavailable - player is not sitting");
            return;
        }

        // Fallback to original FastTravelUI
        FastTravelUI fastTravelUI = FindFirstObjectByType<FastTravelUI>();

        if (fastTravelUI != null)
        {
            Debug.Log("PlayerController: FastTravelUI found, toggling menu...");
            if (fastTravelUI.IsOpen())
            {
                fastTravelUI.CloseFastTravelUI();
            }
            else
            {
                fastTravelUI.OpenFastTravelUI();
            }
            return;
        }

        Debug.LogWarning("PlayerController: No Fast Travel UI found in scene!");
    }

    // ==================== PUBLIC ACCESSORS ====================

    /// <summary>
    /// Get the carried item (for door checking, etc.)
    /// </summary>
    public GameObject GetCarriedItem()
    {
        return interactionHandler.GetCarriedItem();
    }

    /// <summary>
    /// Use the currently carried item (called by doors, etc.)
    /// </summary>
    public void UseCarriedItem()
    {
        interactionHandler.UseCarriedItem();
    }

    /// <summary>
    /// Get the current player state
    /// </summary>
    public PlayerState GetCurrentState()
    {
        return stateMachine.CurrentState;
    }

    /// <summary>
    /// Check if player is currently sitting
    /// </summary>
    public bool IsSitting()
    {
        return stateMachine.CurrentState == PlayerState.Sitting;
    }

    /// <summary>
    /// Get the interaction handler component
    /// </summary>
    public InteractionHandler GetInteractionHandler()
    {
        return interactionHandler;
    }

    /// <summary>
    /// Get the energy system component
    /// </summary>
    public PlayerEnergy GetEnergySystem()
    {
        return energySystem;
    }

    /// <summary>
    /// 启用/禁用回到存档点功能（R键）
    /// </summary>
    public void SetRespawnEnabled(bool enabled)
    {
        isRespawnEnabled = enabled;
        Debug.Log($"PlayerController: Respawn enabled set to {enabled}");
    }

    /// <summary>
    /// 检查回到存档点功能是否启用
    /// </summary>
    public bool IsRespawnEnabled()
    {
        return isRespawnEnabled;
    }

    // ==================== IInteractor IMPLEMENTATION ====================

    /// <summary>
    /// Get the transform of this interactor
    /// </summary>
    Transform IInteractor.GetTransform()
    {
        return transform;
    }

    /// <summary>
    /// Get the GameObject of this interactor
    /// </summary>
    GameObject IInteractor.GetGameObject()
    {
        return gameObject;
    }

    // ==================== Z INDEX MANAGEMENT ====================

    /// <summary>
    /// Set the z index (Sorting Order) for all player sprite renderers
    /// Higher values render on top of lower values
    /// </summary>
    /// <param name="zIndex">The sorting order value to set</param>
    public void SetPlayerZIndex(int zIndex)
    {
        playerZIndex = zIndex;

        // Refresh sprite renderer references if needed
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        // Apply z index to all sprite renderers
        foreach (SpriteRenderer renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                renderer.sortingOrder = zIndex;
            }
        }
    }

    /// <summary>
    /// Get the current z index (Sorting Order) of the player
    /// </summary>
    /// <returns>The current sorting order value</returns>
    public int GetPlayerZIndex()
    {
        return playerZIndex;
    }

    // ==================== TRIP PROMPT LOGIC ====================

    private void InitializeTripPrompt()
    {
        // Load R button sprite if not assigned
        if (rButtonSprite == null)
        {
            LoadRButtonSprite();
        }

        // Create the prompt object
        CreateTripPromptObject();

        // Initially hide the prompt
        SetTripPromptVisible(false);
    }

    private void LoadRButtonSprite()
    {
        // 載入新的 R_but 圖片
        rButtonSprite = Resources.Load<Sprite>("UI/R_but");

        #if UNITY_EDITOR
        if (rButtonSprite == null)
        {
            rButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/UI/R_but.png");
        }
        #endif

        if (rButtonSprite == null)
        {
            Debug.LogWarning("PlayerController: 無法載入 R 鍵圖片！路徑：Assets/Image/UI/R_but.png");
        }
        else
        {
            Debug.Log($"PlayerController: 成功載入 R 鍵圖片：{rButtonSprite.name}, texture: {rButtonSprite.texture?.name}, rect: {rButtonSprite.rect}, bounds: {rButtonSprite.bounds}");
        }
    }

    private void CreateTripPromptObject()
    {
        // Create new GameObject for the prompt
        tripPromptObject = new GameObject("TripPrompt_RButton");
        tripPromptObject.transform.SetParent(transform);
        tripPromptObject.transform.localPosition = tripPromptOffset;
        tripPromptBasePosition = tripPromptObject.transform.position;

        // Add SpriteRenderer
        tripPromptRenderer = tripPromptObject.AddComponent<SpriteRenderer>();
        tripPromptRenderer.sprite = rButtonSprite;
        tripPromptRenderer.sortingOrder = 500; // Very high sorting order to appear above everything
        // tripPromptRenderer.sortingLayerName = "UI"; // Removed - may not exist in this project

        // Force color to be visible
        tripPromptRenderer.color = new Color(1f, 1f, 1f, 1f);

        Debug.Log($"PlayerController: Created SpriteRenderer with sprite: {rButtonSprite?.name}, enabled: {tripPromptRenderer.enabled}, sortingOrder: {tripPromptRenderer.sortingOrder}");

        // Set size
        if (rButtonSprite != null)
        {
            Vector3 scale = new Vector3(
                tripPromptSize.x / rButtonSprite.bounds.size.x,
                tripPromptSize.y / rButtonSprite.bounds.size.y,
                1f
            );
            tripPromptObject.transform.localScale = scale;
            Debug.Log($"PlayerController: Set trip prompt scale to {scale}, sprite bounds: {rButtonSprite.bounds.size}, final size: {scale.x * rButtonSprite.bounds.size.x} x {scale.y * rButtonSprite.bounds.size.y}");
        }
        else
        {
            Debug.LogWarning("PlayerController: Cannot set trip prompt size - rButtonSprite is null!");
        }

        // Initially hide
        tripPromptRenderer.enabled = false;

        Debug.Log("PlayerController: Trip prompt object created successfully");
    }

    private void SetTripPromptVisible(bool visible)
    {
        isTripPromptVisible = visible;

        if (tripPromptRenderer != null)
        {
            tripPromptRenderer.enabled = visible;
            Debug.Log($"PlayerController: Trip prompt renderer enabled: {visible}");

            if (visible)
            {
                // Reset animation state
                tripPromptFloatTimer = 0f;
                if (tripPromptObject != null)
                {
                    tripPromptBasePosition = transform.position + tripPromptOffset;
                    tripPromptObject.transform.position = tripPromptBasePosition;
                    Debug.Log($"PlayerController: Trip prompt positioned at {tripPromptBasePosition}, player position: {transform.position}");
                }
            }
        }
        else
        {
            Debug.LogWarning("PlayerController: Trip prompt renderer is null!");
        }
    }

    private void UpdateTripPromptAnimation()
    {
        if (!isTripPromptVisible || tripPromptRenderer == null) return;

        // Debug logging to check if animation is running
        Debug.Log("TripPromptAnimation: Updating animation");

        // Update pulse animation
        UpdateTripPromptPulse();

        // Update float animation if enabled
        if (tripPromptEnableFloat)
        {
            UpdateTripPromptFloat();
        }
    }

    private void UpdateTripPromptPulse()
    {
        if (tripPromptRenderer == null) return;

        // Calculate pulsing alpha
        float pulse = Mathf.Sin(Time.time * tripPromptPulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(tripPromptMinAlpha, tripPromptMaxAlpha, pulse);

        // Apply alpha
        Color color = tripPromptRenderer.color;
        color.a = alpha;
        tripPromptRenderer.color = color;

        // Debug logging
        Debug.Log($"TripPromptPulse: Alpha = {alpha}, Pulse = {pulse}, Color = {color}, Enabled = {tripPromptRenderer.enabled}, Position = {tripPromptRenderer.transform.position}");
    }

    private void UpdateTripPromptFloat()
    {
        if (tripPromptObject == null) return;

        tripPromptFloatTimer += Time.deltaTime * tripPromptFloatSpeed;
        float yOffset = Mathf.Sin(tripPromptFloatTimer) * tripPromptFloatDistance;

        Vector3 newPosition = tripPromptBasePosition + Vector3.up * yOffset;
        tripPromptObject.transform.position = newPosition;
    }

    // ==================== TRIP LOGIC ====================

    private void HandleTrip()
    {
        // Only trip if not already in a critical state
        if (stateMachine.CurrentState != PlayerState.Cutscene &&
            stateMachine.CurrentState != PlayerState.Rappelling &&
            stateMachine.CurrentState != PlayerState.Tripping)
        {
            StartTrip();
        }
    }

    private void StartTrip()
    {
        Debug.Log("PlayerController: StartTrip called! Setting state to Tripping...");

        stateMachine.ChangeState(PlayerState.Tripping);
        Debug.Log($"PlayerController: State changed. Current state: {stateMachine.CurrentState}");

        movement.StopMovement(); // Ensure physics stop
        movement.SetMovementLocked(true); // Lock movement to prevent player from moving
        Debug.Log("PlayerController: Movement locked");

        if (animationController.IsAnimationSystemReady())
        {
            animationController.TriggerTrip();
            Debug.Log("PlayerController: Trip animation triggered");
        }

        // Show the R button prompt above the player
        Debug.Log("PlayerController: Setting trip prompt visible");
        SetTripPromptVisible(true);

        Debug.Log("PlayerController: Player is tripped - waiting for R key to recover...");
        // Player stays tripped until pressing R - no auto-recovery
    }

    private IEnumerator RecoverFromTrip()
    {
        Debug.Log("PlayerController: RecoverFromTrip coroutine started!");

        // Immediately change state to Idle to unlock input
        Debug.Log("PlayerController: Changing state from Tripping to Idle");
        stateMachine.ChangeState(PlayerState.Idle);
        
        // Clear and unlock movement
        Debug.Log("PlayerController: Clearing movement and unlocking");
        movement.StopMovement();
        movement.ClearInput();
        movement.SetMovementLocked(false);
        movement.SetGravityEnabled(true);
        
        // Force animation system to idle state
        if (animationController.IsAnimationSystemReady())
        {
            Debug.Log("PlayerController: Forcing animation to idle");
            animationController.TriggerRecover();

            // Also reset all animation parameters
            animationController.SetSpeed(0f);
            animationController.SetGrounded(true);
            animationController.SetSitting(false);
        }

        // Hide the trip prompt
        SetTripPromptVisible(false);

        // Restore full energy
        if (energySystem != null)
        {
            Debug.Log("PlayerController: Restoring all energy");
            energySystem.RestoreAllEnergy();
        }
        
        // Reset tracking variables
        wasMoving = false;
        
        // Wait one frame to ensure all systems have updated
        yield return null;
        
        Debug.Log("PlayerController: Recovery complete! Now calling RespawnAtLastChair");
        
        // Now respawn at the last chair
        GameManager gameManager = GameManager.GetInstance();
        if (gameManager != null)
        {
            gameManager.RespawnPlayer();
        }
        else
        {
            Debug.LogError("PlayerController: GameManager not found for respawn!");
        }
    }

    void OnDestroy()
    {
        // Clean up trip prompt object
        if (tripPromptObject != null)
        {
            Destroy(tripPromptObject);
        }
    }
}

