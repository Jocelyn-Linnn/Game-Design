using UnityEngine;

/// <summary>
/// Represents a chair that any interactor can sit on
/// Implements IInteractable to work with the interaction system
/// </summary>
public class chair : MonoBehaviour, IInteractable
{
    [Header("Sitting Configuration")]
    [Tooltip("Position where the interactor will be placed when sitting")]
    public Transform sitpoint;

    [Header("UI Prompts")]
    [Tooltip("UI element shown when interactor can sit down and teleport (Press Z)")]
    public GameObject pressZPrompt;

    [Header("Spawn Point Configuration")]
    [Tooltip("Unique identifier for this chair as a spawn point")]
    [SerializeField] private string chairId = "";
    
    [Tooltip("Display name shown in fast travel menu")]
    [SerializeField] private string chairName = "";
    
    [Tooltip("Brief description of the chair's location")]
    [SerializeField] private string locationDescription = "";
    
    [Tooltip("Visual indicator shown when this is the current spawn point")]
    [SerializeField] private GameObject activeSpawnVisual;
    
    [Tooltip("Sound played when player sits on chair (saves progress)")]
    [SerializeField] private AudioClip sitSound;

    [Header("Auto Energy Restore")]
    [Tooltip("Automatically restore player's energy to full when passing by (no key press needed)")]
    [SerializeField] private bool autoRestoreEnergyOnPass = true;

    [Tooltip("Sound played when energy is auto-restored")]
    [SerializeField] private AudioClip energyRestoreSound;

    [Tooltip("Maximum distance from chair center for energy restoration (very small for precise control)")]
    [SerializeField] private float energyRestoreDistance = 0.5f;

    [Tooltip("Automatically save progress when passing by (no key press needed)")]
    [SerializeField] private bool autoSaveProgressOnPass = true;

    private IInteractor currentInteractor = null;
    private bool hasRestoredEnergyForCurrentInteractor = false;
    private bool hasSavedProgressForCurrentInteractor = false;
    private PlayerController currentPlayerInZone = null;

    void Start()
    {
        // Hide prompt at start
        ShowPromptZ(false);

        // Generate chair ID if not set
        if (string.IsNullOrEmpty(chairId))
        {
            chairId = $"chair_{gameObject.name}_{transform.position.x:F0}_{transform.position.y:F0}";
        }

        // Generate chair name if not set
        if (string.IsNullOrEmpty(chairName))
        {
            chairName = gameObject.name;
        }

        // Update spawn point status
        UpdateSpawnPointStatus();
    }

    void Update()
    {
        // Continuously check for energy restoration and progress saving while player is in zone
        if (currentPlayerInZone != null)
        {
            float distanceToChair = Vector3.Distance(currentPlayerInZone.transform.position, transform.position);

            // Auto-restore energy
            if (autoRestoreEnergyOnPass && !hasRestoredEnergyForCurrentInteractor && distanceToChair <= energyRestoreDistance)
            {
                RestorePlayerEnergy(currentPlayerInZone);
                hasRestoredEnergyForCurrentInteractor = true;
                Debug.Log($"Player moved within energy restoration range ({distanceToChair:F2} <= {energyRestoreDistance}) at '{chairName}'");
            }

            // Auto-save progress
            if (autoSaveProgressOnPass && !hasSavedProgressForCurrentInteractor && distanceToChair <= energyRestoreDistance)
            {
                SaveProgress();
                hasSavedProgressForCurrentInteractor = true;
                Debug.Log($"Player moved within progress saving range ({distanceToChair:F2} <= {energyRestoreDistance}) at '{chairName}' - Progress auto-saved!");
            }
        }
    }

    // ==================== IInteractable Implementation ====================

    /// <summary>
    /// Called when an interactor enters the chair's interaction zone
    /// </summary>
    public void OnInteractorEnterZone(IInteractor interactor)
    {
        Debug.Log("Interactor entered chair zone");
        currentInteractor = interactor;

        // Check if this is a player
        PlayerController player = interactor.GetGameObject().GetComponent<PlayerController>();
        if (player != null)
        {
            currentPlayerInZone = player;

            // Show sit prompt if not already sitting
            if (!player.IsSitting())
            {
                ShowPromptZ(true);
                Debug.Log("Showing sit prompt (Press Z)");
            }
        }
    }

    /// <summary>
    /// Called when an interactor exits the chair's interaction zone
    /// </summary>
    public void OnInteractorExitZone(IInteractor interactor)
    {
        // Check if this is a player and if they're sitting
        PlayerController player = interactor.GetGameObject().GetComponent<PlayerController>();

        // Only clear prompts if player is not sitting
        // (when sitting, player should not leave the zone)
        if (player == null || !player.IsSitting())
        {
            ShowPromptZ(false);
            currentInteractor = null;
            hasRestoredEnergyForCurrentInteractor = false; // Reset for next visit
            hasSavedProgressForCurrentInteractor = false; // Reset for next visit
            currentPlayerInZone = null; // Clear player reference
        }
    }

