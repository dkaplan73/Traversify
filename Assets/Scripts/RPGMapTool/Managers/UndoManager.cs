// File: ProjectHololand/Assets/Scripts/RPGMapTool/Managers/UndoManager.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using RPGMapTool.Core;

namespace RPGMapTool.Managers
{
    public class UndoManager : MonoBehaviour
    {
        [Header("Undo Settings")]
        [SerializeField, Tooltip("Maximum number of history layers to save.")]
        private int maxHistoryCount = 10;

        [SerializeField, Tooltip("Reference to the Undo button.")]
        private Button undoButton;

        // History stack to store painting states (could be textures, commands, etc.)
        private Stack<Texture2D> historyStack = new Stack<Texture2D>();

        private void Awake()
        {
            if (undoButton != null)
            {
                undoButton.onClick.AddListener(() =>
                {
                    // Find the active PaintOnMap to apply undo.
                    PaintOnMap painter = FindObjectOfType<PaintOnMap>();
                    if (painter != null)
                    {
                        Undo(painter);
                    }
                    else
                    {
                        Debug.LogError("UndoManager: PaintOnMap component not found for undo.");
                    }
                });
            }
            else
            {
                Debug.LogWarning("UndoManager: UndoButton is not assigned.");
            }
        }

        /// <summary>
        /// Saves the current state of the painting overlay.
        /// Call this method after each painting action.
        /// </summary>
        public void SaveState(Texture2D currentState)
        {
            // If history exceeds the limit, remove the oldest state.
            while (historyStack.Count >= maxHistoryCount)
            {
                historyStack.Pop();
            }
            historyStack.Push(currentState);
            Debug.Log($"UndoManager: State saved. History count: {historyStack.Count}");
        }

        /// <summary>
        /// Applies the last saved state to the given PaintOnMap.
        /// </summary>
        public void Undo(PaintOnMap painter)
        {
            if (historyStack.Count > 0)
            {
                Texture2D lastState = historyStack.Pop();
                // Here you can assign lastState to the painter's overlay texture.
                // For example:
                // painter.SetOverlayTexture(lastState);
                Debug.Log("UndoManager: Undo action performed.");
            }
            else
            {
                Debug.Log("UndoManager: No history to undo.");
            }
        }
    }
}