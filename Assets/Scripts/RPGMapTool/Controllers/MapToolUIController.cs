// File: Assets/Scripts/RPGMapTool/Controllers/MapToolUIController.cs
// GameObject Parent: UIManagerObject (under RPGMapTool/UI)
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using SimpleFileBrowser;

namespace RPGMapTool.Controllers
{
    public class MapToolUIController : MonoBehaviour
    {
        [Header("Base Map Settings")]
        [Tooltip("RawImage for displaying the Base Map.")]
        [SerializeField] private RawImage baseMapImage;

        /// <summary>
        /// Called by the Select Image button.
        /// Opens a file browser dialog so the user can choose an image file.
        /// </summary>
        public void OnSelectImageButtonClicked()
        {
            StartCoroutine(OpenFileBrowserCoroutine());
        }

        private IEnumerator OpenFileBrowserCoroutine()
        {
            // Opens the file browser dialog
            yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false,
                                                         "", null, "Select Image", "Select");
            if (FileBrowser.Success)
            {
                string[] paths = FileBrowser.Result;
                if (paths != null && paths.Length > 0)
                {
                    Debug.Log("MapToolUIController: Selected file: " + paths[0]);
                    yield return LoadAndSetBaseMap(paths[0]);
                }
                else
                {
                    Debug.LogWarning("MapToolUIController: No file selected.");
                }
            }
            else
            {
                Debug.LogWarning("MapToolUIController: Image selection cancelled.");
            }
        }

        /// <summary>
        /// Coroutine that loads an image from the given file path and assigns it to the Base Map Image.
        /// </summary>
        private IEnumerator LoadAndSetBaseMap(string filePath)
        {
            // Prepend with "file:///" for local file access.
            string uri = "file:///" + filePath;
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(uri))
            {
                yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
#else
                if (www.isNetworkError || www.isHttpError)
#endif
                {
                    Debug.LogError("MapToolUIController: Error loading image: " + www.error);
                }
                else
                {
                    Texture texture = DownloadHandlerTexture.GetContent(www);
                    if (texture == null)
                    {
                        Debug.LogError("MapToolUIController: Loaded texture is null.");
                    }
                    else
                    {
                        baseMapImage.texture = texture;
                        Debug.Log("MapToolUIController: Base Map image updated successfully.");
                    }
                }
            }
        }
    }
}