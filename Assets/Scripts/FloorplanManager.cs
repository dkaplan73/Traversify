using UnityEngine;

public class FloorPlanProcessor : MonoBehaviour
{
    [Header("Input Settings")]
    [Tooltip("Assign your map image (must have Read/Write enabled).")]
    [SerializeField] private Texture2D inputTexture;

    [Header("Map Object Settings")]
    [Tooltip("Assign the GameObject that will display the processed map. This can be a Terrain or any GameObject with a Renderer.")]
    [SerializeField] private GameObject mapObject;

    [Header("Detection Parameters")]
    [Tooltip("Minimum brightness for a pixel to be considered as potential floor (0 = black, 1 = white).")]
    [Range(0f, 1f)]
    [SerializeField] private float floorBrightnessThreshold = 0.6f;
    
    [Tooltip("Local variance threshold below which the area is considered uniform (likely a grid area).")]
    [SerializeField] private float varianceThreshold = 0.02f;
    
    [Tooltip("Radius for the local neighborhood (a value of 1 means a 3x3 pixel window).")]
    [SerializeField] private int localWindowSize = 1;

    // The processed texture that will display only traversable (grid) areas.
    private Texture2D processedTexture;

    private int textureWidth;
    private int textureHeight;

    void Start()
    {
        // Verify input texture.
        if (inputTexture == null)
        {
            Debug.LogError("Input texture not assigned! Please assign a map texture in the Inspector.");
            return;
        }
        if (!inputTexture.isReadable)
        {
            Debug.LogError("Input texture is not readable! Enable Read/Write in the Texture Import Settings.");
            return;
        }
        // Verify map object.
        if (mapObject == null)
        {
            Debug.LogError("Map object not assigned! Please assign a GameObject (Terrain or with a Renderer) in the Inspector.");
            return;
        }

        ProcessFloorPlan();
    }

    /// <summary>
    /// Processes the input texture by analyzing brightness and local variance to decide which pixels belong to the traversable grid.
    /// Walkable areas will be painted green; non-walkable areas will be made transparent.
    /// </summary>
    void ProcessFloorPlan()
    {
        textureWidth = inputTexture.width;
        textureHeight = inputTexture.height;
        processedTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);

        // For each pixel, decide whether it's part of the traversable grid.
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                Color pixel = inputTexture.GetPixel(x, y);
                if (IsWalkable(pixel, x, y))
                {
                    // Mark traversable (grid) areas in green.
                    processedTexture.SetPixel(x, y, Color.green);
                }
                else
                {
                    // Otherwise, set transparent (non-walkable: environmental/scenery).
                    processedTexture.SetPixel(x, y, Color.clear);
                }
            }
        }
        processedTexture.Apply();

        ApplyProcessedTexture();
    }

    /// <summary>
    /// Determines if the pixel at (x,y) should be considered part of the traversable grid.
    /// Uses both brightness and local neighborhood variance as criteria.
    /// </summary>
    bool IsWalkable(Color pixel, int x, int y)
    {
        // Check brightness first.
        float brightness = pixel.grayscale;
        if (brightness < floorBrightnessThreshold)
        {
            return false;
        }

        // Calculate the local neighborhood variance.
        float sum = 0f;
        float sumSq = 0f;
        int count = 0;
        for (int dy = -localWindowSize; dy <= localWindowSize; dy++)
        {
            for (int dx = -localWindowSize; dx <= localWindowSize; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < textureWidth && ny >= 0 && ny < textureHeight)
                {
                    float g = inputTexture.GetPixel(nx, ny).grayscale;
                    sum += g;
                    sumSq += g * g;
                    count++;
                }
            }
        }
        float mean = sum / count;
        float variance = (sumSq / count) - (mean * mean);

        // If the area is uniform (low variance), assume it's a grid area (traversable).
        return (variance < varianceThreshold);
    }

    /// <summary>
    /// Applies the processed texture to the assigned map object.
    /// If the map object is a Terrain, it attempts to set the texture on its material template.
    /// Otherwise, it uses the Renderer component.
    /// </summary>
    void ApplyProcessedTexture()
    {
        // First, check if the mapObject has a Terrain component.
        Terrain terrainComponent = mapObject.GetComponent<Terrain>();
        if (terrainComponent != null)
        {
            if (terrainComponent.materialTemplate != null)
            {
                terrainComponent.materialTemplate.mainTexture = processedTexture;
                Debug.Log("Processed texture applied to Terrain's material template.");
            }
            else
            {
                Debug.LogWarning("Terrain does not have a material template assigned. Please assign one in the Terrain settings.");
            }
        }
        else
        {
            // Otherwise, check for a Renderer (e.g., if using a Plane or Quad).
            Renderer rend = mapObject.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.mainTexture = processedTexture;
                Debug.Log("Processed texture applied to Renderer.");
            }
            else
            {
                Debug.LogError("The assigned map object does not have a Terrain or Renderer component!");
            }
        }
    }
}
