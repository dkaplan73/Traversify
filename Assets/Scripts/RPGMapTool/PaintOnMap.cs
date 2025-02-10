// Assets/Scripts/RPGMapTool/PaintOnMap.cs
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SimpleFileBrowser; // Ensure you have this package or remove if unused

namespace RPGMapTool
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class PaintOnMap : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler
    {
        #region Inspector Fields

        [Header("=== UI BUTTONS ===")]
        public Button SaveAnnotationButton;
        public Button OpenBrowserButton;
        public Button ClearButton;
        public Button IncreaseBrushSizeButton;
        public Button DecreaseBrushSizeButton;
        public Button ZoomInButton;
        public Button ZoomOutButton;
        public Button PanButton;
        public Button MagicWandButton; // Button to toggle Magic Wand/Paintbrush
        public Button UndoButton;
        public Button BorderColorButton; // Button to toggle Eyedropper
        public Button ThickenBlobButton;

        [Header("=== DROPDOWNS ===")]
        [Tooltip("Appends the selected text to the annotation filename.")]
        public Dropdown MapTypeDropDown;
        [Tooltip("Switch paint color & overlay based on selection (e.g. 'Traversable' vs. 'Non-Traversable').")]
        public Dropdown DrawTypeDropDown;

        [Header("=== INPUT FIELDS ===")]
        public InputField SetThresholdField;
        public InputField BorderThicknessField;
        public InputField BorderColorThresholdField;

        [Header("=== TOGGLES ===")]
        public Toggle TraversableToggle;
        public Toggle NonTraversableToggle;

        [Header("=== SAVE FOLDER ===")]
        [Tooltip("Folder path for annotation PNG saves.")]
        public string SaveFolder = "Assets/Annotations/";

        [Header("=== MAP & OVERLAYS ===")]
        public RawImage BaseMap;
        [Tooltip("The RectTransform that holds the map and overlays (e.g., PanLayer)")]
        public RectTransform PanContainer;
        public RawImage TraversableDrawLayer;
        public RawImage NonTraversableDrawLayer;

        [Header("=== UI Elements ===")]
        public Image ColorSample; // Assign the ColorSample Image here
        public List<Image> UIIconElements; // Assign all UI icons that need dynamic coloring

        #endregion

        #region Drawing Settings

        [Header("=== Drawing Settings ===")]
        public Color paintColor = Color.green;
        public int brushSize = 10;

        [Range(0f, 1f)]
        public float colorThreshold = 0.1f;

        public float borderThickness = 1f;
        public Color borderColor = Color.black;

        [Range(0f, 1f)]
        public float borderColorThreshold = 0.1f;

        #endregion

        #region Pan & Zoom

        [Header("=== Pan & Zoom ===")]
        public float minZoom = 0.5f;
        public float maxZoom = 3.0f;
        public float zoomSpeed = 0.1f;

        #endregion

        #region Undo System

        [Header("=== Undo Manager (Internal) ===")]
        [Tooltip("If true, multi-level undo is enabled.")]
        public bool enableUndo = true;

        private UndoManager undoManager;

        #endregion

        #region Private Fields

        private RectTransform managerRect;
        private RawImage activeOverlay;
        private Texture2D activeTexture;

        private bool isPainting = true; // Default to painting
        private bool isMagicWandActive = false; // Default to OFF
        private bool isEyedropperActive = false;
        private bool strokeStateSaved = false;

        // Pan
        private bool isPanning = false;
        private Vector2 panStartPointerPosition;
        private Vector2 panStartContainerPosition;

        // Last pointer position on the texture
        private Vector2 lastTexCoord;

        // MapType from MapTypeDropDown
        private string currentMapType = "DefaultMap";
        // DrawType from DrawTypeDropDown
        private string currentDrawType = "Traversable";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            managerRect = GetComponent<RectTransform>();

            // Ensure PaintManager's Image is transparent yet active so it receives pointer events.
            Image img = GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0, 0, 0, 0);
                Debug.Log("[PaintOnMap] Image component color set to transparent.");
            }
            else
            {
                Debug.LogError("[PaintOnMap] PaintManager is missing an Image component.");
            }

            if (PanContainer == null)
            {
                Debug.LogError("[PaintOnMap] PanContainer (PanLayer) is not assigned.");
            }

            undoManager = new UndoManager();
            // Initialize MagicWandTool if necessary or handle within this script

            // Initialize ColorSample Image
            if (ColorSample == null)
            {
                Debug.LogError("[PaintOnMap] ColorSample Image is not assigned in the Inspector.");
            }

            // Disable all UI buttons, fields, and toggles except OpenBrowserButton initially
            DisableAllUI();
            if (OpenBrowserButton)
                OpenBrowserButton.interactable = true;
        }

        private void Start()
        {
            Debug.Log("[PaintOnMap] Start() => Initializing.");

            if (!ValidateReferences())
            {
                Debug.LogError("[PaintOnMap] Missing references. Init aborted.");
                return;
            }

            // Disable raycasts on BaseMap and overlays so pointer events reach PaintManager.
            if (BaseMap)
            {
                BaseMap.raycastTarget = false;
                Debug.Log("[PaintOnMap] BaseMap raycast target disabled.");
            }
            if (TraversableDrawLayer)
            {
                TraversableDrawLayer.raycastTarget = false;
                TraversableDrawLayer.gameObject.SetActive(true);
                Debug.Log("[PaintOnMap] TraversableDrawLayer activated and raycast target disabled.");
            }
            if (NonTraversableDrawLayer)
            {
                NonTraversableDrawLayer.raycastTarget = false;
                NonTraversableDrawLayer.gameObject.SetActive(true);
                Debug.Log("[PaintOnMap] NonTraversableDrawLayer activated and raycast target disabled.");
            }

            // If BaseMap texture exists, align layers; otherwise, create fallback overlays.
            if (BaseMap && BaseMap.texture)
            {
                AlignLayers();
                SetActiveOverlay(TraversableDrawLayer);
                Debug.Log("[PaintOnMap] BaseMap detected. Layers aligned and TraversableDrawLayer set as active.");

                // Enable all UI elements now that the image is loaded
                EnableAllUI();

                // Initialize UI icon colors based on Base Map
                StartCoroutine(UpdateUIIconColors());
            }
            else
            {
                Debug.LogWarning("[PaintOnMap] No BaseMap texture found at startup. Initializing empty overlays.");
                InitializeOverlay(TraversableDrawLayer, 1024, 1024);
                InitializeOverlay(NonTraversableDrawLayer, 1024, 1024);
                SetActiveOverlay(TraversableDrawLayer);
            }

            // Hook up dropdown callbacks.
            if (MapTypeDropDown)
                MapTypeDropDown.onValueChanged.AddListener(OnMapTypeDropdownChanged);
            if (DrawTypeDropDown)
                DrawTypeDropDown.onValueChanged.AddListener(OnDrawTypeDropdownChanged);

            // Hook up button callbacks once with listeners cleared
            HookUpButtonCallbacks();

            // Ensure an EventSystem exists.
            EnsureEventSystemExists();

            // Initialize UI to reflect default tool (Paintbrush)
            InitializeToolUI();

            // Set default toggles
            if (TraversableToggle)
            {
                TraversableToggle.isOn = true;
                TraversableToggle.onValueChanged.AddListener(TraversableToggleChanged);
            }
            if (NonTraversableToggle)
            {
                NonTraversableToggle.isOn = true;
                NonTraversableToggle.onValueChanged.AddListener(NonTraversableToggleChanged);
            }

            Debug.Log("[PaintOnMap] Start() => Initialization complete.");
        }

        #endregion

        #region Button Management

        /// <summary>
        /// Hooks up all button callbacks with listeners after removing existing ones.
        /// </summary>
        private void HookUpButtonCallbacks()
        {
            if (SaveAnnotationButton)
            {
                SaveAnnotationButton.onClick.RemoveAllListeners();
                SaveAnnotationButton.onClick.AddListener(SaveAnnotationMethod);
            }
            if (OpenBrowserButton)
            {
                OpenBrowserButton.onClick.RemoveAllListeners();
                OpenBrowserButton.onClick.AddListener(OpenBrowserMethod);
            }
            if (ClearButton)
            {
                ClearButton.onClick.RemoveAllListeners();
                ClearButton.onClick.AddListener(ClearAnnotationsMethod);
            }
            if (IncreaseBrushSizeButton)
            {
                IncreaseBrushSizeButton.onClick.RemoveAllListeners();
                IncreaseBrushSizeButton.onClick.AddListener(IncreaseBrushSizeMethod);
            }
            if (DecreaseBrushSizeButton)
            {
                DecreaseBrushSizeButton.onClick.RemoveAllListeners();
                DecreaseBrushSizeButton.onClick.AddListener(DecreaseBrushSizeMethod);
            }
            if (ZoomInButton)
            {
                ZoomInButton.onClick.RemoveAllListeners();
                ZoomInButton.onClick.AddListener(ZoomInMethod);
            }
            if (ZoomOutButton)
            {
                ZoomOutButton.onClick.RemoveAllListeners();
                ZoomOutButton.onClick.AddListener(ZoomOutMethod);
            }
            if (PanButton)
            {
                PanButton.onClick.RemoveAllListeners();
                PanButton.onClick.AddListener(TogglePanMode);
            }
            if (MagicWandButton)
            {
                MagicWandButton.onClick.RemoveAllListeners();
                MagicWandButton.onClick.AddListener(ToggleMagicWandOrPaintbrush);
            }
            if (UndoButton)
            {
                UndoButton.onClick.RemoveAllListeners();
                UndoButton.onClick.AddListener(UndoCurrentMethod);
            }
            if (BorderColorButton)
            {
                BorderColorButton.onClick.RemoveAllListeners();
                BorderColorButton.onClick.AddListener(ToggleEyedropper);
            }
            if (ThickenBlobButton)
            {
                ThickenBlobButton.onClick.RemoveAllListeners();
                ThickenBlobButton.onClick.AddListener(ThickenBlobMethod);
            }

            Debug.Log("[PaintOnMap] Button callbacks hooked up with listeners cleared.");
        }

        /// <summary>
        /// Disables all UI buttons, fields, and toggles except OpenBrowserButton.
        /// </summary>
        private void DisableAllUI()
        {
            // Buttons
            Button[] buttons = GetComponentsInChildren<Button>();
            foreach (Button btn in buttons)
            {
                if (btn != OpenBrowserButton)
                    btn.interactable = false;
            }

            // Dropdowns
            Dropdown[] dropdowns = GetComponentsInChildren<Dropdown>();
            foreach (Dropdown dd in dropdowns)
            {
                dd.interactable = false;
            }

            // Input Fields
            InputField[] inputFields = GetComponentsInChildren<InputField>();
            foreach (InputField input in inputFields)
            {
                input.interactable = false;
            }

            // Toggles
            Toggle[] toggles = GetComponentsInChildren<Toggle>();
            foreach (Toggle toggle in toggles)
            {
                toggle.interactable = false;
            }

            // UI Icons
            if (UIIconElements != null)
            {
                foreach (Image icon in UIIconElements)
                {
                    icon.color = Color.white; // Reset to default
                }
            }

            Debug.Log("[PaintOnMap] All UI elements except OpenBrowserButton have been disabled.");
        }

        /// <summary>
        /// Enables all UI buttons, fields, and toggles except OpenBrowserButton.
        /// </summary>
        private void EnableAllUI()
        {
            // Buttons
            Button[] buttons = GetComponentsInChildren<Button>();
            foreach (Button btn in buttons)
            {
                if (btn != OpenBrowserButton)
                    btn.interactable = true;
            }

            // Dropdowns
            Dropdown[] dropdowns = GetComponentsInChildren<Dropdown>();
            foreach (Dropdown dd in dropdowns)
            {
                dd.interactable = true;
            }

            // Input Fields
            InputField[] inputFields = GetComponentsInChildren<InputField>();
            foreach (InputField input in inputFields)
            {
                input.interactable = true;
            }

            // Toggles
            Toggle[] toggles = GetComponentsInChildren<Toggle>();
            foreach (Toggle toggle in toggles)
            {
                toggle.interactable = true;
            }

            Debug.Log("[PaintOnMap] All UI elements except OpenBrowserButton have been enabled.");
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the UI to reflect the default tool (Paintbrush).
        /// </summary>
        private void InitializeToolUI()
        {
            UpdateMagicWandButtonUI();
            UpdateEyedropperButtonUI();

            isPainting = true; // Ensure painting is enabled
            Debug.Log("[PaintOnMap] Initialized to Paintbrush Tool.");
        }

        #endregion

        #region Validation & Setup

        /// <summary>
        /// Validates that all necessary references are assigned in the Inspector.
        /// </summary>
        /// <returns>True if all references are valid; otherwise, false.</returns>
        private bool ValidateReferences()
        {
            bool valid = true;
            if (!BaseMap)
            {
                Debug.LogError("[PaintOnMap] 'BaseMap' not assigned.");
                valid = false;
            }
            if (!TraversableDrawLayer || !NonTraversableDrawLayer)
            {
                Debug.LogError("[PaintOnMap] 'TraversableDrawLayer' or 'NonTraversableDrawLayer' not assigned.");
                valid = false;
            }
            if (MagicWandButton == null)
            {
                Debug.LogError("[PaintOnMap] 'MagicWandButton' is not assigned.");
                valid = false;
            }
            if (ColorSample == null)
            {
                Debug.LogError("[PaintOnMap] 'ColorSample' Image is not assigned.");
                valid = false;
            }
            if (BorderColorButton == null)
            {
                Debug.LogError("[PaintOnMap] 'BorderColorButton' is not assigned.");
                valid = false;
            }
            if (UIIconElements == null || UIIconElements.Count == 0)
            {
                Debug.LogWarning("[PaintOnMap] 'UIIconElements' are not assigned or empty. UI icon coloring will not work.");
            }
            // Additional checks can be added here as needed
            return valid;
        }

        /// <summary>
        /// Aligns the BaseMap, PanContainer, and overlay layers to match the loaded map image dimensions.
        /// Scales them to fit within the PanContainer if necessary.
        /// </summary>
        public void AlignLayers()
        {
            if (BaseMap == null || BaseMap.texture == null)
            {
                Debug.LogError("[PaintOnMap] BaseMap or its texture is not assigned.");
                return;
            }
            Texture2D mapTex = BaseMap.texture as Texture2D;
            if (mapTex == null)
            {
                Debug.LogError("[PaintOnMap] BaseMap texture is not a Texture2D.");
                return;
            }
            int width = mapTex.width;
            int height = mapTex.height;

            // Calculate scaling factors to fit within PanContainer
            float containerWidth = PanContainer.rect.width;
            float containerHeight = PanContainer.rect.height;
            float scaleX = containerWidth / width;
            float scaleY = containerHeight / height;
            float scale = Mathf.Min(scaleX, scaleY);

            // Apply scaling
            BaseMap.rectTransform.sizeDelta = new Vector2(width * scale, height * scale);
            TraversableDrawLayer.rectTransform.sizeDelta = new Vector2(width * scale, height * scale);
            NonTraversableDrawLayer.rectTransform.sizeDelta = new Vector2(width * scale, height * scale);
            PanContainer.localScale = Vector3.one * scale;

            // Initialize overlay textures with scaled dimensions
            InitializeOverlay(TraversableDrawLayer, width, height);
            InitializeOverlay(NonTraversableDrawLayer, width, height);

            Debug.Log($"[PaintOnMap] Layers aligned and scaled by factor {scale}.");
        }

        /// <summary>
        /// Initializes a blank texture for the given overlay.
        /// </summary>
        /// <param name="overlay">The RawImage overlay to initialize.</param>
        /// <param name="width">Width of the texture.</param>
        /// <param name="height">Height of the texture.</param>
        private void InitializeOverlay(RawImage overlay, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            ClearTexture(tex);
            tex.filterMode = FilterMode.Point; // Prevents blurring
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            overlay.texture = tex;
            Debug.Log($"[PaintOnMap] Initialized overlay '{overlay.name}' to {width}x{height}");
        }

        #endregion

        #region SetActiveOverlay

        /// <summary>
        /// Sets the active overlay (and caches its texture) for drawing.
        /// </summary>
        /// <param name="overlay">The RawImage overlay to set as active.</param>
        private void SetActiveOverlay(RawImage overlay)
        {
            activeOverlay = overlay;
            activeTexture = overlay.texture as Texture2D;
            if (activeTexture == null)
            {
                Debug.LogError("[PaintOnMap] Active texture is null! Ensure overlay textures are initialized.");
            }
            else
            {
                Debug.Log($"[PaintOnMap] Active overlay set to '{overlay.name}'.");
            }
        }

        #endregion

        #region Dropdown Logic

        /// <summary>
        /// Handles changes to the Map Type dropdown.
        /// </summary>
        /// <param name="index">Selected index.</param>
        public void OnMapTypeDropdownChanged(int index)
        {
            if (!MapTypeDropDown) return;
            if (index < 0 || index >= MapTypeDropDown.options.Count) return;
            currentMapType = MapTypeDropDown.options[index].text;
            Debug.Log($"[PaintOnMap] OnMapTypeDropdownChanged => {currentMapType}");
        }

        /// <summary>
        /// Handles changes to the Draw Type dropdown.
        /// </summary>
        /// <param name="index">Selected index.</param>
        public void OnDrawTypeDropdownChanged(int index)
        {
            if (!DrawTypeDropDown) return;
            if (index < 0 || index >= DrawTypeDropDown.options.Count) return;
            currentDrawType = DrawTypeDropDown.options[index].text;
            Debug.Log($"[PaintOnMap] OnDrawTypeDropdownChanged => {currentDrawType}");

            string lower = currentDrawType.ToLower();
            if (lower.Contains("traversable path"))
            {
                paintColor = Color.green;
                SetActiveOverlay(TraversableDrawLayer);
                Debug.Log("[PaintOnMap] DrawType => Traversable Path (Green)");
            }
            else if (lower.Contains("non-traversable terrain"))
            {
                paintColor = Color.red;
                SetActiveOverlay(NonTraversableDrawLayer);
                Debug.Log("[PaintOnMap] DrawType => Non-Traversable Terrain (Red)");
            }
            else if (lower.Contains("non-traversable constructed"))
            {
                paintColor = Color.blue;
                SetActiveOverlay(NonTraversableDrawLayer);
                Debug.Log("[PaintOnMap] DrawType => Non-Traversable Constructed (Blue)");
            }
            else if (lower.Contains("traversable object"))
            {
                paintColor = Color.yellow;
                SetActiveOverlay(TraversableDrawLayer);
                Debug.Log("[PaintOnMap] DrawType => Traversable Object (Yellow)");
            }
            else
            {
                paintColor = Color.white;
                SetActiveOverlay(TraversableDrawLayer);
                Debug.Log("[PaintOnMap] DrawType => Default (White)");
            }
        }

        #endregion

        #region Pointer / Painting

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log("[PaintOnMap] OnPointerDown called.");

            if (isMagicWandActive)
            {
                // Magic Wand operation
                Vector2 texCoord = GetTextureCoord(eventData);
                Debug.Log($"[PaintOnMap] Magic Wand activated at texture coordinates: {texCoord}");

                // Save state before filling
                SaveUndoState();

                // Perform flood fill
                StartCoroutine(FloodFill(activeTexture, Vector2Int.FloorToInt(texCoord), paintColor));

                // Magic Wand remains active until toggled off
            }
            else if (isEyedropperActive)
            {
                // Eyedropper operation
                Vector2 screenPos = eventData.position;
                Color pickedColor = PickColorFromBaseMap(screenPos);
                if (pickedColor != Color.clear)
                {
                    borderColor = pickedColor;
                    if (ColorSample != null)
                    {
                        ColorSample.color = borderColor;
                        Debug.Log("[PaintOnMap] ColorSample image updated.");
                    }
                    Debug.Log($"[PaintOnMap] Border Color set to: #{ColorUtility.ToHtmlStringRGBA(borderColor)}");
                }

                // Deactivate Eyedropper after use
                isEyedropperActive = false;
                UpdateEyedropperButtonUI();
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isPainting = true;
                Debug.Log("[PaintOnMap] Eyedropper Tool => OFF after picking color.");
            }
            else
            {
                // Regular painting operation
                if (!activeTexture)
                {
                    Debug.LogWarning("[PaintOnMap] OnPointerDown => activeTexture is null. Cannot paint.");
                    return;
                }

                isPainting = true;
                Vector2 texCoord = GetTextureCoord(eventData);
                lastTexCoord = texCoord;
                Debug.Log($"[PaintOnMap] Painting started at texture coordinates: {texCoord}");

                // Save state before painting
                SaveUndoState();

                // Check for overlapping before painting
                if (CanPaintPixel((int)texCoord.x, (int)texCoord.y))
                {
                    DrawCircle(activeTexture, (int)texCoord.x, (int)texCoord.y, brushSize, paintColor);
                    activeTexture.Apply();
                    Debug.Log("[PaintOnMap] Drew a brush stroke, refreshed UI.");
                }
                else
                {
                    Debug.LogWarning("[PaintOnMap] Cannot paint here as the pixel is already annotated in another layer.");
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isPanning && PanContainer)
            {
                Vector2 delta = eventData.position - panStartPointerPosition;
                PanContainer.anchoredPosition = panStartContainerPosition + delta;
                Debug.Log($"[PaintOnMap] Panning... New position: {PanContainer.anchoredPosition}");
                return;
            }

            if (isPainting && activeTexture)
            {
                Vector2 texCoord = GetTextureCoord(eventData);
                if (texCoord.x >= 0 && texCoord.x < activeTexture.width &&
                    texCoord.y >= 0 && texCoord.y < activeTexture.height)
                {
                    // Check for overlapping before painting
                    if (CanPaintPixel((int)texCoord.x, (int)texCoord.y))
                    {
                        DrawLine(activeTexture, lastTexCoord, texCoord, brushSize, paintColor);
                        lastTexCoord = texCoord;
                        activeTexture.Apply();
                        Debug.Log($"[PaintOnMap] Drawing line to texture coordinates: {texCoord}");
                    }
                    else
                    {
                        Debug.LogWarning("[PaintOnMap] Cannot paint here as the pixel is already annotated in another layer.");
                    }
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isPainting)
            {
                isPainting = false;
                strokeStateSaved = false;
                Debug.Log("[PaintOnMap] Painting stopped.");
            }
        }

        /// <summary>
        /// Converts the pointer event position to texture coordinates using the BaseMap's RectTransform.
        /// </summary>
        /// <param name="eventData">Pointer event data.</param>
        /// <returns>Texture coordinates as Vector2.</returns>
        private Vector2 GetTextureCoord(PointerEventData eventData)
        {
            if (!BaseMap || !activeTexture) return Vector2.zero;
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                BaseMap.rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                Rect r = BaseMap.rectTransform.rect;
                // Normalize localPoint based on mapRect size
                float normX = (localPoint.x - r.xMin) / r.width;
                float normY = (localPoint.y - r.yMin) / r.height;
                float tx = Mathf.Clamp(normX * activeTexture.width, 0, activeTexture.width - 1);
                float ty = Mathf.Clamp(normY * activeTexture.height, 0, activeTexture.height - 1);
                return new Vector2(tx, ty);
            }
            return Vector2.zero;
        }

        #endregion

        #region Drawing Functionalities

        /// <summary>
        /// Draws a perfect circle on the texture at the specified coordinates.
        /// </summary>
        /// <param name="tex">The texture to draw on.</param>
        /// <param name="cx">Center x-coordinate.</param>
        /// <param name="cy">Center y-coordinate.</param>
        /// <param name="radius">Radius of the circle.</param>
        /// <param name="color">Color to paint with.</param>
        private void DrawCircle(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            if (!tex) return;
            int rsq = radius * radius;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= rsq)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                        {
                            tex.SetPixel(px, py, color);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws a line between two points using the specified brush size and color.
        /// </summary>
        /// <param name="tex">The texture to draw on.</param>
        /// <param name="start">Starting texture coordinates.</param>
        /// <param name="end">Ending texture coordinates.</param>
        /// <param name="radius">Brush size.</param>
        /// <param name="color">Color to paint with.</param>
        private void DrawLine(Texture2D tex, Vector2 start, Vector2 end, int radius, Color color)
        {
            if (!tex) return;
            float distance = Vector2.Distance(start, end);
            int steps = Mathf.CeilToInt(distance);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 pos = Vector2.Lerp(start, end, i / (float)steps);
                DrawCircle(tex, (int)pos.x, (int)pos.y, radius, color);
            }
        }

        #endregion

        #region Magic Wand Functionalities

        /// <summary>
        /// Toggles between Magic Wand and Paintbrush tools.
        /// </summary>
        public void ToggleMagicWandOrPaintbrush()
        {
            Debug.Log("[PaintOnMap] ToggleMagicWandOrPaintbrush() called.");

            if (isEyedropperActive)
            {
                // Disable Eyedropper if active
                isEyedropperActive = false;
                UpdateEyedropperButtonUI();
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isPainting = true;
                Debug.Log("[PaintOnMap] Eyedropper Tool => OFF");
            }

            isMagicWandActive = !isMagicWandActive;
            Debug.Log($"[PaintOnMap] isMagicWandActive set to: {isMagicWandActive}");

            UpdateMagicWandButtonUI();

            if (isMagicWandActive)
            {
                // Disable regular painting
                isPainting = false;
                Debug.Log("[PaintOnMap] Magic Wand Tool => ON");
            }
            else
            {
                // Revert cursor to default
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Debug.Log("[PaintOnMap] Cursor reverted to default.");

                // Enable regular painting
                isPainting = true;
                Debug.Log("[PaintOnMap] Magic Wand Tool => OFF");
            }
        }

        /// <summary>
        /// Updates the Magic Wand Button's UI based on the current state.
        /// </summary>
        private void UpdateMagicWandButtonUI()
        {
            // Change button appearance based on tool state
            if (MagicWandButton)
            {
                ColorBlock cb = MagicWandButton.colors;
                if (isMagicWandActive)
                {
                    cb.normalColor = Color.yellow;
                }
                else
                {
                    cb.normalColor = Color.white;
                }
                MagicWandButton.colors = cb;
            }
        }

        /// <summary>
        /// Performs a flood-fill operation on the active texture starting from the specified position.
        /// The fill respects the Base Map's boundaries and prevents overlapping with other annotation layers.
        /// </summary>
        /// <param name="targetTex">The texture to fill.</param>
        /// <param name="startPos">Starting pixel position.</param>
        /// <param name="fillColor">Color to fill with.</param>
        private IEnumerator FloodFill(Texture2D targetTex, Vector2Int startPos, Color fillColor)
        {
            if (targetTex == null || BaseMap == null || BaseMap.texture == null)
            {
                Debug.LogError("[PaintOnMap] FloodFill => Invalid textures.");
                yield break;
            }

            Texture2D baseTex = BaseMap.texture as Texture2D;
            if (baseTex == null)
            {
                Debug.LogError("[PaintOnMap] FloodFill => BaseMap texture is not a Texture2D.");
                yield break;
            }

            // Get the base color at the starting position
            Color targetBaseColor = baseTex.GetPixel(startPos.x, startPos.y);

            // Get the current color on the active layer at the starting position
            Color targetLayerColor = targetTex.GetPixel(startPos.x, startPos.y);

            // If the target color is already the fill color, do nothing
            if (targetLayerColor == fillColor)
            {
                Debug.Log("[PaintOnMap] FloodFill => Target pixel is already the fill color.");
                yield break;
            }

            // Define a tolerance for color matching
            float tolerance = colorThreshold; // Adjustable via inspector

            // Queue for BFS
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(startPos);

            // Keep track of visited pixels
            bool[,] visited = new bool[targetTex.width, targetTex.height];
            visited[startPos.x, startPos.y] = true;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                // Get Base Map color at current position
                Color currentBaseColor = baseTex.GetPixel(current.x, current.y);

                // Compare with target base color
                if (!IsColorSimilar(currentBaseColor, targetBaseColor, tolerance))
                {
                    continue; // Boundary reached
                }

                // Check if the pixel is already annotated in another layer
                if (!CanPaintPixel(current.x, current.y))
                {
                    continue; // Prevent overlapping
                }

                // Check border thresholds
                bool isBorder = IsBorderPixel(current.x, current.y);
                if (isBorder)
                {
                    // Check border color and thickness
                    if (IsBorderMatching(current.x, current.y))
                    {
                        continue; // Do not paint beyond matching border
                    }
                }

                // Set the fill color on the active layer
                targetTex.SetPixel(current.x, current.y, fillColor);

                // Enqueue neighboring pixels (4-directional)
                foreach (Vector2Int neighbor in GetNeighbors(current, targetTex.width, targetTex.height))
                {
                    if (!visited[neighbor.x, neighbor.y])
                    {
                        queue.Enqueue(neighbor);
                        visited[neighbor.x, neighbor.y] = true;
                    }
                }

                // Yield periodically to avoid freezing the editor
                if (queue.Count % 100 == 0)
                {
                    yield return null;
                }
            }

            targetTex.Apply();
            Debug.Log("[PaintOnMap] FloodFill => Completed.");
        }

        /// <summary>
        /// Determines whether two colors are similar within a specified tolerance.
        /// </summary>
        /// <param name="a">First color.</param>
        /// <param name="b">Second color.</param>
        /// <param name="tolerance">Tolerance value between 0 and 1.</param>
        /// <returns>True if colors are similar; otherwise, false.</returns>
        private bool IsColorSimilar(Color a, Color b, float tolerance)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance &&
                   Mathf.Abs(a.a - b.a) <= tolerance;
        }

        /// <summary>
        /// Retrieves the 4-directional neighbors of a given pixel.
        /// </summary>
        /// <param name="pos">Current pixel position.</param>
        /// <param name="width">Width of the texture.</param>
        /// <param name="height">Height of the texture.</param>
        /// <returns>List of neighboring pixel positions.</returns>
        private List<Vector2Int> GetNeighbors(Vector2Int pos, int width, int height)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            // Up
            if (pos.y + 1 < height)
                neighbors.Add(new Vector2Int(pos.x, pos.y + 1));
            // Down
            if (pos.y - 1 >= 0)
                neighbors.Add(new Vector2Int(pos.x, pos.y - 1));
            // Left
            if (pos.x - 1 >= 0)
                neighbors.Add(new Vector2Int(pos.x - 1, pos.y));
            // Right
            if (pos.x + 1 < width)
                neighbors.Add(new Vector2Int(pos.x + 1, pos.y));

            return neighbors;
        }

        /// <summary>
        /// Determines if the current pixel is a border pixel based on border thickness.
        /// </summary>
        /// <param name="x">X-coordinate of the pixel.</param>
        /// <param name="y">Y-coordinate of the pixel.</param>
        /// <returns>True if it's a border pixel; otherwise, false.</returns>
        private bool IsBorderPixel(int x, int y)
        {
            // Simple border detection based on alpha value
            // Assuming borders are painted with higher alpha
            Texture2D baseTex = BaseMap.texture as Texture2D;
            if (baseTex == null) return false;

            Color baseColor = baseTex.GetPixel(x, y);
            return baseColor.a >= borderColorThreshold;
        }

        /// <summary>
        /// Checks if the border pixel matches the set border color and thickness.
        /// </summary>
        /// <param name="x">X-coordinate of the pixel.</param>
        /// <param name="y">Y-coordinate of the pixel.</param>
        /// <returns>True if the border matches; otherwise, false.</returns>
        private bool IsBorderMatching(int x, int y)
        {
            Texture2D baseTex = BaseMap.texture as Texture2D;
            if (baseTex == null) return false;

            Color baseColor = baseTex.GetPixel(x, y);
            return IsColorSimilar(baseColor, borderColor, borderColorThreshold) &&
                   baseColor.a >= borderThickness;
        }

        #endregion

        #region Eyedropper Functionalities

        /// <summary>
        /// Toggles the Eyedropper tool On and Off.
        /// </summary>
        public void ToggleEyedropper()
        {
            Debug.Log("[PaintOnMap] ToggleEyedropper() called.");

            if (isMagicWandActive)
            {
                // Disable Magic Wand if active
                isMagicWandActive = false;
                UpdateMagicWandButtonUI();
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isPainting = true;
                Debug.Log("[PaintOnMap] Magic Wand Tool => OFF");
            }

            isEyedropperActive = !isEyedropperActive;
            Debug.Log($"[PaintOnMap] isEyedropperActive set to: {isEyedropperActive}");

            UpdateEyedropperButtonUI();

            if (isEyedropperActive)
            {
                // Disable regular painting
                isPainting = false;
                Debug.Log("[PaintOnMap] Eyedropper Tool => ON");
            }
            else
            {
                // Revert cursor to default
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Debug.Log("[PaintOnMap] Cursor reverted to default.");

                // Enable regular painting
                isPainting = true;
                Debug.Log("[PaintOnMap] Eyedropper Tool => OFF");
            }
        }

        /// <summary>
        /// Updates the Eyedropper Button's UI based on the current state.
        /// </summary>
        private void UpdateEyedropperButtonUI()
        {
            // Change button appearance based on tool state
            if (BorderColorButton)
            {
                ColorBlock cb = BorderColorButton.colors;
                if (isEyedropperActive)
                {
                    cb.normalColor = Color.cyan;
                }
                else
                {
                    cb.normalColor = Color.white;
                }
                BorderColorButton.colors = cb;
            }
        }

        /// <summary>
        /// Picks a color from the Base Map based on screen position.
        /// </summary>
        /// <param name="screenPos">Screen position to pick color from.</param>
        /// <returns>Picked color.</returns>
        private Color PickColorFromBaseMap(Vector2 screenPos)
        {
            if (!BaseMap || !BaseMap.texture)
            {
                Debug.LogWarning("[PaintOnMap] BaseMap or texture null for Eyedropper.");
                return Color.clear;
            }
            Texture2D baseTex = BaseMap.texture as Texture2D;
            if (!baseTex) return Color.clear;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(BaseMap.rectTransform, screenPos, null, out Vector2 localPoint))
            {
                Rect rect = BaseMap.rectTransform.rect;
                float normX = (localPoint.x - rect.xMin) / rect.width;
                float normY = (localPoint.y - rect.yMin) / rect.height;
                int px = Mathf.Clamp(Mathf.FloorToInt(normX * baseTex.width), 0, baseTex.width - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt(normY * baseTex.height), 0, baseTex.height - 1);
                Color pick = baseTex.GetPixel(px, py);
                Debug.Log($"[PaintOnMap] PickColorFromBaseMap => color at ({px},{py}) is #{ColorUtility.ToHtmlStringRGBA(pick)}");
                return pick;
            }
            Debug.LogWarning("[PaintOnMap] Eyedropper => could not convert screen pos to local point.");
            return Color.clear;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Clears the entire texture by setting all pixels to transparent.
        /// </summary>
        /// <param name="tex">The texture to clear.</param>
        public void ClearTexture(Texture2D tex)
        {
            if (!tex) return;
            Color[] c = new Color[tex.width * tex.height];
            for (int i = 0; i < c.Length; i++)
                c[i] = Color.clear;
            tex.SetPixels(c);
            tex.Apply();
            Debug.Log("[PaintOnMap] Texture cleared.");
        }

        /// <summary>
        /// Retrieves the color of a specific pixel from the Base Map.
        /// </summary>
        /// <param name="x">X-coordinate of the pixel.</param>
        /// <param name="y">Y-coordinate of the pixel.</param>
        /// <returns>Color of the pixel.</returns>
        public Color GetBaseMapPixel(int x, int y)
        {
            if (!BaseMap || !BaseMap.texture) return Color.clear;
            Texture2D baseTex = BaseMap.texture as Texture2D;
            if (!baseTex) return Color.clear;
            x = Mathf.Clamp(x, 0, baseTex.width - 1);
            y = Mathf.Clamp(y, 0, baseTex.height - 1);
            return baseTex.GetPixel(x, y);
        }

        /// <summary>
        /// Saves the current state of the active texture for undo functionality.
        /// </summary>
        public void SaveUndoState()
        {
            if (!enableUndo) return;
            if (!activeTexture) return;
            undoManager.SaveState(activeTexture);
            Debug.Log("[PaintOnMap] UndoManager => State saved.");
        }

        /// <summary>
        /// Resizes a texture to new dimensions.
        /// </summary>
        /// <param name="source">Source texture.</param>
        /// <param name="newWidth">New width.</param>
        /// <param name="newHeight">New height.</param>
        /// <returns>Resized texture.</returns>
        public Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            if (!source)
            {
                Debug.LogError("[PaintOnMap] ResizeTexture => Source texture is null.");
                return null;
            }
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            rt.filterMode = FilterMode.Bilinear;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Debug.Log("[PaintOnMap] ResizeTexture => Successfully resized texture.");
            return result;
        }

        /// <summary>
        /// Ensures an EventSystem exists in the scene.
        /// </summary>
        private void EnsureEventSystemExists()
        {
            if (!FindObjectOfType<EventSystem>())
            {
                Debug.LogWarning("[PaintOnMap] No EventSystem found! Creating one.");
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                Debug.Log("[PaintOnMap] EventSystem created.");
            }

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas && !rootCanvas.GetComponent<GraphicRaycaster>())
            {
                Debug.LogWarning("[PaintOnMap] No GraphicRaycaster on root Canvas! Adding one.");
                rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log("[PaintOnMap] GraphicRaycaster added to root Canvas.");
            }
        }

        /// <summary>
        /// Checks if the pixel at (x, y) can be painted based on other annotation layers.
        /// Prevents overlapping by ensuring that the pixel is not already annotated in another layer.
        /// </summary>
        /// <param name="x">X-coordinate of the pixel.</param>
        /// <param name="y">Y-coordinate of the pixel.</param>
        /// <returns>True if painting is allowed; otherwise, false.</returns>
        private bool CanPaintPixel(int x, int y)
        {
            // Check Traversable Layer
            if (activeOverlay != TraversableDrawLayer)
            {
                Texture2D travTex = TraversableDrawLayer.texture as Texture2D;
                if (travTex != null && travTex.GetPixel(x, y).a > 0f)
                {
                    return false; // Already annotated in Traversable Layer
                }
            }

            // Check Non-Traversable Layer
            if (activeOverlay != NonTraversableDrawLayer)
            {
                Texture2D nonTravTex = NonTraversableDrawLayer.texture as Texture2D;
                if (nonTravTex != null && nonTravTex.GetPixel(x, y).a > 0f)
                {
                    return false; // Already annotated in Non-Traversable Layer
                }
            }

            return true; // Safe to paint
        }

        #endregion


        #region Save, Browser, Zoom, Pan

        /// <summary>
        /// Saves the current annotations by combining both layers into a single PNG file.
        /// </summary>
        public void SaveAnnotationMethod()
        {
            if (!Directory.Exists(SaveFolder))
            {
                Directory.CreateDirectory(SaveFolder);
                Debug.Log($"[PaintOnMap] SaveFolder '{SaveFolder}' created.");
            }
            if (!TraversableDrawLayer || !TraversableDrawLayer.texture ||
                !NonTraversableDrawLayer || !NonTraversableDrawLayer.texture)
            {
                Debug.LogWarning("[PaintOnMap] SaveAnnotation => Overlays not ready.");
                return;
            }
            var travTex = TraversableDrawLayer.texture as Texture2D;
            var nonTravTex = NonTraversableDrawLayer.texture as Texture2D;
            if (!travTex || !nonTravTex)
            {
                Debug.LogWarning("[PaintOnMap] SaveAnnotation => Textures are null.");
                return;
            }
            int width = travTex.width;
            int height = travTex.height;
            Color[] travPixels = travTex.GetPixels();
            Color[] nonTravPixels = nonTravTex.GetPixels();
            Color[] final = new Color[travPixels.Length];
            for (int i = 0; i < final.Length; i++)
            {
                if (nonTravPixels[i].a > 0f)
                {
                    final[i] = nonTravPixels[i];
                }
                else if (travPixels[i].a > 0f)
                {
                    final[i] = travPixels[i];
                }
                else
                {
                    final[i] = Color.clear;
                }
            }
            Texture2D combined = new Texture2D(width, height, TextureFormat.RGBA32, false);
            combined.SetPixels(final);
            combined.Apply();
            string fileName = $"Annotation_{currentMapType}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(SaveFolder, fileName);
            File.WriteAllBytes(fullPath, combined.EncodeToPNG());
            Debug.Log($"[PaintOnMap] SaveAnnotation => Saved: {fullPath}");
        }

        /// <summary>
        /// Opens the file browser to select a new Base Map image.
        /// </summary>
        public void OpenBrowserMethod()
        {
            FileBrowser.SetFilters(false, new FileBrowser.Filter("Images", ".png", ".jpg", ".jpeg"));
            FileBrowser.SetDefaultFilter(".png");
            FileBrowser.ShowLoadDialog(
                (paths) => { StartCoroutine(LoadMapTexture(paths[0])); },
                () => { Debug.Log("[PaintOnMap] File selection canceled."); },
                FileBrowser.PickMode.Files,
                false,
                null,
                "Select a Map Image",
                "Select"
            );
            Debug.Log("[PaintOnMap] File browser opened.");
        }

        /// <summary>
        /// Coroutine to load the selected map texture.
        /// Scales it to fit PanContainer if larger.
        /// </summary>
        /// <param name="path">Path of the selected image.</param>
        private IEnumerator LoadMapTexture(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[PaintOnMap] LoadMapTexture => File not found: " + path);
                yield break;
            }
            byte[] data = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (!tex.LoadImage(data))
            {
                Debug.LogWarning("[PaintOnMap] LoadMapTexture => Failed to load image data.");
                yield break;
            }
            BaseMap.texture = tex;
            BaseMap.SetNativeSize();
            AlignLayers();
            SetActiveOverlay(TraversableDrawLayer);
            Debug.Log($"[PaintOnMap] LoadMapTexture => {path} ({tex.width}x{tex.height})");

            // Enable all UI elements now that the image is loaded
            EnableAllUI();

            // Initialize UI icon colors based on Base Map
            StartCoroutine(UpdateUIIconColors());

            yield return null;
        }

        /// <summary>
        /// Zooms in the PanContainer.
        /// </summary>
        public void ZoomInMethod()
        {
            if (!PanContainer) return;
            float currentScale = PanContainer.localScale.x;
            float newScale = Mathf.Clamp(currentScale + zoomSpeed, minZoom, maxZoom);
            PanContainer.localScale = Vector3.one * newScale;
            Debug.Log("[PaintOnMap] ZoomIn => Scale= " + newScale);
        }

        /// <summary>
        /// Zooms out the PanContainer.
        /// </summary>
        public void ZoomOutMethod()
        {
            if (!PanContainer) return;
            float currentScale = PanContainer.localScale.x;
            float newScale = Mathf.Clamp(currentScale - zoomSpeed, minZoom, maxZoom);
            PanContainer.localScale = Vector3.one * newScale;
            Debug.Log("[PaintOnMap] ZoomOut => Scale= " + newScale);
        }

        /// <summary>
        /// Toggles the panning mode.
        /// </summary>
        public void TogglePanMode()
        {
            isPanning = !isPanning;
            Debug.Log("[PaintOnMap] Pan Mode => " + (isPanning ? "ON" : "OFF"));

            // Store the starting positions for smooth panning
            if (isPanning)
            {
                panStartPointerPosition = Input.mousePosition;
                panStartContainerPosition = PanContainer.anchoredPosition;
            }

            // Optional: Change button appearance based on panning state
            if (PanButton)
            {
                ColorBlock cb = PanButton.colors;
                if (isPanning)
                {
                    cb.normalColor = Color.yellow;
                }
                else
                {
                    cb.normalColor = Color.white;
                }
                PanButton.colors = cb;
            }
        }

        /// <summary>
        /// Sets the panning state.
        /// </summary>
        /// <param name="enable">Enable or disable panning.</param>
        public void SetPan(bool enable)
        {
            isPanning = enable;
            Debug.Log("[PaintOnMap] SetPan => " + (isPanning ? "ON" : "OFF"));
        }

        #endregion

        #region Update Base Map Manually

        /// <summary>
        /// Updates the Base Map texture and aligns all overlays accordingly.
        /// </summary>
        /// <param name="newTexture">The new Base Map texture.</param>
        public void UpdateMapTexture(Texture2D newTexture)
        {
            if (BaseMap && newTexture)
            {
                BaseMap.texture = newTexture;
                BaseMap.SetNativeSize();
                Debug.Log("[PaintOnMap] UpdateMapTexture => BaseMap updated.");
                AlignLayers();
                SetActiveOverlay(TraversableDrawLayer);

                // Enable all UI elements now that the image is loaded
                EnableAllUI();

                // Initialize UI icon colors based on Base Map
                StartCoroutine(UpdateUIIconColors());
            }
            else
            {
                Debug.LogWarning("[PaintOnMap] UpdateMapTexture => BaseMap or newTexture null.");
            }
        }

        #endregion

        #region Traversable & Non-Traversable Toggle Methods

        /// <summary>
        /// Handles changes to the Traversable toggle.
        /// </summary>
        /// <param name="isOn">State of the toggle.</param>
        public void TraversableToggleChanged(bool isOn)
        {
            if (TraversableDrawLayer)
            {
                TraversableDrawLayer.gameObject.SetActive(isOn);
                Debug.Log($"[PaintOnMap] Traversable Layer => {(isOn ? "VISIBLE" : "HIDDEN")}");
            }
        }

        /// <summary>
        /// Handles changes to the Non-Traversable toggle.
        /// </summary>
        /// <param name="isOn">State of the toggle.</param>
        public void NonTraversableToggleChanged(bool isOn)
        {
            if (NonTraversableDrawLayer)
            {
                NonTraversableDrawLayer.gameObject.SetActive(isOn);
                Debug.Log($"[PaintOnMap] Non-Traversable Layer => {(isOn ? "VISIBLE" : "HIDDEN")}");
            }
        }

        #endregion

        #region Input Field Methods

        /// <summary>
        /// Sets the color threshold based on user input.
        /// </summary>
        /// <param name="value">Input string representing the threshold.</param>
        public void SetThresholdFieldValue(string value)
        {
            if (float.TryParse(value, out float val))
            {
                colorThreshold = Mathf.Clamp01(val);
                Debug.Log("[PaintOnMap] SetThresholdFieldValue => " + colorThreshold);
            }
            else
            {
                Debug.LogWarning("[PaintOnMap] SetThresholdFieldValue => Invalid float.");
            }
        }

        /// <summary>
        /// Sets the border thickness based on user input.
        /// </summary>
        /// <param name="value">Input string representing the thickness.</param>
        public void SetBorderThicknessFieldValue(string value)
        {
            if (float.TryParse(value, out float val))
            {
                borderThickness = Mathf.Max(val, 0f);
                Debug.Log("[PaintOnMap] SetBorderThicknessFieldValue => " + borderThickness);
            }
            else
            {
                Debug.LogWarning("[PaintOnMap] SetBorderThicknessFieldValue => Invalid float.");
            }
        }

        /// <summary>
        /// Sets the border color threshold based on user input.
        /// </summary>
        /// <param name="value">Input string representing the threshold.</param>
        public void SetBorderColorThresholdInputValue(string value)
        {
            if (float.TryParse(value, out float val))
            {
                borderColorThreshold = Mathf.Clamp01(val);
                Debug.Log("[PaintOnMap] SetBorderColorThresholdInputValue => " + borderColorThreshold);
            }
            else
            {
                Debug.LogWarning("[PaintOnMap] SetBorderColorThresholdInputValue => Invalid float.");
            }
        }

        #endregion

        #region Missing Methods

        /// <summary>
        /// Clears all annotations on both Traversable and Non-Traversable overlays.
        /// </summary>
        public void ClearAnnotationsMethod()
        {
            if (TraversableDrawLayer && TraversableDrawLayer.texture)
            {
                Texture2D travTex = TraversableDrawLayer.texture as Texture2D;
                ClearTexture(travTex);
                travTex.Apply();
                Debug.Log("[PaintOnMap] ClearAnnotationsMethod => TraversableDrawLayer cleared.");
            }
            if (NonTraversableDrawLayer && NonTraversableDrawLayer.texture)
            {
                Texture2D nonTravTex = NonTraversableDrawLayer.texture as Texture2D;
                ClearTexture(nonTravTex);
                nonTravTex.Apply();
                Debug.Log("[PaintOnMap] ClearAnnotationsMethod => NonTraversableDrawLayer cleared.");
            }
        }

        /// <summary>
        /// Increases the brush size.
        /// </summary>
        public void IncreaseBrushSizeMethod()
        {
            brushSize += 1;
            Debug.Log($"[PaintOnMap] IncreaseBrushSizeMethod => brushSize={brushSize}");
        }

        /// <summary>
        /// Decreases the brush size.
        /// </summary>
        public void DecreaseBrushSizeMethod()
        {
            brushSize = Mathf.Max(brushSize - 1, 1);
            Debug.Log($"[PaintOnMap] DecreaseBrushSizeMethod => brushSize={brushSize}");
        }

        /// <summary>
        /// Performs an undo operation on the active texture.
        /// </summary>
        public void UndoCurrentMethod()
        {
            if (enableUndo && activeTexture != null)
            {
                undoManager.Undo(activeTexture);
                Debug.Log("[PaintOnMap] UndoCurrentMethod => Undo performed.");
            }
            else
            {
                Debug.LogWarning("[PaintOnMap] UndoCurrentMethod => Undo not enabled or activeTexture is null.");
            }
        }

        /// <summary>
        /// Thickens the blob in the active overlay without overlapping other annotation layers.
        /// </summary>
        public void ThickenBlobMethod()
        {
            if (activeTexture == null)
            {
                Debug.LogWarning("[PaintOnMap] ThickenBlobMethod => activeTexture is null.");
                return;
            }

            // Implementing a simple dilation algorithm
            SaveUndoState();

            Texture2D texCopy = Instantiate(activeTexture);
            texCopy.Apply();

            for (int x = 1; x < texCopy.width - 1; x++)
            {
                for (int y = 1; y < texCopy.height - 1; y++)
                {
                    if (activeTexture.GetPixel(x, y).a > 0f)
                    {
                        // Check neighbors
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;

                                if (nx >= 0 && nx < texCopy.width && ny >= 0 && ny < texCopy.height)
                                {
                                    if (activeTexture.GetPixel(nx, ny).a == 0f)
                                    {
                                        // Check if painting here would overlap another layer
                                        if (CanPaintPixel(nx, ny))
                                        {
                                            texCopy.SetPixel(nx, ny, paintColor);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            texCopy.Apply();
            activeOverlay.texture = texCopy;
            activeTexture = texCopy;

            Debug.Log("[PaintOnMap] ThickenBlobMethod => Blob thickened without overlapping.");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Converts a Sprite to a Texture2D.
        /// Note: This method is retained for potential future use but replaced with Texture2D handling.
        /// </summary>
        /// <param name="sprite">Sprite to convert.</param>
        /// <returns>Converted Texture2D.</returns>
        public Texture2D SpriteToTexture(Sprite sprite)
        {
            if (sprite == null)
            {
                Debug.LogError("[PaintOnMap] SpriteToTexture => Provided sprite is null.");
                return null;
            }

            Texture2D originalTexture = sprite.texture;

            if (originalTexture == null)
            {
                Debug.LogError("[PaintOnMap] SpriteToTexture => Sprite's texture is null.");
                return null;
            }

            if (!originalTexture.isReadable)
            {
                Debug.LogError("[PaintOnMap] SpriteToTexture => Texture is not readable. Ensure 'Read/Write Enabled' is checked in the Texture Import Settings.");
                return null;
            }

            Rect rect = sprite.rect;
            Texture2D tex = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false);
            try
            {
                Color[] data = originalTexture.GetPixels((int)rect.x, (int)rect.y, Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
                tex.SetPixels(data);
                tex.Apply();
                Debug.Log("[PaintOnMap] SpriteToTexture => Successfully converted sprite to texture.");
                return tex;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PaintOnMap] SpriteToTexture => Exception occurred: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helper UI Methods

        /// <summary>
        /// Updates the color of UI icon elements to contrast with the Base Map.
        /// </summary>
        /// <returns></returns>
        private IEnumerator UpdateUIIconColors()
        {
            if (UIIconElements == null || UIIconElements.Count == 0)
            {
                yield break;
            }

            // Wait a frame to ensure textures are loaded
            yield return null;

            foreach (Image icon in UIIconElements)
            {
                Vector2Int texCoord = GetIconBaseMapPixel(icon);
                if (texCoord.x >= 0 && texCoord.x < BaseMap.texture.width &&
                    texCoord.y >= 0 && texCoord.y < BaseMap.texture.height)
                {
                    Texture2D baseTex = BaseMap.texture as Texture2D;
                    Color baseColor = baseTex.GetPixel(texCoord.x, texCoord.y);
                    Color contrastingColor = GetContrastingColor(baseColor);
                    icon.color = contrastingColor;
                }
                else
                {
                    icon.color = Color.white; // Default color if out of bounds
                }
            }

            Debug.Log("[PaintOnMap] UI Icon colors updated based on Base Map.");
        }

        /// <summary>
        /// Determines a contrasting color (black or white) based on the brightness of the input color.
        /// </summary>
        /// <param name="color">Input color.</param>
        /// <returns>Contrasting color.</returns>
        private Color GetContrastingColor(Color color)
        {
            // Calculate perceived brightness
            float brightness = (color.r * 0.299f + color.g * 0.587f + color.b * 0.114f);
            return brightness > 0.5f ? Color.black : Color.white;
        }

        /// <summary>
        /// Gets the Base Map pixel coordinate underneath a UI icon.
        /// </summary>
        /// <param name="icon">UI icon image.</param>
        /// <returns>Texture coordinates as Vector2Int.</returns>
        private Vector2Int GetIconBaseMapPixel(Image icon)
        {
            Vector3[] worldCorners = new Vector3[4];
            icon.rectTransform.GetWorldCorners(worldCorners);

            // Calculate the center point of the icon
            Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldCorners[0] + (worldCorners[2] - worldCorners[0]) / 2);

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(BaseMap.rectTransform, screenPoint, Camera.main, out localPoint))
            {
                Rect rect = BaseMap.rectTransform.rect;
                float normX = (localPoint.x - rect.xMin) / rect.width;
                float normY = (localPoint.y - rect.yMin) / rect.height;
                int px = Mathf.Clamp(Mathf.FloorToInt(normX * BaseMap.texture.width), 0, BaseMap.texture.width - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt(normY * BaseMap.texture.height), 0, BaseMap.texture.height - 1);
                return new Vector2Int(px, py);
            }
            return new Vector2Int(-1, -1); // Invalid
        }

        #endregion

        #region Panning

        private void Update()
        {
            if (isPanning && Input.GetMouseButton(0))
            {
                Vector2 currentPointerPosition = Input.mousePosition;
                Vector2 delta = currentPointerPosition - panStartPointerPosition;
                PanContainer.anchoredPosition = panStartContainerPosition + delta;
            }
        }

        #endregion

    }

    #region Undo Manager Implementation

    /// <summary>
    /// Simple Undo Manager to handle multi-level undo for Texture2D.
    /// </summary>
    public class UndoManager
    {
        private Stack<Color[]> undoStack = new Stack<Color[]>();
        private int maxStackSize = 20;

        /// <summary>
        /// Saves the current state of the texture.
        /// </summary>
        /// <param name="tex">Texture to save.</param>
        public void SaveState(Texture2D tex)
        {
            if (tex == null) return;

            Color[] currentPixels = tex.GetPixels();
            undoStack.Push(currentPixels);

            if (undoStack.Count > maxStackSize)
            {
                // Remove the oldest entry by reversing, removing the last, and re-reversing
                List<Color[]> tempList = new List<Color[]>(undoStack);
                tempList.Reverse();
                tempList.RemoveAt(tempList.Count - 1); // Remove the oldest
                undoStack = new Stack<Color[]>(tempList);
            }

            Debug.Log("[UndoManager] State saved.");
        }

        /// <summary>
        /// Performs an undo operation on the texture.
        /// </summary>
        /// <param name="tex">Texture to undo.</param>
        public void Undo(Texture2D tex)
        {
            if (undoStack.Count == 0)
            {
                Debug.LogWarning("[UndoManager] Undo stack is empty.");
                return;
            }

            Color[] previousState = undoStack.Pop();
            tex.SetPixels(previousState);
            tex.Apply();
            Debug.Log("[UndoManager] Undo performed.");
        }
    }

    #endregion
}
