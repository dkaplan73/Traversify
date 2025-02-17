using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Core;
using RPGMapTool.Data;

namespace RPGMapTool.Managers
{
    public class PaintManager : MonoBehaviour
    {
        [SerializeField] private RawImage baseMapImage;
        [SerializeField] private PaintData paintData;

        public RawImage GetBaseMap() => baseMapImage;
        public PaintData GetPaintData() => paintData;

        // Converts a screen point to texture coordinates based on the baseMapImage.
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
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, null, out localPoint))
            {
                Debug.LogError("PaintManager: Unable to convert screen point.");
                return Vector2Int.zero;
            }
            Vector2 normalized = new Vector2(
                (localPoint.x + rect.width * 0.5f) / rect.width,
                (localPoint.y + rect.height * 0.5f) / rect.height);
            Texture2D tex = baseMapImage.texture as Texture2D;
            if (tex == null)
            {
                Debug.LogError("PaintManager: Base Map texture is not a Texture2D.");
                return Vector2Int.zero;
            }
            int x = Mathf.Clamp((int)(normalized.x * tex.width), 0, tex.width - 1);
            int y = Mathf.Clamp((int)(normalized.y * tex.height), 0, tex.height - 1);
            return new Vector2Int(x, y);
        }
    }
}