using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using SimpleFileBrowser;
using RPGMapTool.Core;
using RPGMapTool.Enums;

namespace RPGMapTool.Controllers
{
    public class MapToolUIController : MonoBehaviour
    {
        [Header("Base Map Settings")]
        [Tooltip("RawImage for displaying the Base Map.")]
        [SerializeField] private RawImage baseMapImage;
        [SerializeField] private float zoomSpeed = 0.1f;
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 2f;

        [Header("UI References")]
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;

        private RectTransform baseMapRect;
        private Vector3 initialScale;
        private bool isPanning;

        private void Awake()
        {
            if (baseMapImage == null)
            {
                Debug.LogError("MapToolUIController: Base Map Image not assigned.");
                return;
            }

            baseMapRect = baseMapImage.GetComponent<RectTransform>();
            initialScale = baseMapRect.localScale;
            
            SetupZoomControls();
            Debug.Log("MapToolUIController: Initialized with base map and zoom controls.");
        }

        private void SetupZoomControls()
        {
            if (zoomInButton != null)
                zoomInButton.onClick.AddListener(() => ZoomMap(zoomSpeed));
            else
                Debug.LogWarning("MapToolUIController: Zoom In button not assigned.");

            if (zoomOutButton != null)
                zoomOutButton.onClick.AddListener(() => ZoomMap(-zoomSpeed));
            else
                Debug.LogWarning("MapToolUIController: Zoom Out button not assigned.");
        }

        /// <summary>
        /// Opens a file browser dialog so the user can choose an image file.
        /// </summary>
        public void OnSelectImageButtonClicked()
        {
            try
            {
                FileBrowser.ShowLoadDialog(
                    (string[] paths) =>
                    {
                        if (paths != null && paths.Length > 0)
                        {
                            Debug.Log("MapToolUIController: Selected file: " + paths[0]);
                            StartCoroutine(LoadAndSetBaseMap(paths[0]));
                        }
                        else
                        {
                            Debug.LogWarning("MapToolUIController: No file selected.");
                        }
                    },
                    () =>
                    {
                        Debug.LogWarning("MapToolUIController: Image selection cancelled.");
                    },
                    FileBrowser.PickMode.Files,
                    false,
                    "",
                    "Select Image",
                    "Select"
                );
            }
            catch (Exception ex)
            {
                Debug.LogError("MapToolUIController: Error opening file browser: " + ex.Message);
            }
        }

        private IEnumerator LoadAndSetBaseMap(string filePath)
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
                        ResizeRawImageToFitParent(texture);
                        ResetPaintLayers();
                    }
                }
            }
        }

        private void ResizeRawImageToFitParent(Texture texture)
        {
            if (baseMapRect == null)
            {
                Debug.LogError("MapToolUIController: baseMapRect is null.");
                return;
            }

            RectTransform parentRect = baseMapImage.transform.parent.GetComponent<RectTransform>();
            if (parentRect == null)
            {
                Debug.LogError("MapToolUIController: Parent RectTransform not found.");
                return;
            }

            Vector2 parentSize = parentRect.rect.size;
            float imageRatio = (float)texture.width / texture.height;
            float parentRatio = parentSize.x / parentSize.y;
            Vector2 newSize;

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

            baseMapRect.sizeDelta = newSize;
            Debug.Log($"MapToolUIController: Resized baseMapImage to {newSize}");
        }

        public void ZoomMap(float zoomDelta)
        {
            if (baseMapRect == null) return;

            float currentScale = baseMapRect.localScale.x;
            float newScale = Mathf.Clamp(currentScale + zoomDelta, minZoom, maxZoom);
            baseMapRect.localScale = Vector3.one * newScale;
            Debug.Log($"MapToolUIController: Map zoomed to scale {newScale}");
        }

        public void BeginPan()
        {
            isPanning = true;
            Debug.Log("MapToolUIController: Started panning");
        }

        public void HandlePan(Vector2 delta)
        {
            if (!isPanning || baseMapRect == null) return;
            baseMapRect.anchoredPosition += delta;
            Debug.Log($"MapToolUIController: Panning, new position: {baseMapRect.anchoredPosition}");
        }

        public void EndPan()
        {
            isPanning = false;
            Debug.Log("MapToolUIController: Ended panning");
        }

        private void ResetPaintLayers()
        {
            var painter = FindObjectOfType<PaintOnMap>();
            if (painter != null)
            {
                painter.ClearLayer(AnnotationLayer.Traversable);
                painter.ClearLayer(AnnotationLayer.NonTraversable);
                Debug.Log("MapToolUIController: Paint layers reset to blank.");
            }
            else
            {
                Debug.LogWarning("MapToolUIController: Could not find PaintOnMap instance to reset layers.");
            }
        }
    }
}