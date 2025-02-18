using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Core;
using RPGMapTool.Data;

namespace RPGMapTool.Managers
{
    public class PaintManager : MonoBehaviour
    {
        [Header("Base Map Settings")]
        [SerializeField] private RawImage baseMapImage;
        [SerializeField] private PaintData paintData;
        
        [Header("Painting Settings")]
        [SerializeField] private Color currentColor = Color.white;
        [SerializeField] private float brushSize = 5f;
        [SerializeField] private Color currentBorderColor = Color.black;

        public Color CurrentColor => currentColor;
        public float BrushSize => brushSize;
        public Color CurrentBorderColor => currentBorderColor;

        private void Awake()
        {
            if (baseMapImage == null)
                Debug.LogError("PaintManager: Base Map Image not assigned.");
            else
                Debug.Log("PaintManager: Base Map Image found.");

            if(paintData == null)
            {
                Debug.LogWarning("PaintManager: PaintData not assigned; creating new instance.");
                paintData = new PaintData();
            }
            Debug.Log($"PaintManager: Initialized with currentColor={currentColor}, brushSize={brushSize}, borderColor={currentBorderColor}");
        }

        public RawImage GetBaseMap()
        {
            if (baseMapImage == null)
                Debug.LogError("PaintManager: GetBaseMap() - BaseMapImage is null.");
            return baseMapImage;
        }

        public PaintData GetPaintData()
        {
            if(paintData == null)
            {
                Debug.LogWarning("PaintManager: PaintData is null, creating new instance.");
                paintData = new PaintData();
            }
            return paintData;
        }

        /// <summary>
        /// Converts a screen point to texture coordinates according to the Base Map.
        /// </summary>
        public Vector2Int ScreenPointToTextureCoords(Vector2 screenPoint)
        {
            if (baseMapImage == null)
            {
                Debug.LogError("PaintManager: Base Map Image not assigned.");
                return Vector2Int.zero;
            }

            RectTransform rt = baseMapImage.rectTransform;
            Rect rect = rt.rect;
            Vector2 localPoint;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, null, out localPoint))
            {
                Debug.LogError($"PaintManager: Failed to convert screen point {screenPoint}.");
                return Vector2Int.zero;
            }

            Vector2 normalized = new Vector2(
                (localPoint.x + rect.width * 0.5f) / rect.width,
                (localPoint.y + rect.height * 0.5f) / rect.height
            );

            Texture baseTex = baseMapImage.texture;
            Texture2D tex = baseTex as Texture2D;
            if (tex == null)
            {
                Debug.LogWarning("PaintManager: Base Map texture is not a Texture2D. Attempting conversion...");
                RenderTexture rtTex = baseTex as RenderTexture;
                if (rtTex != null)
                {
                    Texture2D converted = new Texture2D(rtTex.width, rtTex.height, TextureFormat.RGBA32, false);
                    RenderTexture currentRT = RenderTexture.active;
                    RenderTexture.active = rtTex;
                    converted.ReadPixels(new Rect(0, 0, rtTex.width, rtTex.height), 0, 0);
                    converted.Apply();
                    RenderTexture.active = currentRT;
                    tex = converted;
                    Debug.Log("PaintManager: Converted RenderTexture to Texture2D.");
                }
                else
                {
                    Debug.LogError("PaintManager: Base Map texture is neither Texture2D nor RenderTexture.");
                    return Vector2Int.zero;
                }
            }

            int x = Mathf.Clamp((int)(normalized.x * tex.width), 0, tex.width - 1);
            int y = Mathf.Clamp((int)(normalized.y * tex.height), 0, tex.height - 1);
            Vector2Int textureCoords = new Vector2Int(x, y);
            Debug.Log($"PaintManager: Converted screen point {screenPoint} to texture coordinates {textureCoords}.");
            return textureCoords;
        }
    }
}