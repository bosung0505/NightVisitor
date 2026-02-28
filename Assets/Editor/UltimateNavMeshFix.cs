using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;

public class UltimateNavMeshFix : EditorWindow
{
    [MenuItem("Tools/Undo NavMesh Fix")]
    public static void Undo()
    {
        NavMeshSurface surface = GameObject.FindObjectOfType<NavMeshSurface>();
        if (surface != null)
        {
            surface.layerMask = -1; // -1 is 'Everything'
            Debug.Log("Restored NavMeshSurface LayerMask to Everything.");
        }

        int restoredCount = 0;

        MeshRenderer[] renderers = GameObject.FindObjectsOfType<MeshRenderer>(true);
        foreach (MeshRenderer r in renderers)
        {
            string rootName = r.transform.root.name.ToLower();
            string objName = r.gameObject.name.ToLower();

            bool isPlant = rootName.Contains("plant") || objName.Contains("plant");
            bool isFence = rootName.Contains("fence1") || objName.Contains("fence1");

            if (isPlant || isFence)
            {
                if (isPlant) r.gameObject.layer = 0; // Default layer
                if (isFence) r.gameObject.layer = 6; // Original Fence layer

                NavMeshModifier mod = r.GetComponent<NavMeshModifier>();
                if (mod != null)
                {
                    DestroyImmediate(mod);
                }
                restoredCount++;
            }
        }

        Collider[] colliders = GameObject.FindObjectsOfType<Collider>(true);
        foreach(Collider c in colliders)
        {
            string rootName = c.transform.root.name.ToLower();
            if (rootName.Contains("plant")) c.gameObject.layer = 0;
            if (rootName.Contains("fence1")) c.gameObject.layer = 6;
        }

        Debug.Log($"Restored layers and removed modifiers for {restoredCount} objects.");
    }
}