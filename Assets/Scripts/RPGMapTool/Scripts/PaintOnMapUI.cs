// Assets/Scripts/RPGMapTool/Scripts/PaintOnMapUI.cs
using UnityEngine;
using UnityEngine.UI;

namespace RPGMapTool
{
    public class PaintOnMapUI : MonoBehaviour
    {
        public PaintOnMap paintOnMap; // Assign this in the Inspector

        [Header("=== UI Elements ===")]
        public Button UndoButton;
        public Button ClearButton;
        public Button IncreaseBrushSizeButton;
        public Button DecreaseBrushSizeButton;
        public Button ThickenBlobButton;

        private void Start()
        {
            if (paintOnMap == null)
            {
                Debug.LogError("[PaintOnMapUI] PaintOnMap reference is not assigned.");
                return;
            }

            if (UndoButton != null)
                UndoButton.onClick.AddListener(paintOnMap.UndoCurrentMethod);
            else
                Debug.LogWarning("[PaintOnMapUI] UndoButton is not assigned.");

            if (ClearButton != null)
                ClearButton.onClick.AddListener(paintOnMap.ClearAnnotationsMethod);
            else
                Debug.LogWarning("[PaintOnMapUI] ClearButton is not assigned.");

            if (IncreaseBrushSizeButton != null)
                IncreaseBrushSizeButton.onClick.AddListener(paintOnMap.IncreaseBrushSizeMethod);
            else
                Debug.LogWarning("[PaintOnMapUI] IncreaseBrushSizeButton is not assigned.");

            if (DecreaseBrushSizeButton != null)
                DecreaseBrushSizeButton.onClick.AddListener(paintOnMap.DecreaseBrushSizeMethod);
            else
                Debug.LogWarning("[PaintOnMapUI] DecreaseBrushSizeButton is not assigned.");

            if (ThickenBlobButton != null)
                ThickenBlobButton.onClick.AddListener(paintOnMap.ThickenBlobMethod);
            else
                Debug.LogWarning("[PaintOnMapUI] ThickenBlobButton is not assigned.");
        }
    }
}
