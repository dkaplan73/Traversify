// File: ProjectHololand/Assets/Scripts/RPGMapTool/Managers/AnnotationSaver.cs
// GameObject Parent: AnnotationSaveObject under RPGMapTool/UI
using UnityEngine;
using UnityEngine.UI;
using System.IO;

namespace RPGMapTool.Managers
{
    public class AnnotationSaver : MonoBehaviour
    {
        public InputField annotationInput;

        // Save the annotation text to a file.
        public void SaveAnnotation()
        {
            string annotationText = annotationInput.text;
            string filePath = Application.persistentDataPath + "/annotation.txt";

            try
            {
                File.WriteAllText(filePath, annotationText);
                Debug.Log("AnnotationSaver: Annotation saved to " + filePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("AnnotationSaver: Failed to save annotation: " + ex);
            }
        }
    }
}