// Assets/Scripts/RPGMapTool/Scripts/MagicWandTool.cs
using System.Collections.Generic; // Essential for Queue<T>
using UnityEngine;

namespace RPGMapTool
{
    public class MagicWandTool
    {
        private UndoManager undoManager;

        public MagicWandTool(UndoManager manager)
        {
            undoManager = manager;
        }

        /// <summary>
        /// Executes the flood-fill algorithm to fill connected areas with the specified color.
        /// </summary>
        /// <param name="targetTex">The texture to fill.</param>
        /// <param name="startPos">Starting position for flood fill.</param>
        /// <param name="fillColor">Color to fill with.</param>
        /// <param name="baseTex">The base map texture for reference.</param>
        public void Execute(Texture2D targetTex, Vector2Int startPos, Color fillColor, Texture2D baseTex)
        {
            if (targetTex == null || baseTex == null)
            {
                Debug.LogError("[MagicWandTool] Execute => targetTex or baseTex is null.");
                return;
            }

            Color targetColor = targetTex.GetPixel(startPos.x, startPos.y);
            if (targetColor == fillColor)
            {
                Debug.Log("[MagicWandTool] Execute => Fill color is the same as target color. Aborting.");
                return;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(startPos);

            while (queue.Count > 0)
            {
                Vector2Int pos = queue.Dequeue();

                if (pos.x < 0 || pos.x >= targetTex.width || pos.y < 0 || pos.y >= targetTex.height)
                    continue;

                Color currentColor = targetTex.GetPixel(pos.x, pos.y);
                if (currentColor != targetColor)
                    continue;

                targetTex.SetPixel(pos.x, pos.y, fillColor);

                // Enqueue neighboring pixels
                queue.Enqueue(new Vector2Int(pos.x + 1, pos.y));
                queue.Enqueue(new Vector2Int(pos.x - 1, pos.y));
                queue.Enqueue(new Vector2Int(pos.x, pos.y + 1));
                queue.Enqueue(new Vector2Int(pos.x, pos.y - 1));
            }

            targetTex.Apply();
            Debug.Log("[MagicWandTool] Execute => Flood fill completed.");
        }
    }
}
