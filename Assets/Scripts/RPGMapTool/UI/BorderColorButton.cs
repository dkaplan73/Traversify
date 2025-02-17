// Folder: Traversify/Assets/Scripts/RPGMapTool/UI
// GameObject Parent: Buttons under RPGMapTool/UI
using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Managers;

namespace RPGMapTool.UI
{
    public class BorderColorButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Color newColor = Color.black;
        
        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(OnButtonClicked);
        }
        
        private void OnButtonClicked()
        {
            var borderManager = FindObjectOfType<BorderManager>();
            if (borderManager != null)
                borderManager.SetBorderColor(newColor);
            else
                Debug.LogError("BorderColorButton: BorderManager not found.");
        }
    }
}