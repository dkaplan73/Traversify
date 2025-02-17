// Folder: Traversify/Assets/Scripts/RPGMapTool/UI
// GameObject Parent: UI Elements under RPGMapTool/UI
using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Managers;

namespace RPGMapTool.UI
{
    public class ColorPicker : MonoBehaviour
    {
        [SerializeField] private Image colorDisplay;
        [SerializeField] private Slider rSlider, gSlider, bSlider;

        private void Start()
        {
            if (rSlider) rSlider.onValueChanged.AddListener(UpdateColor);
            if (gSlider) gSlider.onValueChanged.AddListener(UpdateColor);
            if (bSlider) bSlider.onValueChanged.AddListener(UpdateColor);
        }

        private void UpdateColor(float value)
        {
            Color newColor = new Color(rSlider.value, gSlider.value, bSlider.value);
            if (colorDisplay != null)
                colorDisplay.color = newColor;
            // You might assign newColor to the current tool's paint data.
            var pm = FindObjectOfType<PaintManager>();
            if (pm != null)
            {
                pm.GetPaintData().SelectedColor = newColor;
            }
        }
    }
}