    /// <summary>
    /// Called when an interactor presses the interact button while near the chair
    /// </summary>
    public bool Interact(IInteractor interactor)
    {
        Debug.Log("Chair Interact() called");
        
        // Try to get PlayerController component from the interactor
        PlayerController player = interactor.GetGameObject().GetComponent<PlayerController>();
        
        if (player == null)
        {
            Debug.LogWarning("Chair: Only players can sit on chairs");
            return false;
        }
        
        if (player.IsSitting())
        {
            // Player is already sitting - stand up
            Debug.Log("Player standing up from chair (Press Z)");
            player.LeaveChair();
            ShowPromptZ(true); // 重新顯示坐下提示
            return true;
        }

        // Make the player sit down
        Debug.Log("Making player sit on chair (Press Z)");
        player.SitOnChair(this);

        // Save progress by setting this chair as spawn point
        SaveProgress();

        // Automatically open the fast travel menu when sitting
        OpenFastTravelMenuForPlayer(player);

        // Keep prompt visible (can press Z to stand up)
        ShowPromptZ(true);
        
        return true;
    }

    /// <summary>
    /// Get the GameObject this interactable belongs to
    /// </summary>
    public GameObject GetGameObject()
    {
        return gameObject;
    }

    // ==================== Public Methods ====================

    /// <summary>
    /// Show or hide the "Press Z" prompt
    /// </summary>
    public void ShowPromptZ(bool show)
    {
        if (pressZPrompt != null)
        {
            pressZPrompt.SetActive(show);
        }
    }

    // ==================== Energy Restoration ====================

    /// <summary>
    /// Restore player's energy to full when passing by the chair
    /// </summary>
    private void RestorePlayerEnergy(PlayerController player)
    {
        if (player == null) return;

        PlayerEnergy energySystem = player.GetEnergySystem();
        if (energySystem != null)
        {
            // Check if player actually needs energy restoration
            if (!energySystem.IsEnergyFull())
            {
                energySystem.RestoreAllEnergy();
                
                // Play energy restore sound using SoundManager for consistent volume control
                if (energyRestoreSound != null)
                {
                    SoundManager.GetInstance()?.PlaySound(energyRestoreSound, transform.position, 0.01f);
                }
                
                Debug.Log($"Chair: Auto-restored player's energy to full at '{chairName}'");
            }
        }
    }

    // ==================== Spawn Point Methods ====================

    /// <summary>
    /// Save progress by registering this chair as a spawn point
    /// Uses NewGameManager and SpawnPointSystem
    /// </summary>
    private void SaveProgress()
    {
        Vector3 spawnPosition = sitpoint != null ? sitpoint.position : transform.position;
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Use new system (GameManager + SpawnPointSystem)
        GameManager gameManager = GameManager.GetInstance();
        if (gameManager == null)
        {
            Debug.LogWarning("Chair: GameManager not found! Cannot save progress.");
            return;
        }
        
        // Create spawn point data
        SpawnPointData spawnData = new SpawnPointData(
            chairId,
            chairName,
            locationDescription,
            spawnPosition,
            currentScene
        )
        {
            isOneTimeUse = false,
            isTransportable = true,
            isChairSpawn = true,
            restoresEnergy = true,
            energyRestoreAmount = 0,
            activationSound = sitSound,
            activeVisual = activeSpawnVisual
        };
        
        // Register and activate spawn point
        gameManager.GetSpawnPointSystem()?.AddSpawnPoint(spawnData);
        gameManager.ActivateSpawnPoint(chairId);
        
        // Play sit sound
        if (sitSound != null)
        {
            AudioSource.PlayClipAtPoint(sitSound, transform.position);
        }
        
        // Update visual indicator
        UpdateSpawnPointStatus();
        
        Debug.Log($"Chair: Progress saved at '{chairName}' ({chairId})");
    }

    /// <summary>
    /// Update the visual indicator for spawn point status
    /// Uses NewGameManager
    /// </summary>
    private void UpdateSpawnPointStatus()
    {
        if (activeSpawnVisual == null) return;

        GameManager gameManager = GameManager.GetInstance();
        if (gameManager == null) return;

        SpawnPointData currentSpawn = gameManager.GetCurrentActiveSpawnPoint();
        bool isCurrentSpawn = currentSpawn != null && currentSpawn.spawnPointId == chairId;
        
        activeSpawnVisual.SetActive(isCurrentSpawn);
        
        if (isCurrentSpawn)
        {
            Debug.Log($"Chair: '{chairName}' is now the current spawn point");
        }
    }

    /// <summary>
    /// Get the chair's unique ID
    /// </summary>
    public string GetChairId()
    {
        return chairId;
    }

    /// <summary>
    /// Get the chair's display name
    /// </summary>
    public string GetChairName()
    {
        return chairName;
    }

    /// <summary>
    /// Get the chair's location description
    /// </summary>
    public string GetLocationDescription()
    {
        return locationDescription;
    }

    /// <summary>
    /// Open the fast travel menu for the player when they sit on the chair
    /// </summary>
    private void OpenFastTravelMenuForPlayer(PlayerController player)
    {
        Debug.Log("Chair: Automatically opening fast travel menu for sitting player");

        // Find the FastTravelUI component
        FastTravelUI fastTravelUI = FindFirstObjectByType<FastTravelUI>();

        if (fastTravelUI != null)
        {
            Debug.Log("Chair: FastTravelUI found, opening fast travel menu...");
            fastTravelUI.OpenFastTravelUI();
        }
        else
        {
            Debug.LogWarning("Chair: FastTravelUI not found! Cannot open fast travel menu.");
        }
    }

    /// <summary>
    /// Check if this chair is the current spawn point
    /// Uses NewGameManager
    /// </summary>
    public bool IsCurrentSpawnPoint()
    {
        GameManager gameManager = GameManager.GetInstance();
        if (gameManager == null) return false;

        SpawnPointData currentSpawn = gameManager.GetCurrentActiveSpawnPoint();
        return currentSpawn != null && currentSpawn.spawnPointId == chairId;
    }
}
