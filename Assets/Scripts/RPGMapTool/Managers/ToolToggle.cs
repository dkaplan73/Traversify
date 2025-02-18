using UnityEngine;
using UnityEngine.UI;
using RPGMapTool.Enums;

namespace RPGMapTool.UI
{
    [System.Serializable]
    public struct ToolSpriteMapping
    {
        public AnnotationTool tool;
        public Sprite toolSprite;
    }

    public class ToolToggle : MonoBehaviour
    {
        [Header("Active Tool Settings")]
        [SerializeField] private AnnotationTool currentTool;
        [SerializeField] private Image cursorSpriteImage;

        [Header("Tool Sprite Mappings")]
        [SerializeField] private ToolSpriteMapping[] toolSpriteMappings;

        public AnnotationTool CurrentTool => currentTool;

        /// <summary>
        /// Sets the active tool and updates the associated cursor sprite.
        /// </summary>
        public void SetTool(AnnotationTool newTool)
        {
            currentTool = newTool;
            UpdateCursorSprite();
            Debug.Log("ToolToggle: Active tool set to " + currentTool);
        }

        /// <summary>
        /// Updates the cursor sprite based on the active tool using the mapping data.
        /// </summary>
        private void UpdateCursorSprite()
        {
            if (cursorSpriteImage == null)
            {
                Debug.LogError("ToolToggle: Cursor Sprite Image reference is missing.");
                return;
            }

            foreach (var mapping in toolSpriteMappings)
            {
                if (mapping.tool == currentTool)
                {
                    if (mapping.toolSprite != null)
                    {
                        cursorSpriteImage.sprite = mapping.toolSprite;
                        Debug.Log("ToolToggle: Updated cursor sprite for tool: " + currentTool);
                    }
                    else
                    {
                        Debug.LogWarning("ToolToggle: No sprite assigned for tool: " + currentTool);
                    }
                    return;
                }
            }
            Debug.LogWarning("ToolToggle: No mapping found for tool: " + currentTool);
        }
    }
}