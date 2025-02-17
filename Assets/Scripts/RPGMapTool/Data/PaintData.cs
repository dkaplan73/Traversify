// File: Assets/Scripts/RPGMapTool/Data/PaintData.cs
// Not attached; serves as a data container for paint settings.
using UnityEngine;

namespace RPGMapTool.Data
{
    public class PaintData
    {
        public float BrushSize { get; set; }
        public Color SelectedColor { get; set; }
        public float FillTolerance { get; set; }
        public bool UseEraser { get; set; }
        
        public PaintData()
        {
            BrushSize = 5f;
            SelectedColor = Color.white;
            FillTolerance = 0.1f;
            UseEraser = false;
        }
    }
}