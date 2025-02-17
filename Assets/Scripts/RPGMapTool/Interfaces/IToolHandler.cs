// Folder: Traversify/Assets/Scripts/RPGMapTool/Interfaces
namespace RPGMapTool.Interfaces
{
    public interface IToolHandler
    {
        void OnToolSelected();
        void OnToolUsed(UnityEngine.Vector2 screenPosition);
    }
}