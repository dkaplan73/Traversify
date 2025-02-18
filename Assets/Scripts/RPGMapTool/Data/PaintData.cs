using UnityEngine;

namespace RPGMapTool.Data
{
    /// <summary>
    /// Container class for paint settings.
    /// </summary>
    public class PaintData
    {
        /// <summary>
        /// Gets or sets the brush size.
        /// </summary>
        public float BrushSize { get; set; }

        /// <summary>
        /// Gets or sets the selected color.
        /// </summary>
        public Color SelectedColor { get; set; }

        /// <summary>
        /// Gets or sets the fill tolerance (used for flood fill operations).
        /// </summary>
        public float FillTolerance { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the eraser is in use.
        /// </summary>
        public bool UseEraser { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaintData"/> class with default values.
        /// </summary>
        public PaintData()
        {
            BrushSize = 5f;
            SelectedColor = Color.white;
            FillTolerance = 0.1f;
            UseEraser = false;
        }
    }
}