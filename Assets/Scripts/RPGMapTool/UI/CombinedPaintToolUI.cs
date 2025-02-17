// Folder: Traversify/Assets/Scripts/RPGMapTool/UI
// GameObject Parent: UIManagerObject under RPGMapTool/UI
using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Managers;
using RPGMapTool.Enums;

namespace RPGMapTool.UI
{
    public class CombinedPaintToolUI : MonoBehaviour
    {
        [SerializeField] private Button openBrowserButton;
        [SerializeField] private Button saveAnnotationButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Dropdown toolDropdown;

        private UIManager uiManager;
        private ToolToggle toolToggle;

        private void Awake()
        {
            uiManager = FindObjectOfType<UIManager>();
            toolToggle = FindObjectOfType<ToolToggle>();

            if (openBrowserButton != null)
                openBrowserButton.onClick.AddListener(() => uiManager.OnOpenBrowserButtonClicked());
            if (saveAnnotationButton != null)
                saveAnnotationButton.onClick.AddListener(() => uiManager.OnSaveAnnotationButtonClicked());
            if (undoButton != null)
            {
                undoButton.onClick.AddListener(() =>
                {
                    var undoMgr = FindObjectOfType<UndoManager>();
                    var painter = FindObjectOfType<Core.PaintOnMap>();
                    if (undoMgr != null && painter != null)
                        undoMgr.Undo(painter);
                });
            }
            if (toolDropdown != null)
                toolDropdown.onValueChanged.AddListener(OnToolChanged);
        }

        private void OnToolChanged(int index)
        {
            // Map dropdown index to AnnotationTool enum.
            AnnotationTool selectedTool = (AnnotationTool)index;
            if (toolToggle != null)
                toolToggle.SetTool(selectedTool);
            Debug.Log("CombinedPaintToolUI: Tool changed to " + selectedTool);
        }
    }
}