using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.IO;
using TMPro; // For TextMeshPro support

// AWS namespaces – ensure you have AWSSDK.Core and AWSSDK.S3 imported.
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;

public enum ToolType { None, MagicWand, PaintBrush, Eraser, Eyedropper, Pan }

public class UnifiedMapTool : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    #region Public UI Fields
    [Header("General Settings")]
    [Tooltip("Select the active tool.")]
    public ToolType activeTool = ToolType.None;
    [Tooltip("Enable debug logs.")]
    public bool debugMode = true;

    [Header("Base Map & Overlays (Assigned from BaseMapContainer)")]
    [Tooltip("RawImage that displays the base map (child: BaseMap).")]
    public RawImage baseMapRawImage;
    [Tooltip("RawImage for the traversable overlay (child: TraversableDrawLayer).")]
    public RawImage traversableDrawLayer;
    [Tooltip("RawImage for the non-traversable overlay (child: NonTraversableDrawLayer).")]
    public RawImage nonTraversableDrawLayer;
    [Tooltip("RawImage for the ornamental overlay (child: OrnamentalDrawLayer).")]
    public RawImage ornamentalDrawLayer;

    [Header("Dropdowns & Input Fields (Assigned from UI Panel)")]
    [Tooltip("Dropdown for area type (e.g., Traversable Terrain, Object, etc.).")]
    public Dropdown areaTypeDropdown;
    [Tooltip("Dropdown for naming the annotation file (MapTypeDropdown).")]
    public Dropdown mapTypeDropdown;
    [Tooltip("Input field for Magic Wand threshold (SetThresholdField).")]
    public InputField thresholdField;
    [Tooltip("Input field for Border Threshold (BorderThicknessField).")]
    public InputField borderThresholdField;
    [Tooltip("Input field for Border Color Threshold (BorderColorThresholdField).")]
    public InputField borderColorThresholdField;

    [Header("Tool Buttons (Assigned from UI Panel > Buttons)")]
    public Button clearButton;
    public Button undoButton;
    public Button borderColorButton;
    public Button thickenBlobButton;
    public Button panButton;
    public Button zoomInButton;
    public Button zoomOutButton;
    public Button increaseBrushSizeButton;
    public Button decreaseBrushSizeButton;
    public Button saveAnnotationButton;
    [Tooltip("Button to open the file browser (OpenBrowserButton).")]
    public Button selectImageButton;
    public Button wandButton;
    public Button eraserButton;
    public Button brushButton;

    [Header("Toggles & Color Sample (Assigned from UI Panel and UI Elements)")]
    public Toggle showTraversableToggle;
    public Toggle showNonTraversableToggle;
    [Tooltip("Image that displays the currently selected color (from UI Elements > ColorSample).")]
    public Image colorSampleImage;

    [Header("Magic Wand & Brush Settings")]
    public float magicWandTolerance = 16f / 255f;
    public float brushSize = 10f;
    [Range(0.1f, 1f)]
    public float brushOpacity = 1f;
    public float eraserSize = 10f;

    [Header("Eyedropper & Border Settings")]
    public Color selectedBorderColor = Color.white;
    public float borderThreshold = 2f;
    public float borderColorThreshold = 0.05f;
    #endregion

    #region AWS S3 Settings
    [Header("AWS S3 Settings")]
    public string awsAccessKey = "YOUR_ACCESS_KEY";
    public string awsSecretKey = "YOUR_SECRET_KEY";
    public string bucketName = "your-bucket-name";
    public string awsRegion = "us-west-2";
    #endregion

    #region Heat UI File Browser (Assigned from FileBrowserDialog in your hierarchy)
    [Header("Heat UI File Browser")]
    [Tooltip("The parent Modal GameObject that contains the file browser UI (FileBrowserDialog panel).")]
    public GameObject modalContainer;
    [Tooltip("The Content transform of the ScrollView hosting file/folder buttons.")]
    public Transform heatUIFileListContainer;
    [Tooltip("Prefab for a folder item button (should include a child text component, preferably TMP).")]
    public GameObject heatUIFolderItemPrefab;
    [Tooltip("Prefab for a file item button (should include a child text component, preferably TMP).")]
    public GameObject heatUIFileItemPrefab;
    [Tooltip("The Up Button for navigating up one folder.")]
    public Button heatUIUpButton;
    #endregion

    #region Private Fields
    private Texture2D baseMapTexture;
    private Texture2D traversableTexture;
    private Texture2D nonTraversableTexture;
    private RectTransform rectTransform;

    private class UndoState { public Color[] travPixels, nonTravPixels; public int width, height; }
    private Stack<UndoState> undoStack = new Stack<UndoState>();

    private bool isDragging = false;
    private Vector2 lastDragPos;
    private const float MIN_DRAG_DISTANCE = 2f;
    private bool panMode = false;
    private bool isInitialized = false;

    private IAmazonS3 s3Client;
    private string currentPrefix = "";
    #endregion

    #region S3 Listing Helper Class
    private class S3BucketListing
    {
        public List<string> FolderNames = new List<string>();
        public List<string> FolderPrefixes = new List<string>();
        public List<string> FileNames = new List<string>();
        public List<S3Object> FileObjects = new List<S3Object>();
    }
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        LogReferences();

        // Ensure all layers remain visible at startup.
        if (showTraversableToggle != null) showTraversableToggle.isOn = true;
        if (showNonTraversableToggle != null) showNonTraversableToggle.isOn = true;
        if (traversableDrawLayer != null) traversableDrawLayer.gameObject.SetActive(true);
        if (nonTraversableDrawLayer != null) nonTraversableDrawLayer.gameObject.SetActive(true);
        if (ornamentalDrawLayer != null) ornamentalDrawLayer.gameObject.SetActive(true);

        StartCoroutine(WaitForBaseMapTexture());
        RegisterUIEvents();
        UpdateThresholdsFromInput();

        if (heatUIUpButton != null)
            heatUIUpButton.onClick.AddListener(NavigateUpS3);
    }
    #endregion

    #region Coroutines & Registration
    private IEnumerator WaitForBaseMapTexture()
    {
        while (baseMapRawImage == null || baseMapRawImage.texture == null)
        {
            if (debugMode)
                Debug.Log("[UnifiedMapTool] Waiting for base map texture...");
            yield return null;
        }
        InitializeTextures();
    }

    private void RegisterUIEvents()
    {
        clearButton?.onClick.AddListener(ClearAllPaint);
        undoButton?.onClick.AddListener(UndoAction);
        borderColorButton?.onClick.AddListener(() => ActivateTool(ToolType.Eyedropper));
        thickenBlobButton?.onClick.AddListener(ThickenBlobs);
        panButton?.onClick.AddListener(TogglePanMode);
        zoomInButton?.onClick.AddListener(ZoomIn);
        zoomOutButton?.onClick.AddListener(ZoomOut);
        increaseBrushSizeButton?.onClick.AddListener(IncreaseBrushSize);
        decreaseBrushSizeButton?.onClick.AddListener(DecreaseBrushSize);
        saveAnnotationButton?.onClick.AddListener(() => { SaveAnnotation(); ClearBaseImage(); });
        selectImageButton?.onClick.AddListener(OpenS3Browser);
        wandButton?.onClick.AddListener(() => ActivateTool(ToolType.MagicWand));
        eraserButton?.onClick.AddListener(() => ActivateTool(ToolType.Eraser));
        brushButton?.onClick.AddListener(() => ActivateTool(ToolType.PaintBrush));
        showTraversableToggle?.onValueChanged.AddListener(val =>
        {
            if (traversableDrawLayer != null)
                traversableDrawLayer.gameObject.SetActive(val);
        });
        showNonTraversableToggle?.onValueChanged.AddListener(val =>
        {
            if (nonTraversableDrawLayer != null)
                nonTraversableDrawLayer.gameObject.SetActive(val);
        });
        thresholdField?.onEndEdit.AddListener(s =>
        {
            if (float.TryParse(s, out float t))
            {
                magicWandTolerance = t / 255f;
                if (debugMode)
                    Debug.Log("Magic Wand Tolerance set to: " + magicWandTolerance);
            }
            else
            {
                Debug.LogError("Failed to parse threshold input: " + s);
            }
        });
        borderThresholdField?.onEndEdit.AddListener(s =>
        {
            if (float.TryParse(s, out float bt))
            {
                borderThreshold = bt;
                if (debugMode)
                    Debug.Log("Border Threshold set to: " + borderThreshold);
            }
            else
            {
                Debug.LogError("Failed to parse border threshold input: " + s);
            }
        });
        borderColorThresholdField?.onEndEdit.AddListener(s =>
        {
            if (float.TryParse(s, out float bct))
            {
                borderColorThreshold = bct / 255f;
                if (debugMode)
                    Debug.Log("Border Color Threshold set to: " + borderColorThreshold);
            }
            else
            {
                Debug.LogError("Failed to parse border color threshold input: " + s);
            }
        });
    }
    #endregion

    #region Initialization & Utility Methods
    private void LogReferences()
    {
        if (debugMode)
        {
            Debug.Log("[UnifiedMapTool] Logging References:");
            Debug.Log("BaseMapRawImage: " + (baseMapRawImage ? "Assigned" : "MISSING"));
            Debug.Log("TraversableDrawLayer: " + (traversableDrawLayer ? "Assigned" : "MISSING"));
            Debug.Log("NonTraversableDrawLayer: " + (nonTraversableDrawLayer ? "Assigned" : "MISSING"));
            Debug.Log("OrnamentalDrawLayer: " + (ornamentalDrawLayer ? "Assigned" : "MISSING"));
            Debug.Log("AreaTypeDropdown: " + (areaTypeDropdown ? "Assigned" : "MISSING"));
            Debug.Log("MapTypeDropdown: " + (mapTypeDropdown ? "Assigned" : "MISSING"));
            Debug.Log("Modal Container (FileBrowserDialog): " + (modalContainer ? "Assigned" : "MISSING"));
        }
    }

    private void InitializeTextures()
    {
        if (baseMapRawImage == null || baseMapRawImage.texture == null)
        {
            Debug.LogError("[UnifiedMapTool] BaseMapRawImage or its texture is not assigned!");
            return;
        }
        baseMapTexture = baseMapRawImage.texture as Texture2D;
        if (baseMapTexture == null)
        {
            Debug.LogError("[UnifiedMapTool] Base map texture is not a Texture2D!");
            return;
        }
        baseMapRawImage.rectTransform.sizeDelta = new Vector2(baseMapTexture.width, baseMapTexture.height);
        int width = baseMapTexture.width;
        int height = baseMapTexture.height;
        traversableTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        nonTraversableTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        ClearTexture(traversableTexture, Color.clear);
        ClearTexture(nonTraversableTexture, Color.clear);
        traversableTexture.Apply();
        nonTraversableTexture.Apply();
        if (traversableDrawLayer != null)
        {
            traversableDrawLayer.texture = traversableTexture;
            traversableDrawLayer.rectTransform.sizeDelta = new Vector2(width, height);
            traversableDrawLayer.rectTransform.position = baseMapRawImage.rectTransform.position;
        }
        if (nonTraversableDrawLayer != null)
        {
            nonTraversableDrawLayer.texture = nonTraversableTexture;
            nonTraversableDrawLayer.rectTransform.sizeDelta = new Vector2(width, height);
            nonTraversableDrawLayer.rectTransform.position = baseMapRawImage.rectTransform.position;
        }
        if (ornamentalDrawLayer != null)
        {
            ornamentalDrawLayer.rectTransform.sizeDelta = new Vector2(width, height);
            ornamentalDrawLayer.rectTransform.position = baseMapRawImage.rectTransform.position;
        }
        isInitialized = true;
        if (debugMode)
            Debug.Log("[UnifiedMapTool] Textures initialized.");
    }

    private void ClearTexture(Texture2D texture, Color clearColor)
    {
        Color[] pixels = new Color[texture.width * texture.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clearColor;
        texture.SetPixels(pixels);
        texture.Apply();
    }

    private void ClearBaseImage()
    {
        if (baseMapTexture != null)
        {
            ClearTexture(baseMapTexture, Color.clear);
            baseMapRawImage.texture = baseMapTexture;
            if (debugMode)
                Debug.Log("[UnifiedMapTool] Cleared base map image.");
        }
    }

    private void UpdateThresholdsFromInput()
    {
        if (thresholdField != null && float.TryParse(thresholdField.text, out float t))
        {
            magicWandTolerance = t / 255f;
            if (debugMode)
                Debug.Log("Magic Wand Tolerance updated to " + magicWandTolerance);
        }
        if (borderThresholdField != null && float.TryParse(borderThresholdField.text, out float bt))
        {
            borderThreshold = bt;
            if (debugMode)
                Debug.Log("Border Threshold updated to " + borderThreshold);
        }
        if (borderColorThresholdField != null && float.TryParse(borderColorThresholdField.text, out float bct))
        {
            borderColorThreshold = bct / 255f;
            if (debugMode)
                Debug.Log("Border Color Threshold updated to " + borderColorThreshold);
        }
    }

    private Vector2Int ScreenPointToTextureCoords(Vector2 screenPos, Camera eventCamera = null)
    {
        RectTransform rt = baseMapRawImage.rectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, eventCamera, out Vector2 localPoint))
        {
            Vector2 pivot = rt.pivot;
            Vector2 size = rt.rect.size;
            float xNorm = (localPoint.x + pivot.x * size.x) / size.x;
            float yNorm = (localPoint.y + pivot.y * size.y) / size.y;
            int x = Mathf.RoundToInt(xNorm * baseMapTexture.width);
            int y = Mathf.RoundToInt(yNorm * baseMapTexture.height);
            return new Vector2Int(x, y);
        }
        return Vector2Int.zero;
    }

    private List<Vector2Int> FloodFill(Texture2D texture, Vector2Int startPos, Color targetColor, float tolerance)
    {
        List<Vector2Int> region = new List<Vector2Int>();
        bool[,] visited = new bool[texture.width, texture.height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(startPos);
        visited[startPos.x, startPos.y] = true;
        while (queue.Count > 0)
        {
            Vector2Int pos = queue.Dequeue();
            Color current = texture.GetPixel(pos.x, pos.y);
            if (ColorWithinTolerance(current, targetColor, tolerance))
            {
                region.Add(pos);
                foreach (Vector2Int offset in new Vector2Int[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    Vector2Int neighbor = pos + offset;
                    if (neighbor.x >= 0 && neighbor.x < texture.width &&
                        neighbor.y >= 0 && neighbor.y < texture.height &&
                        !visited[neighbor.x, neighbor.y])
                    {
                        visited[neighbor.x, neighbor.y] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
        return region;
    }

    private bool ColorWithinTolerance(Color a, Color b, float tolerance)
    {
        return Mathf.Abs(a.r - b.r) <= tolerance &&
               Mathf.Abs(a.g - b.g) <= tolerance &&
               Mathf.Abs(a.b - b.b) <= tolerance &&
               Mathf.Abs(a.a - b.a) <= tolerance;
    }

    private bool IsPixelPainted(Vector2Int pos)
    {
        return (traversableTexture != null && traversableTexture.GetPixel(pos.x, pos.y).a > 0) ||
               (nonTraversableTexture != null && nonTraversableTexture.GetPixel(pos.x, pos.y).a > 0);
    }

    private void ApplyFill(Texture2D texture, List<Vector2Int> region, Color fillColor)
    {
        foreach (Vector2Int pos in region)
        {
            if (!IsPixelPainted(pos))
                texture.SetPixel(pos.x, pos.y, fillColor);
        }
        texture.Apply();
    }

    private void PaintBrushStroke(Texture2D texture, Vector2Int center, int size, Color color)
    {
        int halfSize = size / 2;
        for (int x = center.x - halfSize; x <= center.x + halfSize; x++)
        {
            for (int y = center.y - halfSize; y <= center.y + halfSize; y++)
            {
                if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (!IsPixelPainted(pos))
                    {
                        float dx = x - center.x;
                        float dy = y - center.y;
                        if (dx * dx + dy * dy <= halfSize * halfSize)
                            texture.SetPixel(x, y, color);
                    }
                }
            }
        }
        texture.Apply();
    }

    private void EraserStroke(Texture2D texture, Vector2Int center, int size)
    {
        int halfSize = size / 2;
        Color clearColor = Color.clear;
        for (int x = center.x - halfSize; x <= center.x + halfSize; x++)
        {
            for (int y = center.y - halfSize; y <= center.y + halfSize; y++)
            {
                if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    if (dx * dx + dy * dy <= halfSize * halfSize)
                        texture.SetPixel(x, y, clearColor);
                }
            }
        }
        texture.Apply();
    }

    private void SaveUndoState()
    {
        if (traversableTexture == null || nonTraversableTexture == null)
            return;
        UndoState state = new UndoState
        {
            width = traversableTexture.width,
            height = traversableTexture.height,
            travPixels = traversableTexture.GetPixels(),
            nonTravPixels = nonTraversableTexture.GetPixels()
        };
        undoStack.Push(state);
        if (debugMode)
            Debug.Log("Saved undo state.");
    }
    #endregion

    #region Tool Processing
    private void ProcessMagicWand(Vector2 screenPos, Camera eventCamera)
    {
        if (debugMode)
            Debug.Log("ProcessMagicWand at " + screenPos);
        Vector2Int texCoords = ScreenPointToTextureCoords(screenPos, eventCamera);
        if (debugMode)
            Debug.Log("MagicWand coords: " + texCoords);
        if (texCoords.x < 0 || texCoords.x >= baseMapTexture.width || texCoords.y < 0 || texCoords.y >= baseMapTexture.height)
        {
            Debug.LogWarning("MagicWand: Coordinates out of bounds.");
            return;
        }
        Color targetColor = baseMapTexture.GetPixel(texCoords.x, texCoords.y);
        if (debugMode)
            Debug.Log("MagicWand target color: " + targetColor);
        int dropdownVal = areaTypeDropdown ? areaTypeDropdown.value : 0;
        Texture2D targetTexture = dropdownVal < 2 ? traversableTexture : nonTraversableTexture;
        if (targetTexture == null)
        {
            Debug.LogError("MagicWand: Target texture is null.");
            return;
        }
        List<Vector2Int> region = FloodFill(baseMapTexture, texCoords, targetColor, magicWandTolerance);
        if (debugMode)
            Debug.Log("MagicWand region count: " + region.Count);
        if (region.Count > 0)
        {
            Color effectiveFillColor = dropdownVal < 2 ? traversableDrawLayer.color : nonTraversableDrawLayer.color;
            ApplyFill(targetTexture, region, effectiveFillColor);
            if (debugMode)
                Debug.Log("MagicWand: Fill applied.");
        }
        else
        {
            if (debugMode)
                Debug.Log("MagicWand: No region found.");
        }
    }

    private void ProcessPaintBrush(Vector2 screenPos, Camera eventCamera)
    {
        if (debugMode)
            Debug.Log("ProcessPaintBrush at " + screenPos);
        Vector2Int texCoords = ScreenPointToTextureCoords(screenPos, eventCamera);
        if (debugMode)
            Debug.Log("PaintBrush coords: " + texCoords);
        int dropdownVal = areaTypeDropdown ? areaTypeDropdown.value : 0;
        Texture2D targetTexture = dropdownVal < 2 ? traversableTexture : nonTraversableTexture;
        if (targetTexture == null)
        {
            Debug.LogError("PaintBrush: Target texture is null.");
            return;
        }
        Color effectiveColor = dropdownVal < 2 ? traversableDrawLayer.color : nonTraversableDrawLayer.color;
        effectiveColor.a *= brushOpacity;
        PaintBrushStroke(targetTexture, texCoords, Mathf.CeilToInt(brushSize), effectiveColor);
        if (debugMode)
            Debug.Log("PaintBrush: Stroke applied at " + texCoords);
    }

    private void ProcessEraser(Vector2 screenPos, Camera eventCamera)
    {
        if (debugMode)
            Debug.Log("ProcessEraser at " + screenPos);
        Vector2Int texCoords = ScreenPointToTextureCoords(screenPos, eventCamera);
        if (debugMode)
            Debug.Log("Eraser coords: " + texCoords);
        int dropdownVal = areaTypeDropdown ? areaTypeDropdown.value : 0;
        Texture2D targetTexture = dropdownVal < 2 ? traversableTexture : nonTraversableTexture;
        if (targetTexture == null)
        {
            Debug.LogError("Eraser: Target texture is null.");
            return;
        }
        EraserStroke(targetTexture, texCoords, Mathf.CeilToInt(eraserSize));
        if (debugMode)
            Debug.Log("Eraser: Stroke applied at " + texCoords);
    }

    private void ProcessEyeDropper(Vector2 screenPos, Camera eventCamera)
    {
        if (debugMode)
            Debug.Log("ProcessEyeDropper at " + screenPos);
        Vector2Int texCoords = ScreenPointToTextureCoords(screenPos, eventCamera);
        if (debugMode)
            Debug.Log("Eyedropper coords: " + texCoords);
        if (texCoords.x < 0 || texCoords.x >= baseMapTexture.width || texCoords.y < 0 || texCoords.y >= baseMapTexture.height)
        {
            Debug.LogWarning("Eyedropper: Coordinates out of bounds.");
            return;
        }
        selectedBorderColor = baseMapTexture.GetPixel(texCoords.x, texCoords.y);
        if (debugMode)
            Debug.Log("Eyedropper: Selected border color = " + selectedBorderColor);
    }
    #endregion

    #region Interface Methods (Pointer & Drag)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (baseMapRawImage == null || baseMapTexture == null)
        {
            Debug.LogWarning("Base map not assigned. Ignoring click.");
            return;
        }
        if (!RectTransformUtility.RectangleContainsScreenPoint(baseMapRawImage.rectTransform, eventData.position, eventData.pressEventCamera))
        {
            Debug.Log("Click outside base map. Ignoring.");
            return;
        }
        if (panMode)
            return;
        SaveUndoState();
        if (debugMode)
            Debug.Log("Pointer Click at " + eventData.position + ", button " + eventData.button);
        switch (activeTool)
        {
            case ToolType.MagicWand:
                ProcessMagicWand(eventData.position, eventData.pressEventCamera);
                break;
            case ToolType.PaintBrush:
                ProcessPaintBrush(eventData.position, eventData.pressEventCamera);
                break;
            case ToolType.Eraser:
                ProcessEraser(eventData.position, eventData.pressEventCamera);
                break;
            case ToolType.Eyedropper:
                ProcessEyeDropper(eventData.position, eventData.pressEventCamera);
                if (colorSampleImage != null)
                    colorSampleImage.color = selectedBorderColor;
                break;
            default:
                Debug.Log("No active tool. Ignoring click.");
                break;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if ((activeTool == ToolType.PaintBrush || activeTool == ToolType.Eraser) && !panMode)
        {
            isDragging = true;
            lastDragPos = eventData.position;
            if (activeTool == ToolType.PaintBrush)
                ProcessPaintBrush(eventData.position, eventData.pressEventCamera);
            else if (activeTool == ToolType.Eraser)
                ProcessEraser(eventData.position, eventData.pressEventCamera);
        }
        else if (panMode)
        {
            isDragging = true;
            lastDragPos = eventData.position;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if ((activeTool == ToolType.PaintBrush || activeTool == ToolType.Eraser) && isDragging && !panMode)
        {
            if (Vector2.Distance(lastDragPos, eventData.position) >= MIN_DRAG_DISTANCE)
            {
                if (activeTool == ToolType.PaintBrush)
                    ProcessPaintBrush(eventData.position, eventData.pressEventCamera);
                else if (activeTool == ToolType.Eraser)
                    ProcessEraser(eventData.position, eventData.pressEventCamera);
                lastDragPos = eventData.position;
            }
        }
        else if (panMode && isDragging)
        {
            Vector2 delta = eventData.position - lastDragPos;
            rectTransform.anchoredPosition += delta;
            lastDragPos = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }
    #endregion

    #region UI Tool Methods
    public void ActivateTool(ToolType tool)
    {
        activeTool = tool;
        if (debugMode)
            Debug.Log("Activated tool: " + activeTool);
    }

    public void DeactivateTool()
    {
        activeTool = ToolType.None;
        if (debugMode)
            Debug.Log("Deactivated tool.");
    }

    public void ClearAllPaint()
    {
        SaveUndoState();
        if (traversableTexture != null)
            ClearTexture(traversableTexture, Color.clear);
        if (nonTraversableTexture != null)
            ClearTexture(nonTraversableTexture, Color.clear);
        if (debugMode)
            Debug.Log("Cleared all paint from overlays.");
    }

    public void UndoAction()
    {
        if (undoStack.Count > 0)
        {
            UndoState state = undoStack.Pop();
            if (traversableTexture != null && state.travPixels != null)
            {
                traversableTexture.SetPixels(state.travPixels);
                traversableTexture.Apply();
            }
            if (nonTraversableTexture != null && state.nonTravPixels != null)
            {
                nonTraversableTexture.SetPixels(state.nonTravPixels);
                nonTraversableTexture.Apply();
            }
            if (debugMode)
                Debug.Log("Undid last action.");
        }
        else
        {
            Debug.Log("Nothing to undo.");
        }
    }

    public void ThickenBlobs()
    {
        SaveUndoState();
        if (traversableTexture != null)
            ThickenTexture(traversableTexture);
        if (nonTraversableTexture != null)
            ThickenTexture(nonTraversableTexture);
        if (debugMode)
            Debug.Log("Thickened blobs by 1px.");
    }

    private void ThickenTexture(Texture2D texture)
    {
        int width = texture.width, height = texture.height;
        Color[] original = texture.GetPixels();
        Color[] thickened = new Color[original.Length];
        System.Array.Copy(original, thickened, original.Length);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = y * width + x;
                if (original[idx].a > 0)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                int nIdx = ny * width + nx;
                                if (original[nIdx].a == 0)
                                    thickened[nIdx] = original[idx];
                            }
                        }
                    }
                }
            }
        }
        texture.SetPixels(thickened);
        texture.Apply();
    }

    public void TogglePanMode()
    {
        panMode = !panMode;
        if (debugMode)
            Debug.Log("Pan mode " + (panMode ? "enabled" : "disabled"));
    }

    public void ZoomIn()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale *= 1.1f;
            if (debugMode)
                Debug.Log("Zoomed In. Scale: " + rectTransform.localScale);
        }
    }

    public void ZoomOut()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale *= 0.9f;
            if (debugMode)
                Debug.Log("Zoomed Out. Scale: " + rectTransform.localScale);
        }
    }

    public void IncreaseBrushSize()
    {
        brushSize += 1f;
        if (debugMode)
            Debug.Log("Brush size increased to " + brushSize);
    }

    public void DecreaseBrushSize()
    {
        brushSize = Mathf.Max(1f, brushSize - 1f);
        if (debugMode)
            Debug.Log("Brush size decreased to " + brushSize);
    }

    public void SaveAnnotation()
    {
        int width = traversableTexture.width, height = traversableTexture.height;
        Texture2D merged = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] mergedPixels = new Color[width * height];
        Color[] travPixels = traversableTexture.GetPixels();
        Color[] nonTravPixels = nonTraversableTexture.GetPixels();
        for (int i = 0; i < mergedPixels.Length; i++)
        {
            mergedPixels[i] = Color.Lerp(travPixels[i], nonTravPixels[i], nonTravPixels[i].a);
        }
        merged.SetPixels(mergedPixels);
        merged.Apply();
        string mapType = mapTypeDropdown ? mapTypeDropdown.options[mapTypeDropdown.value].text : "Map";
        string filename = "Annotation_" + mapType + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        byte[] pngData = merged.EncodeToPNG();
        if (pngData != null)
        {
            File.WriteAllBytes(Application.dataPath + "/" + filename, pngData);
            if (debugMode)
                Debug.Log("Saved annotation as " + filename);
            ClearBaseImage();
            UploadAnnotationToS3(Application.dataPath + "/" + filename);
        }
        else
        {
            Debug.LogError("Failed to encode annotation to PNG.");
        }
    }
    #endregion

    #region S3 Browser Integration (Heat UI)
    private void ConnectToS3()
    {
        try
        {
            AWSCredentials credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            RegionEndpoint endpoint = RegionEndpoint.GetBySystemName(awsRegion);
            s3Client = new AmazonS3Client(credentials, endpoint);
            if (debugMode)
                Debug.Log("Connected to S3 bucket " + bucketName + " in region " + awsRegion);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error connecting to S3: " + ex.Message);
        }
    }

    public void OpenS3Browser()
    {
        ConnectToS3();
        currentPrefix = "";
        StartCoroutine(ListS3BucketItems(currentPrefix, UpdateUIWithS3Items));
        if (modalContainer != null)
        {
            modalContainer.SetActive(true);
            EnsureComponentsActive(modalContainer);
            RectTransform panelRT = modalContainer.GetComponent<RectTransform>();
            if (panelRT != null)
            {
                panelRT.sizeDelta = new Vector2(Screen.width * 0.8f, Screen.height * 0.8f);
                panelRT.anchoredPosition = Vector2.zero;
            }
            modalContainer.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }
    }

    private void EnsureComponentsActive(GameObject obj)
    {
        if (!obj.activeSelf)
            obj.SetActive(true);

        Button btn = obj.GetComponent<Button>();
        if (btn != null)
            btn.enabled = true;

        Image img = obj.GetComponent<Image>();
        if (img != null)
            img.enabled = true;

        TMP_Text tmp = obj.GetComponent<TMP_Text>();
        if (tmp != null)
            tmp.enabled = true;

        Text legacyText = obj.GetComponent<Text>();
        if (legacyText != null)
            legacyText.enabled = true;

        // Add a LayoutElement to ignore parent's layout if necessary.
        LayoutElement layout = obj.GetComponent<LayoutElement>();
        if (layout == null)
            layout = obj.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        foreach (Transform child in obj.transform)
        {
            EnsureComponentsActive(child.gameObject);
        }
    }

    private int GetDepth(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return 0;
        string trimmed = prefix.TrimEnd('/');
        return trimmed.Split(new char[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private const float VerticalSpacing = 40f;
    private const float IndentPerLevel = 20f;

    private IEnumerator ListS3BucketItems(string prefix, System.Action<S3BucketListing> onCompleted)
    {
        ListObjectsV2Request request = new ListObjectsV2Request()
        {
            BucketName = bucketName,
            Prefix = prefix,
            Delimiter = "/" // simulate directories
        };

        var task = s3Client.ListObjectsV2Async(request);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError("Error listing S3 objects: " + task.Exception);
            yield break;
        }

        ListObjectsV2Response response = task.Result;
        S3BucketListing listing = new S3BucketListing();

        foreach (string commonPrefix in response.CommonPrefixes)
        {
            string folderName = commonPrefix.TrimEnd('/');
            if (folderName.Contains("/"))
                folderName = folderName.Substring(folderName.LastIndexOf('/') + 1);
            listing.FolderNames.Add(folderName);
            listing.FolderPrefixes.Add(commonPrefix);
        }

        foreach (S3Object obj in response.S3Objects)
        {
            if (obj.Key.EndsWith("/"))
                continue;
            string fileName = obj.Key;
            if (fileName.Contains("/"))
                fileName = fileName.Substring(fileName.LastIndexOf('/') + 1);
            listing.FileNames.Add(fileName);
            listing.FileObjects.Add(obj);
        }
        onCompleted?.Invoke(listing);
    }

    private void UpdateUIWithS3Items(S3BucketListing listing)
    {
        // Instead of destroying previous items, simply disable them.
        foreach (Transform child in heatUIFileListContainer)
        {
            if (child.gameObject != heatUIUpButton.gameObject)
                child.gameObject.SetActive(false);
        }

        Debug.Log("S3 Listing: " + listing.FolderNames.Count + " folders, " + listing.FileNames.Count + " files.");
        int index = 0;
        // Create folder buttons first.
        for (int i = 0; i < listing.FolderNames.Count; i++)
        {
            string folderName = listing.FolderNames[i];
            string folderPrefix = listing.FolderPrefixes[i];
            CreateFolderButton(folderName, folderPrefix, index);
            index++;
        }
        // Then create file buttons.
        for (int i = 0; i < listing.FileNames.Count; i++)
        {
            string fileName = listing.FileNames[i];
            S3Object fileObj = listing.FileObjects[i];
            CreateFileButton(fileName, fileObj, index);
            index++;
        }
    }

    private void CreateFolderButton(string folderName, string folderPrefix, int index)
    {
        if (heatUIFolderItemPrefab == null)
        {
            Debug.LogError("Folder prefab (heatUIFolderItemPrefab) is not assigned.");
            return;
        }
        GameObject folderItem = Instantiate(heatUIFolderItemPrefab, heatUIFileListContainer);
        folderItem.name = "FolderItemButton_" + folderName;

        int depth = GetDepth(folderPrefix);
        float indent = depth * IndentPerLevel;

        RectTransform rt = folderItem.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(indent, -index * VerticalSpacing);
        }

        Button btn = folderItem.GetComponent<Button>();
        if (btn != null)
        {
            Debug.Log("Creating folder button: " + folderName + " at depth " + depth + " (indent: " + indent + ")");
            StartCoroutine(UpdateButtonText(folderItem, "Open Folder: " + folderName));
            btn.interactable = true;
            EnsureComponentsActive(folderItem);
            btn.onClick.AddListener(() =>
            {
                Debug.Log("Folder button clicked: " + folderName);
                // Ensure folder prefix ends with a slash.
                if (!folderPrefix.EndsWith("/"))
                    folderPrefix += "/";
                OnFolderButtonClick(folderName, folderPrefix);
            });
        }
        else
        {
            Debug.LogError("Folder prefab does not have a Button component.");
        }
    }

    private void CreateFileButton(string fileName, S3Object fileObj, int index)
    {
        if (heatUIFileItemPrefab == null)
        {
            Debug.LogError("File prefab (heatUIFileItemPrefab) is not assigned.");
            return;
        }
        GameObject fileItem = Instantiate(heatUIFileItemPrefab, heatUIFileListContainer);
        fileItem.name = "FileItemButton_" + fileName;

        int depth = GetDepth(currentPrefix);
        float indent = depth * IndentPerLevel;

        RectTransform rt = fileItem.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(indent, -index * VerticalSpacing);
        }

        Button btn = fileItem.GetComponent<Button>();
        if (btn != null)
        {
            StartCoroutine(UpdateButtonText(fileItem, "Select File: " + fileName));
            btn.interactable = true;
            EnsureComponentsActive(fileItem);
            btn.onClick.AddListener(() => OnFileButtonClick(fileObj));
        }
        else
        {
            Debug.LogError("File prefab does not have a Button component.");
        }
    }

    private IEnumerator UpdateButtonText(GameObject item, string newText)
    {
        yield return new WaitForEndOfFrame();
        TMP_Text tmp = item.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = newText;
            if (debugMode)
                Debug.Log("Updated TMP text to: " + newText);
        }
        else
        {
            Text legacyText = item.GetComponentInChildren<Text>();
            if (legacyText != null)
                legacyText.text = newText;
            else
                Debug.LogError("No text component found in " + item.name);
        }
    }

    private void OnFolderButtonClick(string folderName, string folderPrefix)
    {
        Debug.Log("OnFolderButtonClick triggered for: " + folderName + " with prefix: " + folderPrefix);
        currentPrefix = folderPrefix;
        StartCoroutine(ListS3BucketItems(currentPrefix, UpdateUIWithS3Items));
    }

    private void OnFileButtonClick(S3Object fileObj)
    {
        Debug.Log("OnFileButtonClick triggered for key: " + fileObj.Key);
        StartCoroutine(DownloadS3File(fileObj.Key));
        if (modalContainer != null)
            modalContainer.SetActive(false);
    }

    public void NavigateUpS3()
    {
        if (string.IsNullOrEmpty(currentPrefix))
            return;
        string trimmed = currentPrefix.TrimEnd('/');
        int lastSlash = trimmed.LastIndexOf('/');
        currentPrefix = (lastSlash >= 0) ? trimmed.Substring(0, lastSlash + 1) : "";
        StartCoroutine(ListS3BucketItems(currentPrefix, UpdateUIWithS3Items));
    }

    private IEnumerator DownloadS3File(string key)
    {
        GetObjectRequest request = new GetObjectRequest()
        {
            BucketName = bucketName,
            Key = key
        };

        var task = s3Client.GetObjectAsync(request);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError("Error downloading S3 file: " + task.Exception);
            yield break;
        }

        GetObjectResponse response = task.Result;
        using (Stream stream = response.ResponseStream)
        {
            byte[] buffer = new byte[response.ContentLength];
            stream.Read(buffer, 0, buffer.Length);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(buffer);
            baseMapRawImage.texture = tex;
            baseMapTexture = tex;
            InitializeTextures();
        }
        if (modalContainer != null)
            modalContainer.SetActive(false);
        Debug.Log("Downloaded S3 file: " + key);
    }

    private async void UploadAnnotationToS3(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;
        try
        {
            var putRequest = new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = Path.GetFileName(filePath),
                FilePath = filePath
            };
            await s3Client.PutObjectAsync(putRequest);
            Debug.Log("Annotation uploaded to S3: " + putRequest.Key);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to upload annotation to S3: " + ex.Message);
        }
    }
    #endregion

    // [Other parts of the script remain unchanged...]
}
