// Folder: Traversify/Assets/Scripts/RPGMapTool/Managers
// GameObject Parent: PaintManagerObject under RPGMapTool
using UnityEngine;

namespace RPGMapTool.Managers
{
    public class LayerManager : MonoBehaviour
    {
        // Manages draw and annotation layers
        public void ToggleLayerVisibility(string layerName, bool visible)
        {
            Debug.Log($"LayerManager: Setting layer '{layerName}' visibility to {visible}");
            // TODO: Implement toggling of layer visibility.
        }
    }
}