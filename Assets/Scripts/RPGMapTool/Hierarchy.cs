using UnityEngine;
using System.Text;

public class HierarchyPrinter : MonoBehaviour
{
    void Start()
    {
        // Build the hierarchy string and print it in one statement.
        StringBuilder hierarchyString = new StringBuilder();
        BuildHierarchy(gameObject, hierarchyString);
        Debug.Log(hierarchyString.ToString());
    }

    void BuildHierarchy(GameObject obj, StringBuilder sb, int indent = 0)
    {
        // Append the current GameObject's name with indentation.
        sb.AppendLine(new string('-', indent * 2) + obj.name);

        // Recursively process each child.
        foreach (Transform child in obj.transform)
        {
            BuildHierarchy(child.gameObject, sb, indent + 1);
        }
    }
}
