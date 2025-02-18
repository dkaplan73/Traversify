using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using SimpleFileBrowser;
using RPGMapTool.Core;

namespace RPGMapTool.Managers
{
    public class UIManager : MonoBehaviour
    {
        [Header("Base Map Settings")]
        [Tooltip("RawImage for displaying the Base Map.")]
        [SerializeField] private RawImage baseMapImage;

        [Header("UI Buttons")]
        [SerializeField] private Button openBrowserButton;
        [SerializeField] private Button saveAnnotationButton;

        private void Awake()
        {
            if (baseMapImage == null)
            {
                Debug.LogError("UIManager: Base Map Image not assigned.");
            }
            else
            {
                Debug.Log("UIManager: Base Map Image found.");
            }

            if (openBrowserButton != null)
                openBrowserButton.onClick.AddListener(OnOpenBrowserButtonClicked);
            if (saveAnnotationButton != null)
                saveAnnotationButton.onClick.AddListener(OnSaveAnnotationButtonClicked);
        }

        /// <summary>
        /// Called by the Open Browser button.
        /// Opens a file browser dialog and applies the chosen Base Map image.
        /// </summary>
        public void OnOpenBrowserButtonClicked()
        {
            Debug.Log("UIManager: Open browser clicked.");
            StartCoroutine(SelectAndLoadImageCoroutine());
        }

        private IEnumerator SelectAndLoadImageCoroutine()
        {
            FileBrowser.SetFilters(true, new FileBrowser.Filter("Image Files", ".png", ".jpg", ".jpeg"));
            FileBrowser.SetDefaultFilter(".png");
            FileBrowser.AddQuickLink("Assets", Application.dataPath);

            yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, "", "Select Base Image");

            if (!FileBrowser.Success || FileBrowser.Result == null || FileBrowser.Result.Length == 0)
            {
                Debug.LogWarning("UIManager: No file selected or FileBrowser canceled.");
                yield break;
            }

            string filePath = FileBrowser.Result[0];
            Debug.Log("UIManager: File chosen -> " + filePath);
            yield return LoadAndApplyTexture(filePath);
        }

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
                        // Call PaintOnMap.LoadBaseMap to align overlays with the updated base map.
                        PaintOnMap painter = FindObjectOfType<PaintOnMap>();
                        if (painter != null)
                        {
                            painter.LoadBaseMap(baseMapImage);
                            ResetPaintLayers();
                        }
                        else
                        {
                            Debug.LogWarning("UIManager: PaintOnMap instance not found.");
                        }
                    }
                    else
                    {
                        Debug.LogError("UIManager: Base Map Image reference is missing.");
                    }
                }
            }
        }

        private void ResizeRawImageToFitParent(Texture texture)
        {
            RectTransform imageRect = baseMapImage.GetComponent<RectTransform>();
            RectTransform parentRect = baseMapImage.transform.parent.GetComponent<RectTransform>();

            if (imageRect == null || parentRect == null)
            {
                Debug.LogWarning("UIManager: RectTransform missing for baseMapImage or its parent.");
                return;
            }

            Vector2 parentSize = parentRect.rect.size;
            float imageRatio = (float)texture.width / texture.height;
            float parentRatio = parentSize.x / parentSize.y;
            Vector2 newSize = Vector2.zero;

            if (imageRatio > parentRatio)
            {
                newSize.x = parentSize.x;
                newSize.y = parentSize.x / imageRatio;
            }
            else
            {
                newSize.y = parentSize.y;
                newSize.x = parentSize.y * imageRatio;
            }
            imageRect.sizeDelta = newSize;
            Debug.Log("UIManager: Resized baseMapImage to " + newSize);
        }

        /// <summary>
        /// Resets the paint layers to a blank (transparent) state.
        /// Finds the PaintOnMap instance and clears its overlays.
        /// </summary>
        private void ResetPaintLayers()
        {
            PaintOnMap painter = FindObjectOfType<PaintOnMap>();
            if (painter != null)
            {
                painter.ClearLayer(RPGMapTool.Enums.AnnotationLayer.Traversable);
                painter.ClearLayer(RPGMapTool.Enums.AnnotationLayer.NonTraversable);
                Debug.Log("UIManager: Paint layers reset to blank.");
            }
            else
            {
                Debug.LogWarning("UIManager: Could not find PaintOnMap instance to reset layers.");
            }
        }

        public void OnSaveAnnotationButtonClicked()
        {
            Debug.Log("UIManager: OnSaveAnnotationButtonClicked invoked.");
            // Save annotation logic goes here.
        }
    }
}