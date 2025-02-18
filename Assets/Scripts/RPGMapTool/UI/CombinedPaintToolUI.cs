using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Managers;
using RPGMapTool.Core;

namespace RPGMapTool.UI
{
    public class CombinedPaintToolUI : MonoBehaviour
    {
        [Header("Tool Actions")]
        [SerializeField] private Button openBrowserButton;
        [SerializeField] private Button saveAnnotationButton;
        [SerializeField] private Button undoButton;

        private UIManager uiManager;

        private void Awake()
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
                Debug.LogError("CombinedPaintToolUI: UIManager not found.");
            else
                Debug.Log("CombinedPaintToolUI: UIManager found.");

            if (openBrowserButton != null)
                openBrowserButton.onClick.AddListener(() =>
                {
                    Debug.Log("CombinedPaintToolUI: Open Browser clicked.");
                    uiManager.OnOpenBrowserButtonClicked();
                });
            else
                Debug.LogWarning("CombinedPaintToolUI: Open Browser Button not assigned.");

            if (saveAnnotationButton != null)
                saveAnnotationButton.onClick.AddListener(() =>
                {
                    Debug.Log("CombinedPaintToolUI: Save Annotation clicked.");
                    uiManager.OnSaveAnnotationButtonClicked();
                });
            else
                Debug.LogWarning("CombinedPaintToolUI: Save Annotation Button not assigned.");

            if (undoButton != null)
                undoButton.onClick.AddListener(() =>
                {
                    Debug.Log("CombinedPaintToolUI: Undo clicked.");
                    var painter = FindObjectOfType<PaintOnMap>();
                    if (painter != null)
                    {
                        var undoManager = FindObjectOfType<UndoManager>();
                        if (undoManager != null)
                            undoManager.Undo(painter);
                        else
                            Debug.LogWarning("CombinedPaintToolUI: UndoManager not found.");
                    }
                    else
                        Debug.LogWarning("CombinedPaintToolUI: PaintOnMap not found.");
                });
            else
                Debug.LogWarning("CombinedPaintToolUI: Undo Button not assigned.");
        }
    }
}