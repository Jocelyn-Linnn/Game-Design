using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Fast Travel UI for teleporting to discovered spawn points
/// Shows available teleportable spawn points and allows player to select destination
/// </summary>
public class FastTravelUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Main panel for fast travel UI")]
    [SerializeField] private GameObject fastTravelPanel;

    [Tooltip("Scroll view content for spawn point list")]
    [SerializeField] private Transform spawnPointListContent;

    [Tooltip("Prefab for spawn point list item")]
    [SerializeField] private GameObject spawnPointListItemPrefab;

    [Tooltip("Close button")]
    [SerializeField] private Button closeButton;

    [Tooltip("Title text")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Controls instruction text")]
    [SerializeField] private TextMeshProUGUI controlsText;

    [Header("Map Preview")]
    [Tooltip("RawImage component to display map preview")]
    [SerializeField] private RawImage mapPreviewImage;

    private MapPreviewCamera mapPreviewCamera;

    [Header("Full Map Display (Right Side)")]
    [Tooltip("RawImage component to display minimap on right side (same as bottom-right minimap)")]
    [SerializeField] private RawImage fullMapImage;

    [Tooltip("RawImage component to display fog overlay on minimap")]
    [SerializeField] private RawImage fullMapFogImage;

    [Tooltip("Reference to the minimap camera (MiniMapCamera) - will auto-find if not assigned")]
    [SerializeField] private Camera minimapCamera;

    [Tooltip("Reference to the minimap render texture - will auto-find if not assigned")]
    [SerializeField] private RenderTexture minimapRenderTexture;

    [Tooltip("Parent transform for chair markers on the map")]
    [SerializeField] private Transform chairMarkersParent;

    [Tooltip("Prefab for chair marker (Image component with sprite)")]
    [SerializeField] private GameObject chairMarkerPrefab;

    [Tooltip("Sprite for normal chair marker")]
    [SerializeField] private Sprite chairMarkerSprite;

    [Tooltip("Sprite for selected chair marker (highlighted)")]
    [SerializeField] private Sprite selectedChairMarkerSprite;

    [Tooltip("Size of chair markers on the map")]
    [SerializeField] private Vector2 chairMarkerSize = new Vector2(20f, 20f);

    // --- 新增開始: 玩家位置標記 ---
    [Header("Player Position Marker")]
    [Tooltip("Prefab for the player position marker (usually a simple Image)")]
    [SerializeField] private GameObject playerMarkerPrefab;

    [Tooltip("Sprite for the player position marker (e.g., a dot or player icon)")]
    [SerializeField] private Sprite playerMarkerSprite;

    [Tooltip("Color for the player position marker")]
    [SerializeField] private Color playerMarkerColor = Color.blue;

    [Tooltip("Size of the player position marker on the map")]
    [SerializeField] private Vector2 playerMarkerSize = new Vector2(30f, 30f);
    // --- 新增結束 ---

    [Header("Settings")]
    [Tooltip("Whether to show debug information")]
    [SerializeField] private bool showDebugInfo = false;

    [Tooltip("Whether to show one-time use spawn points")]
    [SerializeField] private bool showOneTimeUseSpawnPoints = false;

    [Tooltip("Whether to show map preview on hover (true) or only on click (false)")]
    [SerializeField] private bool previewOnHover = true;

    // --- 新增開始：自定義重生點排序 ---
    private static readonly string[] customOrderNames = new string[]
    {
        "Prime Cloister",
        "Gloom Hollow",
        "Deep Chasm",
        "Abyssal Terminus"
    };
    // --- 新增結束 ---

    // State
    private bool isOpen = false;
    private List<SpawnPointData> availableSpawnPoints = new List<SpawnPointData>();
    private List<GameObject> spawnPointListItems = new List<GameObject>();
    private SpawnPointData currentlyPreviewedSpawnPoint = null;

    // Keyboard navigation
    private int selectedIndex = 0;
    private float lastNavigationTime = 0f;
    private float navigationCooldown = 0.2f; // Prevent rapid navigation

    // References
    private GameManager gameManager;
    private PlayerController playerController;
    private MinimapFogOfWar fogSystem;


    private Dictionary<string, GameObject> chairMarkers = new Dictionary<string, GameObject>();
    private Vector2 mapWorldMin;
    private Vector2 mapWorldMax;

    // --- 新增開始 ---
    private GameObject playerPositionMarker = null;
    private const string PlayerMarkerId = "CURRENT_PLAYER_POS";
    // --- 新增結束 ---

    void Start()
    {
        Debug.Log("FastTravelUI: Start() called");

        // Get references
        gameManager = GameManager.GetInstance();
        if (gameManager == null)
        {
            Debug.LogError("FastTravelUI: GameManager not found!");
        }
        else
        {
            Debug.Log("FastTravelUI: GameManager found");
        }

        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("FastTravelUI: PlayerController not found");
        }
        else
        {
            Debug.Log("FastTravelUI: PlayerController found");
        }

        // Check if fastTravelPanel is assigned
        if (fastTravelPanel == null)
        {
            Debug.LogError("FastTravelUI: fastTravelPanel is not assigned! Please assign it in the Inspector.");
        }
        else
        {
            Debug.Log($"FastTravelUI: fastTravelPanel assigned: {fastTravelPanel.name}");
        }

        // Initialize UI
        InitializeUI();

        // Initialize map preview system
        InitializeMapPreview();

        // Initialize full map display
        InitializeFullMapDisplay();

        // Subscribe to events
        SubscribeToEvents();

        // Hide UI initially
        SetUIVisibility(false);

        Debug.Log("FastTravelUI: Initialization complete");
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        UnsubscribeFromEvents();
    }

    void Update()
    {
        // Input is now handled by PlayerController
        // This prevents double input detection when M key is pressed
    }

    // ==================== Initialization ====================

    /// <summary>
    /// Initialize the UI components
    /// </summary>
    private void InitializeUI()
    {
        // Set up close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseFastTravelUI);
        }

        // Set title text
        if (titleText != null)
        {
            titleText.text = "Fast Travel";
        }

        // Set controls instruction text - try to find it if not assigned
        if (controlsText == null && fastTravelPanel != null)
        {
            // Try to find by name
            controlsText = FindTextComponent(fastTravelPanel.transform, "ControlsText");
            if (controlsText == null)
            {
                controlsText = FindTextComponent(fastTravelPanel.transform, "InstructionsText");
            }
            if (controlsText == null)
            {
                controlsText = FindTextComponent(fastTravelPanel.transform, "Controls");
            }
        }

        // Setup controls text position and content
        SetupControlsText();

        // Ensure panel is inactive initially
        if (fastTravelPanel != null)
        {
            fastTravelPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Setup controls text - create if needed and position at bottom center
    /// </summary>
    private void SetupControlsText()
    {
        // Try to find controls text if not assigned
        if (controlsText == null && fastTravelPanel != null)
        {
            controlsText = FindTextComponent(fastTravelPanel.transform, "ControlsText");
            if (controlsText == null)
            {
                controlsText = FindTextComponent(fastTravelPanel.transform, "InstructionsText");
            }
            if (controlsText == null)
            {
                controlsText = FindTextComponent(fastTravelPanel.transform, "Controls");
            }
        }

        // Create controls text if it doesn't exist
        if (controlsText == null && fastTravelPanel != null)
        {
            GameObject controlsObj = new GameObject("ControlsText");
            controlsObj.transform.SetParent(fastTravelPanel.transform);
            
            RectTransform rectTransform = controlsObj.AddComponent<RectTransform>();
            controlsText = controlsObj.AddComponent<TextMeshProUGUI>();
            
            // Set position to bottom center
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = new Vector2(0f, 20f); // 20 pixels from bottom
            rectTransform.sizeDelta = new Vector2(400f, 30f);
            
            // Set text properties
            controlsText.text = "UP/DOWN: Select  |  ENTER: Teleport  |  Z: Close";
            controlsText.fontSize = 14f;
            controlsText.alignment = TextAlignmentOptions.Center;
            controlsText.color = Color.white;
            
            if (showDebugInfo)
            {
                Debug.Log("FastTravelUI: Created ControlsText component at bottom center");
            }
        }
        else if (controlsText != null)
        {
            // Update text content
            controlsText.text = "UP/DOWN: Select  |  ENTER: Teleport  |  Z: Close";
            
            // Ensure it's positioned at bottom center
            RectTransform rectTransform = controlsText.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0f);
                rectTransform.anchorMax = new Vector2(0.5f, 0f);
                rectTransform.pivot = new Vector2(0.5f, 0f);
                rectTransform.anchoredPosition = new Vector2(0f, 20f);
            }
            
            // Ensure it's visible
            controlsText.gameObject.SetActive(true);
            
            if (showDebugInfo)
            {
                Debug.Log("FastTravelUI: Controls text set successfully");
            }
        }
    }

    /// <summary>
    /// Initialize map preview system
    /// Creates map preview camera if not assigned
    /// </summary>
    private void InitializeMapPreview()
    {
        // Create map preview camera if not assigned
        if (mapPreviewCamera == null)
        {
            GameObject cameraObject = new GameObject("MapPreviewCamera");
            cameraObject.transform.SetParent(transform);
            mapPreviewCamera = cameraObject.AddComponent<MapPreviewCamera>();

            if (showDebugInfo)
            {
                Debug.Log("FastTravelUI: Created MapPreviewCamera");
            }
        }

        // Check if map preview image is assigned
        if (mapPreviewImage == null)
        {
            Debug.LogWarning("FastTravelUI: MapPreviewImage is not assigned! Please assign a RawImage component in the Inspector to display map preview.");
        }
        else
        {
            // Hide map preview initially
            mapPreviewImage.gameObject.SetActive(false);

            if (showDebugInfo)
            {
                Debug.Log($"FastTravelUI: MapPreviewImage found: {mapPreviewImage.gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Subscribe to relevant events
    /// </summary>
    private void SubscribeToEvents()
    {
        if (gameManager != null)
        {
            SpawnPointSystem spawnSystem = gameManager.GetSpawnPointSystem();
            if (spawnSystem != null)
            {
                spawnSystem.OnSpawnPointActivated += OnSpawnPointActivated;
                spawnSystem.OnSpawnPointDeactivated += OnSpawnPointDeactivated;
            }
        }
    }

    /// <summary>
    /// Unsubscribe from events
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (gameManager != null)
        {
            SpawnPointSystem spawnSystem = gameManager.GetSpawnPointSystem();
            if (spawnSystem != null)
            {
                spawnSystem.OnSpawnPointActivated -= OnSpawnPointActivated;
                spawnSystem.OnSpawnPointDeactivated -= OnSpawnPointDeactivated;
            }
        }
    }

    // ==================== Input Handling ====================
    // Note: M key input is handled by PlayerController to prevent double input detection
    // ESC key is still handled here for convenience

    void LateUpdate()
    {
        if (!isOpen) return;

        // Check for escape key to close
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseFastTravelUI();
            return;
        }

        // Handle keyboard navigation
        HandleKeyboardNavigation();
    }

    // ==================== UI Management ====================

    /// <summary>
    /// Open the fast travel UI
    /// </summary>
    public void OpenFastTravelUI()
    {
        if (isOpen) return;

        if (gameManager == null || fastTravelPanel == null)
        {
            Debug.LogError("FastTravelUI: Missing references!");
            return;
        }

        Time.timeScale = 0f;

        UpdateAvailableSpawnPoints();
        SetUIVisibility(true);
        isOpen = true;
        UpdateSpawnPointList();

        // Setup controls text (ensure it's visible and positioned correctly)
        SetupControlsText();

        // --- 修改開始 ---
        // 原本的程式碼: UpdateMapPreview(currentActive);
        // 修改後: 強制顯示全地圖與迷霧，並隱藏舊的預覽圖
        if (mapPreviewImage != null) mapPreviewImage.gameObject.SetActive(false);
        UpdateFullMapDisplay();
        CreateChairMarkers(); // 確保椅子標記被建立
        // --- 修改結束 ---

        if (availableSpawnPoints.Count == 0)
        {
            Debug.LogWarning("FastTravelUI: No spawn points available.");
        }
    }

    /// <summary>
    /// Close the fast travel UI
    /// </summary>
    public void CloseFastTravelUI()
    {
        if (!isOpen)
        {
            return;
        }

        // Resume the game
        Time.timeScale = 1f;

        // Hide UI
        SetUIVisibility(false);
        isOpen = false;

        // Clear map preview
        ClearMapPreview();

        // Clear full map display
        ClearFullMapDisplay();

        // Clear chair markers
        ClearChairMarkers();

        // Reset selection
        selectedIndex = 0;

        if (showDebugInfo)
        {
            Debug.Log("FastTravelUI: Closed");
        }
    }

    /// <summary>
    /// Set the visibility of the UI
    /// </summary>
    private void SetUIVisibility(bool visible)
    {
        if (fastTravelPanel != null)
        {
            fastTravelPanel.SetActive(visible);
        }
    }

    // ==================== Spawn Point Management ====================

    /// <summary>
    /// Update the list of available spawn points
    /// </summary>
    private void UpdateAvailableSpawnPoints()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("FastTravelUI: GameManager is null, cannot update spawn points");
            availableSpawnPoints.Clear();
            return;
        }

        // Get all teleportable spawn points
        List<SpawnPointData> spawnPoints = gameManager.GetTransportableSpawnPoints();

        if (spawnPoints == null)
        {
            Debug.LogWarning("FastTravelUI: GetTransportableSpawnPoints returned null");
            availableSpawnPoints.Clear();
            return;
        }

        availableSpawnPoints = spawnPoints;

        // Filter out one-time use spawn points if not showing them
        if (!showOneTimeUseSpawnPoints)
        {
            availableSpawnPoints = availableSpawnPoints.Where(sp => !sp.isOneTimeUse).ToList();
        }

        // --- 修改開始 ---
        // 排除名為 "Starting Point" 的重生點 (不區分大小寫)
        availableSpawnPoints = availableSpawnPoints.Where(sp => !sp.displayName.Equals("Starting Point", System.StringComparison.OrdinalIgnoreCase)).ToList();
        // --- 修改結束 ---

        // Sort by scene name, then by display name
        // availableSpawnPoints = availableSpawnPoints.OrderBy(sp => sp.sceneName).ThenBy(sp => sp.displayName).ToList(); // <--- 移除或註釋掉原有的排序

        // --- 修改開始：自定義排序 ---
        // 1. 根據自定義順序排序，如果名稱不在 customOrderNames 中，則將其排在末尾 (透過給予較大的索引值)
        availableSpawnPoints = availableSpawnPoints
            .OrderBy(sp =>
            {
                // 查找名稱在自定義列表中的索引，如果找不到則返回一個很大的數字 (例如 int.MaxValue)，確保其排在末尾。
                // 我們使用 Array.FindIndex 和 StringComparison.OrdinalIgnoreCase 來進行不區分大小寫的查找，以防萬一。
                int index = System.Array.FindIndex(customOrderNames, name => name.Equals(sp.displayName, System.StringComparison.OrdinalIgnoreCase));
                return index == -1 ? int.MaxValue : index;
            })
            // 2. 對於不在自定義列表中的項目 (或名稱相同的項目)，再依場景名稱和顯示名稱排序作為次要排序規則
            .ThenBy(sp => sp.sceneName)
            .ThenBy(sp => sp.displayName)
            .ToList();
        // --- 修改結束 ---

        if (showDebugInfo)
        {
            Debug.Log($"FastTravelUI: Updated available spawn points, found {availableSpawnPoints.Count} teleportable spawn points");
        }
    }

    /// <summary>
    /// Update the spawn point list UI
    /// </summary>
    private void UpdateSpawnPointList()
    {
        // Clear existing list items
        ClearSpawnPointList();

        // Create new list items
        foreach (SpawnPointData spawnPoint in availableSpawnPoints)
        {
            CreateSpawnPointListItem(spawnPoint);
        }

        if (showDebugInfo)
        {
            Debug.Log($"FastTravelUI: Updated spawn point list with {availableSpawnPoints.Count} items");
        }
    }

    /// <summary>
    /// Clear the spawn point list
    /// </summary>
    private void ClearSpawnPointList()
    {
        foreach (GameObject item in spawnPointListItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnPointListItems.Clear();
    }

    /// <summary>
    /// Create a spawn point list item
    /// </summary>
    private void CreateSpawnPointListItem(SpawnPointData spawnPoint)
    {
        if (spawnPointListItemPrefab == null)
        {
            Debug.LogError("FastTravelUI: spawnPointListItemPrefab is not assigned! Please assign the prefab in the Inspector.");
            return;
        }

        if (spawnPointListContent == null)
        {
            Debug.LogError("FastTravelUI: spawnPointListContent is not assigned! Please assign the content transform in the Inspector.");
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("FastTravelUI: Attempted to create list item for null spawn point");
            return;
        }

        // Create the list item
        GameObject listItem = Instantiate(spawnPointListItemPrefab, spawnPointListContent);
        if (listItem == null)
        {
            Debug.LogError("FastTravelUI: Failed to instantiate spawn point list item");
            return;
        }

        spawnPointListItems.Add(listItem);

        // Set up the list item
        SetupSpawnPointListItem(listItem, spawnPoint);

        // Add mouse event handlers for map preview
        AddMapPreviewEventHandlers(listItem, spawnPoint);

        if (showDebugInfo)
        {
            Debug.Log($"FastTravelUI: Created list item for spawn point '{spawnPoint.spawnPointId}'");
        }
    }

    /// <summary>
    /// Set up a spawn point list item
    /// </summary>
    private void SetupSpawnPointListItem(GameObject listItem, SpawnPointData spawnPoint)
    {
        if (showDebugInfo)
        {
            Debug.Log($"FastTravelUI: Setting up list item for '{spawnPoint.displayName}' (ID: {spawnPoint.spawnPointId})");
        }

        // Find UI components - try multiple methods
        Button teleportButton = listItem.GetComponentInChildren<Button>();

        // Try to find text components by name (direct children first, then recursive)
        TextMeshProUGUI nameText = FindTextComponent(listItem.transform, "NameText");
        TextMeshProUGUI sceneText = FindTextComponent(listItem.transform, "SceneText");
        TextMeshProUGUI descriptionText = FindTextComponent(listItem.transform, "DescriptionText");

        // Try to find active indicator
        Image activeIndicator = FindImageComponent(listItem.transform, "ActiveIndicator");

        // Set text content - prioritize button text for simple prefabs
        TextMeshProUGUI buttonText = teleportButton?.GetComponentInChildren<TextMeshProUGUI>();

        if (nameText != null)
        {
            nameText.text = spawnPoint.displayName;
            if (showDebugInfo)
            {
                Debug.Log($"FastTravelUI: Set name text to '{spawnPoint.displayName}'");
            }
        }
        else if (buttonText != null)
        {
            // Fallback: use button text for simple prefabs
            buttonText.text = spawnPoint.displayName;
            if (showDebugInfo)
            {
                Debug.Log($"FastTravelUI: Used button text for '{spawnPoint.displayName}' (simple prefab)");
            }
        }
        else
        {
            Debug.LogWarning($"FastTravelUI: No text component found for '{spawnPoint.displayName}'");
        }

        // Optional text components (these are optional, silently skip if not found)
        if (sceneText != null)
        {
            sceneText.text = spawnPoint.sceneName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = spawnPoint.locationDescription;
        }

        // Optional active indicator (this is optional, silently skip if not found)
        if (activeIndicator != null)
        {
            activeIndicator.gameObject.SetActive(spawnPoint.isActive);
        }

        // Set up teleport button
        if (teleportButton != null)
        {
            teleportButton.onClick.RemoveAllListeners();
            teleportButton.onClick.AddListener(() => TeleportToSpawnPoint(spawnPoint.spawnPointId));

            // Disable button if spawn point is in current scene and player is already there
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (spawnPoint.sceneName == currentScene && playerController != null)
            {
                float distance = Vector3.Distance(playerController.transform.position, spawnPoint.position);
                if (distance < 1f)
                {
                    teleportButton.interactable = false;
                }
            }

            if (showDebugInfo)
            {
                Debug.Log($"FastTravelUI: Button configured for '{spawnPoint.displayName}'");
            }
        }
        else
        {
            Debug.LogError($"FastTravelUI: Button not found in list item prefab for '{spawnPoint.displayName}'!");
        }
    }

    /// <summary>
    /// Add mouse event handlers to list item for map preview
    /// </summary>
    private void AddMapPreviewEventHandlers(GameObject listItem, SpawnPointData spawnPoint)
    {
        // Get or add EventTrigger component
        EventTrigger eventTrigger = listItem.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = listItem.AddComponent<EventTrigger>();
        }

        // Clear existing entries
        eventTrigger.triggers.Clear();

        if (previewOnHover)
        {
            // Add pointer enter event (hover)
            EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
            pointerEnter.eventID = EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((data) => { OnListItemPointerEnter(spawnPoint); });
            eventTrigger.triggers.Add(pointerEnter);

            // Add pointer exit event
            EventTrigger.Entry pointerExit = new EventTrigger.Entry();
            pointerExit.eventID = EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) => { OnListItemPointerExit(); });
            eventTrigger.triggers.Add(pointerExit);
        }

        // Add pointer click event (always show preview on click)
        EventTrigger.Entry pointerClick = new EventTrigger.Entry();
        pointerClick.eventID = EventTriggerType.PointerClick;
        pointerClick.callback.AddListener((data) => { OnListItemPointerClick(spawnPoint); });
        eventTrigger.triggers.Add(pointerClick);
    }

    /// <summary>
    /// Handle pointer enter event (hover)
    /// </summary>
    private void OnListItemPointerEnter(SpawnPointData spawnPoint)
    {
        // No longer updating map preview on hover
        // Right side always shows full map with fog
    }

    /// <summary>
    /// Handle pointer exit event
    /// </summary>
    private void OnListItemPointerExit()
    {
        // No action needed - full map always visible
    }

    /// <summary>
    /// Handle pointer click event
    /// </summary>
    private void OnListItemPointerClick(SpawnPointData spawnPoint)
    {
        // No longer updating map preview on click
        // Right side always shows full map with fog
        // Just teleport directly if clicked
        if (spawnPoint != null)
        {
            TeleportToSpawnPoint(spawnPoint.spawnPointId);
        }
    }

    /// <summary>
    /// Clear map preview
    /// </summary>
    private void ClearMapPreview()
    {
        if (mapPreviewImage != null)
        {
            mapPreviewImage.gameObject.SetActive(false);
            mapPreviewImage.texture = null;
        }
        currentlyPreviewedSpawnPoint = null;
    }

    /// <summary>
    /// Find a TextMeshProUGUI component by GameObject name (recursive search)
    /// </summary>
    private TextMeshProUGUI FindTextComponent(Transform parent, string name)
    {
        // Try direct child first
        Transform child = parent.Find(name);
        if (child != null)
        {
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text != null) return text;
        }

        // Try recursive search in all children
        TextMeshProUGUI[] allTexts = parent.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var text in allTexts)
        {
            if (text.gameObject.name == name)
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>
    /// Find an Image component by GameObject name (recursive search)
    /// </summary>
    private Image FindImageComponent(Transform parent, string name)
    {
        // Try direct child first
        Transform child = parent.Find(name);
        if (child != null)
        {
            Image image = child.GetComponent<Image>();
            if (image != null) return image;
        }

        // Try recursive search in all children
        Image[] allImages = parent.GetComponentsInChildren<Image>();
        foreach (var image in allImages)
        {
            if (image.gameObject.name == name)
            {
                return image;
            }
        }

        return null;
    }

    [Header("Teleport Animation")]
    [Tooltip("Duration of the zoom-in/out animation")]
    [SerializeField] private float zoomAnimationDuration = 0.4f;

    [Tooltip("How much to zoom out. 2 means zooming out to twice the normal camera size.")]
    [SerializeField] private float zoomOutFactor = 1.5f;

    // ==================== Teleportation ====================

    /// <summary>
    /// Starts the teleport sequence, which includes the zoom animation.
    /// </summary>
    private void TeleportToSpawnPoint(string spawnPointId)
    {
        if (gameManager == null)
        {
            Debug.LogWarning("FastTravelUI: GameManager not found, cannot teleport");
            return;
        }

        // The coroutine will handle closing the UI and other state changes.
        StartCoroutine(AnimateTeleport(spawnPointId));
    }

    /// <summary>
    /// Coroutine to handle the zoom-out, teleport, and zoom-in animation.
    /// </summary>
    private IEnumerator AnimateTeleport(string spawnPointId)
    {
        // Hide the UI panel immediately to show the game world during animation.
        if (fastTravelPanel != null)
        {
            fastTravelPanel.SetActive(false);
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("FastTravelUI: Main Camera not found! Cannot perform zoom animation. Teleporting instantly.");
            gameManager.TeleportToSpawnPoint(spawnPointId);
            CloseFastTravelUI();
            yield break;
        }

        float originalSize = mainCamera.orthographicSize;
        float zoomedOutSize = originalSize * zoomOutFactor;

        // --- Zoom Out ---
        float timer = 0f;
        while (timer < zoomAnimationDuration)
        {
            // Using a simple Ease-Out curve for a smoother feel
            float progress = timer / zoomAnimationDuration;
            mainCamera.orthographicSize = Mathf.Lerp(originalSize, zoomedOutSize, 1 - Mathf.Pow(1 - progress, 3));
            timer += Time.unscaledDeltaTime; // Use unscaled time as the game is paused
            yield return null;
        }
        mainCamera.orthographicSize = zoomedOutSize;

        // --- Teleport Player ---
        if (showDebugInfo)
        {
            Debug.Log($"FastTravelUI: Teleporting to spawn point '{spawnPointId}'");
        }
        bool success = gameManager.TeleportToSpawnPoint(spawnPointId);

        if (!success)
        {
            Debug.LogWarning($"FastTravelUI: Failed to teleport to spawn point '{spawnPointId}'");
            // If teleport fails, we still zoom back in before closing.
        }
        else
        {
            // If sitting, leave the chair post-teleport
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerController>();
            }
            if (playerController != null && playerController.IsSitting())
            {
                playerController.LeaveChair();
            }
        }

        // A brief pause at the zoomed-out level can make the transition feel better.
        yield return new WaitForSecondsRealtime(0.1f);

        // --- Zoom In ---
        timer = 0f;
        while (timer < zoomAnimationDuration)
        {
            // Using a simple Ease-In curve
            float progress = timer / zoomAnimationDuration;
            mainCamera.orthographicSize = Mathf.Lerp(zoomedOutSize, originalSize, progress * progress * progress);
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        mainCamera.orthographicSize = originalSize;

        // --- Final Cleanup ---
        // CloseFastTravelUI handles un-pausing the game and other cleanup.
        CloseFastTravelUI();
    }


    // ==================== Event Handlers ====================

    /// <summary>
    /// Called when a spawn point is activated
    /// </summary>
    private void OnSpawnPointActivated(SpawnPointData spawnPoint)
    {
        if (isOpen)
        {
            UpdateSpawnPointList();
        }
    }

    /// <summary>
    /// Called when a spawn point is deactivated
    /// </summary>
    private void OnSpawnPointDeactivated(SpawnPointData spawnPoint)
    {
        if (isOpen)
        {
            UpdateSpawnPointList();
        }
    }

    // ==================== Public Methods ====================

    /// <summary>
    /// Check if the fast travel UI is open
    /// </summary>
    public bool IsOpen()
    {
        return isOpen;
    }

    /// <summary>
    /// Get the number of available spawn points
    /// </summary>
    public int GetAvailableSpawnPointCount()
    {
        return availableSpawnPoints.Count;
    }

    /// <summary>
    /// Refresh the spawn point list
    /// </summary>
    public void RefreshSpawnPointList()
    {
        if (isOpen)
        {
            UpdateAvailableSpawnPoints();
            UpdateSpawnPointList();
        }
    }

    // ==================== Full Map Display ====================

    /// <summary>
    /// Initialize minimap display system (uses same camera and render texture as bottom-right minimap)
    /// </summary>
    private void InitializeFullMapDisplay()
    {
        // Find fog system
        fogSystem = MinimapFogOfWar.GetInstance();
        if (fogSystem == null)
        {
            fogSystem = FindFirstObjectByType<MinimapFogOfWar>();
        }

        // Find minimap camera if not assigned
        if (minimapCamera == null)
        {
            GameObject minimapCameraObj = GameObject.Find("MiniMapCamera");
            if (minimapCameraObj != null)
            {
                minimapCamera = minimapCameraObj.GetComponent<Camera>();
            }

            if (minimapCamera == null)
            {
                // Try to find by tag or component
                Camera[] cameras = FindObjectsOfType<Camera>();
                foreach (Camera cam in cameras)
                {
                    if (cam.name.Contains("MiniMap") || cam.name.Contains("Minimap"))
                    {
                        minimapCamera = cam;
                        break;
                    }
                }
            }

            if (showDebugInfo)
            {
                if (minimapCamera != null)
                {
                    Debug.Log($"FastTravelUI: Found minimap camera: {minimapCamera.name}");
                }
                else
                {
                    Debug.LogWarning("FastTravelUI: Minimap camera not found! Please assign it in Inspector.");
                }
            }
        }

        // Find minimap render texture if not assigned
        if (minimapRenderTexture == null && minimapCamera != null)
        {
            minimapRenderTexture = minimapCamera.targetTexture;

            if (showDebugInfo)
            {
                if (minimapRenderTexture != null)
                {
                    Debug.Log($"FastTravelUI: Found minimap render texture: {minimapRenderTexture.name}");
                }
                else
                {
                    Debug.LogWarning("FastTravelUI: Minimap render texture not found!");
                }
            }
        }

        // Set up minimap image (use same render texture as bottom-right minimap)
        if (fullMapImage != null && minimapRenderTexture != null)
        {
            fullMapImage.texture = minimapRenderTexture;
            fullMapImage.uvRect = new Rect(0, 0, 1, 0.8f); // Same UV rect as minimap
            fullMapImage.gameObject.SetActive(false);

            if (showDebugInfo)
            {
                Debug.Log("FastTravelUI: Set up minimap image with render texture");
            }
        }

        // Set up fog overlay (same as bottom-right minimap)
        if (fullMapFogImage != null)
        {
            if (fogSystem != null)
            {
                Texture2D fogTexture = fogSystem.GetFogTexture();
                if (fogTexture != null)
                {
                    fullMapFogImage.texture = fogTexture;
                    fullMapFogImage.uvRect = new Rect(0, 0, 1, 1);
                    fullMapFogImage.color = Color.white;
                    fullMapFogImage.gameObject.SetActive(false);

                    if (showDebugInfo)
                    {
                        Debug.Log("FastTravelUI: Set up fog overlay");
                    }
                }
            }
        }

        // Create chair markers parent if not assigned
        if (chairMarkersParent == null && fullMapImage != null)
        {
            GameObject markersParent = new GameObject("ChairMarkersParent");
            markersParent.transform.SetParent(fullMapImage.transform);
            RectTransform markersRect = markersParent.AddComponent<RectTransform>();
            markersRect.anchorMin = Vector2.zero;
            markersRect.anchorMax = Vector2.one;
            markersRect.sizeDelta = Vector2.zero;
            markersRect.anchoredPosition = Vector2.zero;
            chairMarkersParent = markersParent.transform;

            if (showDebugInfo)
            {
                Debug.Log("FastTravelUI: Created ChairMarkersParent");
            }
        }
    }

    /// <summary>
    /// Update minimap display on right side (same as bottom-right minimap)
    /// </summary>

private void UpdateFullMapDisplay()
    {
        // 顯示全地圖 (RenderTexture)
        if (fullMapImage != null && minimapRenderTexture != null)
        {
            fullMapImage.texture = minimapRenderTexture;
            fullMapImage.gameObject.SetActive(true);
        }

        // 顯示迷霧 (Fog Overlay)
        if (fullMapFogImage != null)
        {
            if (fogSystem == null) fogSystem = MinimapFogOfWar.GetInstance(); // 確保獲取系統

            if (fogSystem != null)
            {
                Texture2D fogTexture = fogSystem.GetFogTexture();
                if (fogTexture != null)
                {
                    fullMapFogImage.texture = fogTexture;
                    fullMapFogImage.gameObject.SetActive(true); // 確保它是開啟的
                    // 確保迷霧在最上層 (但在椅子標記之下，視你的層級需求而定)
                    fullMapFogImage.transform.SetAsLastSibling();
                }
            }
        }

        // 確保椅子標記父物件在迷霧之上 (這樣才看得到目標點)
        if (chairMarkersParent != null)
        {
            chairMarkersParent.SetAsLastSibling();
        }
    }

    /// <summary>
    /// Clear full map display
    /// </summary>
    private void ClearFullMapDisplay()
    {
        if (fullMapImage != null)
        {
            fullMapImage.gameObject.SetActive(false);
        }

        if (fullMapFogImage != null)
        {
            fullMapFogImage.gameObject.SetActive(false);
        }
    }

    // ==================== Keyboard Navigation ====================

    /// <summary>
    /// Handle keyboard navigation for selecting spawn points
    /// </summary>
    private void HandleKeyboardNavigation()
    {
        if (availableSpawnPoints.Count == 0) return;

        // Check for navigation input
        bool navigateUp = Input.GetKeyDown(KeyCode.UpArrow);
        bool navigateDown = Input.GetKeyDown(KeyCode.DownArrow);

        // Handle continuous navigation with cooldown
        if (Time.unscaledTime - lastNavigationTime < navigationCooldown)
        {
            navigateUp = navigateUp && Input.GetKey(KeyCode.UpArrow);
            navigateDown = navigateDown && Input.GetKey(KeyCode.DownArrow);
        }

        if (navigateUp || navigateDown)
        {
            lastNavigationTime = Time.unscaledTime;

            if (navigateUp)
            {
                selectedIndex--;
                if (selectedIndex < 0)
                {
                    selectedIndex = availableSpawnPoints.Count - 1;
                }
            }
            else if (navigateDown)
            {
                selectedIndex++;
                if (selectedIndex >= availableSpawnPoints.Count)
                {
                    selectedIndex = 0;
                }
            }

            UpdateSelection();
        }

        // Handle selection confirmation (Enter/Space)
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (selectedIndex >= 0 && selectedIndex < availableSpawnPoints.Count)
            {
                SpawnPointData selectedSpawnPoint = availableSpawnPoints[selectedIndex];
                TeleportToSpawnPoint(selectedSpawnPoint.spawnPointId);
            }
        }
    }

    /// <summary>
    /// Update visual selection state
    /// </summary>
    private void UpdateSelection()
    {
        if (availableSpawnPoints.Count == 0) return;

        // Update visual feedback for selected item
        for (int i = 0; i < spawnPointListItems.Count; i++)
        {
            if (spawnPointListItems[i] == null) continue;

            // Find selection indicator or button
            Image selectionIndicator = FindImageComponent(spawnPointListItems[i].transform, "SelectionIndicator");
            Button itemButton = spawnPointListItems[i].GetComponentInChildren<Button>();

            bool isSelected = (i == selectedIndex);

            // Update selection indicator
            if (selectionIndicator != null)
            {
                selectionIndicator.gameObject.SetActive(isSelected);
            }

            // Update button colors or other visual feedback
            if (itemButton != null)
            {
                ColorBlock colors = itemButton.colors;
                if (isSelected)
                {
                    colors.normalColor = new Color(0.8f, 0.8f, 1f, 1f); // Light blue when selected
                }
                else
                {
                    colors.normalColor = Color.white;
                }
                itemButton.colors = colors;
            }
        }

        // Update chair markers on map
        UpdateChairMarkersSelection();

        // Update preview for selected spawn point
        if (selectedIndex >= 0 && selectedIndex < availableSpawnPoints.Count)
        {
            SpawnPointData selectedSpawnPoint = availableSpawnPoints[selectedIndex];
            if (showDebugInfo)
            {
                Debug.Log($"FastTravelUI: Selected spawn point '{selectedSpawnPoint.displayName}' (index {selectedIndex})");
            }
        }
    }

    // ==================== Chair Markers on Map ====================

    /// <summary>
    /// Create chair markers on the full map
    /// </summary>
    private void CreateChairMarkers()
    {
        if (fullMapImage == null || chairMarkersParent == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("FastTravelUI: Cannot create chair markers - fullMapImage or chairMarkersParent is null");
            }
            return;
        }

        // Clear existing markers
        ClearChairMarkers();

        // Create marker for each spawn point
        foreach (SpawnPointData spawnPoint in availableSpawnPoints)
        {
            CreateChairMarker(spawnPoint);
        }

        // --- 修改開始: 建立玩家位置標記 ---
        CreatePlayerPositionMarker();
        // --- 修改結束 ---

        if (showDebugInfo)
        {
            Debug.Log($"FastTravelUI: Created {chairMarkers.Count} chair markers on map");
        }
    }

    /// <summary>
    /// Create a single chair marker for a spawn point
    /// </summary>
    private void CreateChairMarker(SpawnPointData spawnPoint)
    {
        if (spawnPoint == null) return;

        // Create marker GameObject
        GameObject marker;
        if (chairMarkerPrefab != null)
        {
            marker = Instantiate(chairMarkerPrefab, chairMarkersParent);
        }
        else
        {
            // Create simple marker if no prefab
            marker = new GameObject($"ChairMarker_{spawnPoint.spawnPointId}");
            marker.transform.SetParent(chairMarkersParent);

            // Add Image component
            Image markerImage = marker.AddComponent<Image>();
            if (chairMarkerSprite != null)
            {
                markerImage.sprite = chairMarkerSprite;
            }
            else
            {
                // Create a simple colored circle if no sprite
                markerImage.color = Color.yellow;
            }
        }

        // Set up RectTransform
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        if (markerRect == null)
        {
            markerRect = marker.AddComponent<RectTransform>();
        }

        // Set size
        markerRect.sizeDelta = chairMarkerSize;
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);

        // Convert world position to UI position
        Vector2 uiPosition = WorldToMapUI(spawnPoint.position);
        markerRect.anchoredPosition = uiPosition;

        // Store marker reference
        chairMarkers[spawnPoint.spawnPointId] = marker;

        if (showDebugInfo)
        {
            Debug.Log($"FastTravelUI: Created marker for '{spawnPoint.displayName}' at UI position {uiPosition}");
        }
    }

    /// <summary>
    /// Convert world position to UI position on the minimap
    /// </summary>
    private Vector2 WorldToMapUI(Vector3 worldPosition)
    {
        if (fullMapImage == null)
        {
            return Vector2.zero;
        }

        RectTransform mapRect = fullMapImage.rectTransform;
        if (mapRect == null)
        {
            return Vector2.zero;
        }

        // Declare variables at method scope to avoid conflicts
        float normalizedX;
        float normalizedY;
        float mapWidth = mapRect.rect.width;
        float mapHeight = mapRect.rect.height;

        // Use minimap camera bounds if available
        if (minimapCamera != null)
        {
            float orthoSize = minimapCamera.orthographicSize;
            Vector3 cameraPos = minimapCamera.transform.position;

            // Calculate world bounds from camera
            float worldMinX = cameraPos.x - orthoSize;
            float worldMaxX = cameraPos.x + orthoSize;
            float worldMinY = cameraPos.y - orthoSize;
            float worldMaxY = cameraPos.y + orthoSize;

            // Normalize world position (0-1 range)
            normalizedX = (worldPosition.x - worldMinX) / (worldMaxX - worldMinX);
            normalizedY = (worldPosition.y - worldMinY) / (worldMaxY - worldMinY);
        }
        else
        {
            // Fallback: use stored world bounds
            Vector2 worldSize = mapWorldMax - mapWorldMin;

            // Normalize world position (0-1 range)
            normalizedX = (worldPosition.x - mapWorldMin.x) / worldSize.x;
            normalizedY = (worldPosition.y - mapWorldMin.y) / worldSize.y;
        }

        // Convert to UI coordinates (common for both paths)
        float uiX = (normalizedX - 0.5f) * mapWidth;
        float uiY = (normalizedY - 0.5f) * mapHeight;

        return new Vector2(uiX, uiY);
    }

    /// <summary>
    /// Create a marker for the player's current position (if PlayerController is available)
    /// </summary>
    private void CreatePlayerPositionMarker()
    {
        if (playerController == null || fullMapImage == null || chairMarkersParent == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("FastTravelUI: Cannot create player marker - missing PlayerController, fullMapImage, or chairMarkersParent.");
            }
            return;
        }

        // 清除舊的玩家標記
        if (playerPositionMarker != null)
        {
            Destroy(playerPositionMarker);
            playerPositionMarker = null;
        }

        // --- Marker Creation Logic ---
        GameObject marker;
        if (playerMarkerPrefab != null)
        {
            marker = Instantiate(playerMarkerPrefab, chairMarkersParent);
        }
        else
        {
            // Create simple marker if no prefab
            marker = new GameObject($"PlayerMarker_{PlayerMarkerId}");
            marker.transform.SetParent(chairMarkersParent);

            // Add Image component
            Image markerImage = marker.AddComponent<Image>();
            markerImage.color = playerMarkerColor;

            if (playerMarkerSprite != null)
            {
                markerImage.sprite = playerMarkerSprite;
            }
        }

        playerPositionMarker = marker;

        // Set up RectTransform
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        if (markerRect == null)
        {
            markerRect = marker.AddComponent<RectTransform>();
        }

        // Set size
        markerRect.sizeDelta = playerMarkerSize;
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);

        // Convert world position to UI position
        Vector2 uiPosition = WorldToMapUI(playerController.transform.position);
        markerRect.anchoredPosition = uiPosition;

        // 確保玩家標記在最上層，這樣它就不會被霧或椅子標記遮擋
        playerPositionMarker.transform.SetAsLastSibling();

        if (showDebugInfo)
        {
            Debug.Log($"FastTravelUI: Created player marker at UI position {uiPosition}");
        }
    }

    /// <summary>
    /// Update chair markers selection state
    /// </summary>
    private void UpdateChairMarkersSelection()
    {
        if (availableSpawnPoints.Count == 0 || selectedIndex < 0 || selectedIndex >= availableSpawnPoints.Count)
        {
            // Deselect all markers
            foreach (var marker in chairMarkers.Values)
            {
                if (marker != null)
                {
                    UpdateMarkerVisual(marker, false);
                }
            }
            return;
        }

        SpawnPointData selectedSpawnPoint = availableSpawnPoints[selectedIndex];

        // Update all markers
        foreach (var kvp in chairMarkers)
        {
            if (kvp.Value == null) continue;

            bool isSelected = (kvp.Key == selectedSpawnPoint.spawnPointId);
            UpdateMarkerVisual(kvp.Value, isSelected);
        }
    }

    /// <summary>
    /// Update visual appearance of a marker
    /// </summary>
    private void UpdateMarkerVisual(GameObject marker, bool isSelected)
    {
        Image markerImage = marker.GetComponent<Image>();
        if (markerImage == null) return;

        if (isSelected)
        {
            // Use selected sprite or change color
            if (selectedChairMarkerSprite != null)
            {
                markerImage.sprite = selectedChairMarkerSprite;
            }
            else
            {
                markerImage.color = new Color(1f, 0.8f, 0f, 1f); // Bright orange/yellow when selected
            }

            // Make selected marker larger
            RectTransform markerRect = marker.GetComponent<RectTransform>();
            if (markerRect != null)
            {
                markerRect.sizeDelta = chairMarkerSize * 1.5f;
            }
        }
        else
        {
            // Use normal sprite or color
            if (chairMarkerSprite != null)
            {
                markerImage.sprite = chairMarkerSprite;
            }
            else
            {
                markerImage.color = Color.yellow; // Normal yellow
            }

            // Normal size
            RectTransform markerRect = marker.GetComponent<RectTransform>();
            if (markerRect != null)
            {
                markerRect.sizeDelta = chairMarkerSize;
            }
        }
    }

    /// <summary>
    /// Clear all chair markers
    /// </summary>
    private void ClearChairMarkers()
    {
        foreach (var marker in chairMarkers.Values)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        chairMarkers.Clear();

        // --- 修改開始: 清除玩家位置標記 ---
        if (playerPositionMarker != null)
        {
            Destroy(playerPositionMarker);
            playerPositionMarker = null;
        }
        // --- 修改結束 ---

    }

    // ==================== Debug Methods ====================

    /// <summary>
    /// Get debug information about the fast travel UI
    /// </summary>
    public string GetDebugInfo()
    {
        return $"FastTravelUI: Open={isOpen}, AvailableSpawnPoints={availableSpawnPoints.Count}, ShowOneTimeUse={showOneTimeUseSpawnPoints}, SelectedIndex={selectedIndex}, ChairMarkers={chairMarkers.Count}";
    }
}