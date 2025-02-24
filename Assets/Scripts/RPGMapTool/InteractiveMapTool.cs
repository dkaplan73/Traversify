using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.IO;
using SimpleFileBrowser;

public class InteractiveMapTool : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum ToolType { None, MagicWand, PaintBrush, Eraser, Eyedropper, Pan }

    #region Public UI Fields
    [Header("General Settings")]
    public ToolType activeTool = ToolType.None;
    public bool debugMode = true;

    [Header("Base Map & Overlays")]
    [Tooltip("RawImage displaying the base map (Raycast Target enabled)")]
    public RawImage baseMapRawImage;
    [Tooltip("RawImage for the Traversable draw layer (Raycast Target disabled)")]
    public RawImage traversableDrawLayer;
    [Tooltip("RawImage for the Non-Traversable draw layer (Raycast Target disabled)")]
    public RawImage nonTraversableDrawLayer;

    [Header("Dropdowns & Input Fields")]
    [Tooltip("Area Type Dropdown: 0 = Traversable Terrain, 1 = Traversable Object, 2 = Non-Traversable Terrain, 3 = Non-Traversable Object")]
    public Dropdown areaTypeDropdown;
    [Tooltip("Map Type Dropdown: used for naming saved annotation file")]
    public Dropdown mapTypeDropdown;
    [Tooltip("Magic Wand Threshold (0-255)")]
    public InputField thresholdField;
    [Tooltip("Border Threshold (in px)")]
    public InputField borderThresholdField;
    [Tooltip("Border Color Threshold (0-255)")]
    public InputField borderColorThresholdField;

    [Header("Tool Buttons")]
    public Button clearButton;
    public Button undoButton;
    public Button borderColorButton; // Activates Eyedropper
    public Button thickenBlobButton;
    public Button panButton;
    public Button zoomInButton;
    public Button zoomOutButton;
    public Button increaseBrushSizeButton;
    public Button decreaseBrushSizeButton;
    public Button saveAnnotationButton;
    public Button selectImageButton;
    public Button wandButton;
    public Button eraserButton;
    public Button brushButton;

    [Header("Toggles & Color Sample")]
    public Toggle showTraversableToggle;
    public Toggle showNonTraversableToggle;
    public Image colorSampleImage;

    [Header("Magic Wand Settings")]
    public float magicWandTolerance = 16f / 255f; // normalized tolerance

    [Header("Paint Brush Settings")]
    public float brushSize = 10f;
    [Range(0.1f, 1f)]
    public float brushOpacity = 1f;

    [Header("Eraser Settings")]
    public float eraserSize = 10f;

    [Header("Eyedropper Settings")]
    public Color selectedBorderColor = Color.white;

    [Header("Border Settings")]
    public float borderThreshold = 2f;
    public float borderColorThreshold = 0.05f;
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
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        LogReferences();
        StartCoroutine(WaitForBaseMapTexture());
        RegisterUIEvents();
        UpdateThresholdsFromInput();
    }
    #endregion

    #region Coroutine & UI Registration
    private IEnumerator WaitForBaseMapTexture()
    {
        while (baseMapRawImage == null || baseMapRawImage.texture == null)
        {
            if (debugMode) Debug.Log("[InteractiveMapTool] Waiting for base map texture...");
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
        selectImageButton?.onClick.AddListener(SelectImage);
        wandButton?.onClick.AddListener(() => ActivateTool(ToolType.MagicWand));
        eraserButton?.onClick.AddListener(() => ActivateTool(ToolType.Eraser));
        brushButton?.onClick.AddListener(() => ActivateTool(ToolType.PaintBrush));
        showTraversableToggle?.onValueChanged.AddListener(val => traversableDrawLayer.gameObject.SetActive(val));
        showNonTraversableToggle?.onValueChanged.AddListener(val => nonTraversableDrawLayer.gameObject.SetActive(val));
        thresholdField?.onEndEdit.AddListener(s => { if (float.TryParse(s, out float t)) { magicWandTolerance = t / 255f; if (debugMode) Debug.Log("Magic Wand Tolerance updated: " + magicWandTolerance); } });
        borderThresholdField?.onEndEdit.AddListener(s => { if (float.TryParse(s, out float bt)) { borderThreshold = bt; if (debugMode) Debug.Log("Border Threshold updated: " + borderThreshold); } });
        borderColorThresholdField?.onEndEdit.AddListener(s => { if (float.TryParse(s, out float bct)) { borderColorThreshold = bct / 255f; if (debugMode) Debug.Log("Border Color Threshold updated: " + borderColorThreshold); } });
    }

    private void UpdateThresholdsFromInput()
    {
        if (thresholdField != null && float.TryParse(thresholdField.text, out float t))
            magicWandTolerance = t / 255f;
        if (borderThresholdField != null && float.TryParse(borderThresholdField.text, out float bt))
            borderThreshold = bt;
        if (borderColorThresholdField != null && float.TryParse(borderColorThresholdField.text, out float bct))
            borderColorThreshold = bct / 255f;
    }
    #endregion

    #region Initialization & Utility Methods
    private void LogReferences()
    {
        if (debugMode)
        {
            Debug.Log("[InteractiveMapTool] Logging References:");
            Debug.Log("BaseMapRawImage: " + (baseMapRawImage ? "Assigned" : "MISSING"));
            Debug.Log("TraversableDrawLayer: " + (traversableDrawLayer ? "Assigned" : "MISSING"));
            Debug.Log("NonTraversableDrawLayer: " + (nonTraversableDrawLayer ? "Assigned" : "MISSING"));
            Debug.Log("AreaTypeDropdown: " + (areaTypeDropdown ? "Assigned" : "MISSING"));
            Debug.Log("MapTypeDropdown: " + (mapTypeDropdown ? "Assigned" : "MISSING"));
        }
    }

    private void InitializeTextures()
    {
        if (baseMapRawImage == null || baseMapRawImage.texture == null)
        {
            Debug.LogError("[InteractiveMapTool] BaseMapRawImage or its texture is not assigned!");
            return;
        }
        baseMapTexture = baseMapRawImage.texture as Texture2D;
        if (baseMapTexture == null)
        {
            Debug.LogError("[InteractiveMapTool] Base map texture is not a Texture2D!");
            return;
        }
        // Ensure the base map retains its original dimensions.
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
        isInitialized = true;
        if (debugMode)
            Debug.Log("[InteractiveMapTool] Textures initialized.");
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
                Debug.Log("[InteractiveMapTool] Cleared base map image.");
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
        if (traversableTexture != null && traversableTexture.GetPixel(pos.x, pos.y).a > 0) return true;
        if (nonTraversableTexture != null && nonTraversableTexture.GetPixel(pos.x, pos.y).a > 0) return true;
        return false;
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
        if (traversableTexture == null || nonTraversableTexture == null) return;
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
    // These methods are called by the interface methods.
    private void ProcessMagicWand(Vector2 screenPos, Camera eventCamera)
    {
        if (debugMode) Debug.Log("ProcessMagicWand at " + screenPos);
        Vector2Int texCoords = ScreenPointToTextureCoords(screenPos, eventCamera);
        if (debugMode) Debug.Log("MagicWand coords: " + texCoords);
        if (texCoords.x < 0 || texCoords.x >= baseMapTexture.width || texCoords.y < 0 || texCoords.y >= baseMapTexture.height)
        {
            if (debugMode) Debug.Log("MagicWand: Coordinates out of bounds.");
            return;
        }
        Color targetColor = baseMapTexture.GetPixel(texCoords.x, texCoords.y);
        if (debugMode) Debug.Log("MagicWand target color: " + targetColor);
        int dropdownVal = (areaTypeDropdown != null) ? areaTypeDropdown.value : 0;
        Texture2D targetTexture = (dropdownVal < 2) ? traversableTexture : nonTraversableTexture;
        if (targetTexture == null)
        {
            Debug.LogError("MagicWand: Target texture is null.");
            return;
        }
        List<Vector2Int> region = FloodFill(baseMapTexture, texCoords, targetColor, magicWandTolerance);
        if (debugMode) Debug.Log("MagicWand region count: " + region.Count);
        if (region.Count > 0)
        {
            // Use effective fill color from the draw layer's color.
            Color effectiveFillColor = (dropdownVal < 2) ? traversableDrawLayer.color : nonTraversableDrawLayer.color;
            ApplyFill(targetTexture, region, effectiveFillColor);
            if (debugMode) Debug.Log("MagicWand: Fill applied.");
        }
        else if (debugMode)
        {
            Debug.Log("MagicWand: No region found.");
        }
    }

    private void ProcessPaintBrush(Vector2 screenPos, Camera eventCamera)
    {
        if (debugMode) Debug.Log("ProcessPaintBrush at " + screenPos);
        Vector2Int texCoords = ScreenPointToTextureCoords(screenPos, eventCamera);
        if (debugMode) Debug.Log("PaintBrush coords: " + texCoords);
        int dropdownVal = (areaTypeDropdown != null) ? areaTypeDropdown.value : 0;
        Texture2D targetTexture = (dropdownVal < 2) ? traversableTexture : nonTraversableTexture;
        if (targetTexture == null)
        {
            Debug.LogError("PaintBrush: Target texture is null.");
            return;
        }
        // Use effective brush color from the draw layer's color.
        Color effectiveColor = (dropdownVal < 2) ? traversableDrawLayer.color : nonTraversableDrawLayer.color;
        effectiveColor.a *= brushOpacity;
        PaintBrushStroke(targetTexture, texCoords, Mathf.CeilToInt(brushSize), effectiveColor);
        if (debugMode) Debug.Log("PaintBrush: Stroke applied at " + texCoords);
    }

    private void ProcessEraser(Vector2 screenPos, Camera eventCamera)
    {
        if (debugMode) Debug.Log("ProcessEraser at " + screenPos);
        Vector2Int texCoords = ScreenPointToTextureCoords(screenPos, eventCamera);
        if (debugMode) Debug.Log("Eraser coords: " + texCoords);
        int dropdownVal = (areaTypeDropdown != null) ? areaTypeDropdown.value : 0;
        Texture2D targetTexture = (dropdownVal < 2) ? traversableTexture : nonTraversableTexture;
        if (targetTexture == null)
        {
            Debug.LogError("Eraser: Target texture is null.");
            return;
        }
        EraserStroke(targetTexture, texCoords, Mathf.CeilToInt(eraserSize));
        if (debugMode) Debug.Log("Eraser: Stroke applied at " + texCoords);
    }

    private void ProcessEyeDropper(Vector2 screenPos, Camera eventCamera)
    {
        if (debugMode) Debug.Log("ProcessEyeDropper at " + screenPos);
        Vector2Int texCoords = ScreenPointToTextureCoords(screenPos, eventCamera);
        if (debugMode) Debug.Log("Eyedropper coords: " + texCoords);
        if (texCoords.x < 0 || texCoords.x >= baseMapTexture.width || texCoords.y < 0 || texCoords.y >= baseMapTexture.height)
        {
            if (debugMode) Debug.Log("Eyedropper: Coordinates out of bounds.");
            return;
        }
        selectedBorderColor = baseMapTexture.GetPixel(texCoords.x, texCoords.y);
        if (debugMode) Debug.Log("Eyedropper: Selected border color = " + selectedBorderColor);
    }
    #endregion

    #region Interface Methods (Pointer & Drag)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (baseMapRawImage == null || baseMapTexture == null)
        {
            if (debugMode) Debug.Log("Base map not assigned. Ignoring click.");
            return;
        }
        if (!RectTransformUtility.RectangleContainsScreenPoint(baseMapRawImage.rectTransform, eventData.position, eventData.pressEventCamera))
        {
            if (debugMode) Debug.Log("Click outside base map. Ignoring.");
            return;
        }
        if (panMode) return;
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
                if (debugMode) Debug.Log("No active tool. Ignoring click.");
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

    public void OnEndDrag(PointerEventData eventData) { isDragging = false; }
    #endregion

    #region UI Tool Methods
    public void ActivateTool(ToolType tool)
    {
        activeTool = tool;
        if (debugMode) Debug.Log("Activated tool: " + activeTool);
    }

    public void DeactivateTool()
    {
        activeTool = ToolType.None;
        if (debugMode) Debug.Log("Deactivated tool.");
    }

    public void ClearAllPaint()
    {
        SaveUndoState();
        if (traversableTexture != null) ClearTexture(traversableTexture, Color.clear);
        if (nonTraversableTexture != null) ClearTexture(nonTraversableTexture, Color.clear);
        if (debugMode) Debug.Log("Cleared all paint from overlays.");
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
            if (debugMode) Debug.Log("Undid last action.");
        }
        else if (debugMode)
            Debug.Log("Nothing to undo.");
    }

    public void ThickenBlobs()
    {
        SaveUndoState();
        if (traversableTexture != null) ThickenTexture(traversableTexture);
        if (nonTraversableTexture != null) ThickenTexture(nonTraversableTexture);
        if (debugMode) Debug.Log("Thickened blobs by 1px.");
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
        if (debugMode) Debug.Log("Pan mode " + (panMode ? "enabled" : "disabled"));
    }

    public void ZoomIn()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale *= 1.1f;
            if (debugMode) Debug.Log("Zoomed In. Scale: " + rectTransform.localScale);
        }
    }

    public void ZoomOut()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale *= 0.9f;
            if (debugMode) Debug.Log("Zoomed Out. Scale: " + rectTransform.localScale);
        }
    }

    public void IncreaseBrushSize()
    {
        brushSize += 1f;
        if (debugMode) Debug.Log("Brush size increased to " + brushSize);
    }

    public void DecreaseBrushSize()
    {
        brushSize = Mathf.Max(1f, brushSize - 1f);
        if (debugMode) Debug.Log("Brush size decreased to " + brushSize);
    }

    public void SaveAnnotation()
    {
        int width = traversableTexture.width, height = traversableTexture.height;
        Texture2D merged = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] mergedPixels = new Color[width * height];
        Color[] travPixels = traversableTexture.GetPixels();
        Color[] nonTravPixels = nonTraversableTexture.GetPixels();
        for (int i = 0; i < mergedPixels.Length; i++)
            mergedPixels[i] = Color.Lerp(travPixels[i], nonTravPixels[i], nonTravPixels[i].a);
        merged.SetPixels(mergedPixels);
        merged.Apply();
        string mapType = (mapTypeDropdown != null) ? mapTypeDropdown.options[mapTypeDropdown.value].text : "Map";
        string filename = "Annotation_" + mapType + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        byte[] pngData = merged.EncodeToPNG();
        if (pngData != null)
        {
            File.WriteAllBytes(Application.dataPath + "/" + filename, pngData);
            if (debugMode) Debug.Log("Saved annotation as " + filename);
            ClearBaseImage();
        }
        else Debug.LogError("Failed to encode annotation to PNG.");
    }

    public void SelectImage()
    {
        if (debugMode) Debug.Log("Opening file browser...");
        FileBrowser.ShowLoadDialog(
            (string[] paths) =>
            {
                if (paths != null && paths.Length > 0)
                {
                    string path = paths[0];
                    if (debugMode) Debug.Log("Selected file: " + path);
                    StartCoroutine(LoadTextureFromFile(path));
                }
            },
            () => { if (debugMode) Debug.Log("File selection cancelled."); },
            FileBrowser.PickMode.Files, false, "", "Select Image", "Load"
        );
    }

    private IEnumerator LoadTextureFromFile(string path)
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture("file:///" + path))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
                Debug.LogError("Error loading image: " + www.error);
            else
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(www);
                if (tex != null)
                {
                    ClearBaseImage();
                    baseMapRawImage.texture = tex;
                    baseMapTexture = tex;
                    InitializeTextures();
                    if (debugMode) Debug.Log("Base image loaded and overlays reinitialized.");
                }
                else Debug.LogError("Loaded texture is null.");
            }
        }
    }
    #endregion
}
