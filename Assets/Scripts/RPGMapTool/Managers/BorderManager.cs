// Folder: Traversify/Assets/Scripts/RPGMapTool/Managers
// GameObject Parent: PaintManagerObject under RPGMapTool
using UnityEngine;

namespace RPGMapTool.Managers
{
    public class BorderManager : MonoBehaviour
    {
        [SerializeField] private Color borderColor = Color.black;

        public Color BorderColor => borderColor;

        public void SetBorderColor(Color newColor)
        {
            borderColor = newColor;
            Debug.Log("BorderManager: Border color set to " + newColor);
        }
    }
}