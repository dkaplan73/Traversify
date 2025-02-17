// Folder: Traversify/Assets/Scripts/RPGMapTool/Managers
// GameObject Parent: PaintManagerObject under RPGMapTool
using UnityEngine;
using RPGMapTool.Enums;

namespace RPGMapTool.Managers
{
    public class ToolToggle : MonoBehaviour
    {
        public AnnotationTool currentTool = AnnotationTool.Brush;

        public void SetTool(AnnotationTool tool)
        {
            currentTool = tool;
            Debug.Log("ToolToggle: Tool switched to " + tool.ToString());
        }
    }
}