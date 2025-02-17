using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using SimpleFileBrowser;
using System.Collections;

namespace RPGMapTool.Managers
{
    public class UIManager : MonoBehaviour
    {
        [Header("Base Map Settings")]
        [Tooltip("RawImage for displaying the Base Map.")]
        [SerializeField] private RawImage baseMapImage;

        /// <summary>
        /// Called by the Open Browser button.
        /// Opens the file browser dialog via a coroutine so the user can select an image file.
        /// </summary>
        public void OnOpenBrowserButtonClicked()
        {
            Debug.Log("UIManager: Open browser clicked!");
            StartCoroutine(SelectAndLoadImageCoroutine());
        }

        /// <summary>
        /// Waits for the user to pick a file via the file browser, then loads it into the baseMapImage.
        /// </summary>
        private IEnumerator SelectAndLoadImageCoroutine()
        {
            // Optional: setup file filters & quick links
            FileBrowser.SetFilters(true, new FileBrowser.Filter("Image Files", ".png", ".jpg", ".jpeg"));
            FileBrowser.SetDefaultFilter(".png");
            FileBrowser.AddQuickLink("Assets", Application.dataPath);

            // Wait until the user picks a file
            yield return FileBrowser.WaitForLoadDialog(
                pickMode: FileBrowser.PickMode.Files,
                allowMultiSelection: false,
                initialPath: "",
                title: "Select Base Image" // Removed "submitButtonText" to match the plugin overload
            );

            // Check if user has chosen something
            if (!FileBrowser.Success || FileBrowser.Result == null || FileBrowser.Result.Length == 0)
            {
                Debug.LogWarning("UIManager: No file selected or FileBrowser canceled.");
                yield break;
            }

            // Single file path from the selection
            string filePath = FileBrowser.Result[0];
            Debug.Log("UIManager: File chosen -> " + filePath);

            // Now load and apply the texture
            yield return LoadAndApplyTexture(filePath);
        }

        /// <summary>
        /// Loads an image from the given file path, assigns it to the Base Map RawImage,
        /// and resizes the RawImage (while maintaining aspect ratio) to fit within its parent.
        /// </summary>
        private IEnumerator LoadAndApplyTexture(string filePath)
        {
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
                    Debug.LogError("UIManager: Error loading texture: " + www.error);
                }
                else
                {
                    Texture texture = DownloadHandlerTexture.GetContent(www);
                    if (texture == null)
                    {
                        Debug.LogError("UIManager: Loaded texture is null.");
                    }
                    else if (baseMapImage != null)
                    {
                        baseMapImage.texture = texture;
                        Debug.Log("UIManager: Base Map image updated successfully.");
                        ResizeRawImageToFitParent(texture);
                    }
                    else
                    {
                        Debug.LogError("UIManager: Base Map Image reference is missing.");
                    }
                }
            }
        }

        /// <summary>
        /// Resizes the baseMapImage RectTransform to fit within its parent's RectTransform 
        /// while maintaining the original texture's aspect ratio.
        /// </summary>
        private void ResizeRawImageToFitParent(Texture texture)
        {
            RectTransform imageRect = baseMapImage.GetComponent<RectTransform>();
            RectTransform parentRect = baseMapImage.transform.parent.GetComponent<RectTransform>();

            if (imageRect == null || parentRect == null)
            {
                Debug.LogWarning("UIManager: RectTransform missing for baseMapImage or its parent.");
                return;
            }

            // Get parent's size
            Vector2 parentSize = parentRect.rect.size;

            // Determine image aspect ratio
            float imageWidth = texture.width;
            float imageHeight = texture.height;
            float imageRatio = imageWidth / imageHeight;
            float parentRatio = parentSize.x / parentSize.y;

            Vector2 newSize;
            if (imageRatio > parentRatio)
            {
                // Image is relatively wider than parent: match parent's width.
                newSize.x = parentSize.x;
                newSize.y = parentSize.x / imageRatio;
            }
            else
            {
                // Image is relatively taller than parent: match parent's height.
                newSize.y = parentSize.y;
                newSize.x = parentSize.y * imageRatio;
            }

            imageRect.sizeDelta = newSize;
            Debug.Log("UIManager: Resized baseMapImage to " + newSize);
        }

        /// <summary>
        /// Placeholder to match references in CombinedPaintToolUI.
        /// Remove or implement as needed.
        /// </summary>
        public void OnSaveAnnotationButtonClicked()
        {
            Debug.Log("UIManager: OnSaveAnnotationButtonClicked placeholder method invoked.");
            // Implement save logic here if needed
        }
    }
}