using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Managers;
using RPGMapTool.Core;

namespace RPGMapTool.Tools
{
    public class EyeDropperTool : MonoBehaviour
    {
        [Tooltip("Reference to the PaintManager for updating the selected color.")]
        [SerializeField] private PaintManager paintManager;
        [Tooltip("RawImage representing the Base Map.")]
        [SerializeField] private RawImage baseMapImage;

        /// <summary>
        /// Samples a color from the Base Map at the given screen position.
        /// </summary>
        public void UseEyeDropper(Vector2 screenPosition)
        {
            if (baseMapImage == null)
            {
                Debug.LogError("EyeDropperTool: Base Map Image not assigned.");
                return;
            }
            Texture2D texture = baseMapImage.texture as Texture2D;
            if (texture == null)
            {
                Debug.LogError("EyeDropperTool: Base Map texture is not a Texture2D.");
                return;
            }
            RectTransform rt = baseMapImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPosition, null, out Vector2 localPoint))
            {
                Debug.LogError("EyeDropperTool: Failed to convert screen point to local point.");
                return;
            }
            Vector2 size = rt.rect.size;
            int x = Mathf.Clamp((int)(((localPoint.x + size.x * 0.5f) / size.x) * texture.width), 0, texture.width - 1);
            int y = Mathf.Clamp((int)(((localPoint.y + size.y * 0.5f) / size.y) * texture.height), 0, texture.height - 1);
            Color sampledColor = texture.GetPixel(x, y);
            Debug.Log("EyeDropperTool: Sampled color: " + sampledColor);

            if (paintManager != null)
            {
                paintManager.GetPaintData().SelectedColor = sampledColor;
                Debug.Log("EyeDropperTool: Updated selected color in PaintManager.");
            }
            else
                Debug.LogWarning("EyeDropperTool: PaintManager not assigned.");
        }
    }
}