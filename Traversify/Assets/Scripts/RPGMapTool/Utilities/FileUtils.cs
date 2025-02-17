// Folder: Traversify/Assets/Scripts/RPGMapTool/Utilities
using System.IO;
using UnityEngine;

namespace RPGMapTool.Utilities
{
    public static class FileUtils
    {
        public static void OpenFileBrowser(System.Action<string> onFileSelected)
        {
            // Implementation using SimpleFileBrowser or native OS file dialog.
            Debug.Log("FileUtils: OpenFileBrowser called");
            // This is a placeholder. Integrate with your file browser solution.
            onFileSelected?.Invoke("dummy/path/to/file.png");
        }
    }
}