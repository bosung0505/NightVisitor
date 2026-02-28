using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;

public class NavMeshHelper : EditorWindow
{
    [MenuItem("Tools/Force Plants Walkable")]
    public static void ForceWalkable()
    {
        GameObject plantsObj = GameObject.Find("Plants");
        if (plantsObj == null)
        {
            Debug.LogError("Could not find object named 'Plants'");
            return;
        }

        // Add modifier to root if not exists
        NavMeshModifier rootMod = plantsObj.GetComponent<NavMeshModifier>();
        if (rootMod == null) rootMod = plantsObj.AddComponent<NavMeshModifier>();
        
        rootMod.overrideArea = true;
        rootMod.area = 0; // Walkable
        
        // Find all MeshRenderers in children
        MeshRenderer[] renderers = plantsObj.GetComponentsInChildren<MeshRenderer>(true);
        int count = 0;
        foreach (MeshRenderer r in renderers)
        {
            NavMeshModifier mod = r.gameObject.GetComponent<NavMeshModifier>();
            if (mod == null)
            {
                mod = r.gameObject.AddComponent<NavMeshModifier>();
            }
            mod.overrideArea = true;
            mod.area = 0; // Walkable
            mod.ignoreFromBuild = false; // Just to be safe, don't ignore, force walkable
            count++;
        }
        
        Debug.Log($"Applied Walkable NavMeshModifier to {count} child meshes of Plants.");
        
        // Also force Layer 0 (Default) just in case the layer 6 was the issue
        Transform[] children = plantsObj.GetComponentsInChildren<Transform>(true);
        foreach(Transform t in children)
        {
            t.gameObject.layer = 0; // Default layer
        }
    }
}