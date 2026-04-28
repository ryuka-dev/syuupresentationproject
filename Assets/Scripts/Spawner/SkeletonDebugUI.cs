using UnityEngine;
using UnityEngine.InputSystem;

public class SkeletonDebugUI : MonoBehaviour
{
    public SkeletonSpawner spawner;
    private bool showUI = false;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame)
            showUI = !showUI;
    }

    void OnGUI()
    {
        if (!showUI) return;

        GUILayout.BeginArea(new Rect(20, 20, 300, 220));
        
        GUILayout.Label("Skeleton Spawner", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
        GUILayout.Space(10);
        
        GUILayout.Label($"Count: {spawner.GetCount()} / {spawner.maxCount}");
        
        if (GUILayout.Button("Spawn 1", GUILayout.Height(40)))
            spawner.SpawnSkeleton();
        
        if (GUILayout.Button("Spawn 5", GUILayout.Height(40)))
            for (int i = 0; i < 5; i++) spawner.SpawnSkeleton();
        
        GUILayout.Space(10);
        
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear All", GUILayout.Height(40)))
            spawner.ClearAll();
        GUI.backgroundColor = Color.white;
        
        GUILayout.Label("F1: Toggle");
        
        GUILayout.EndArea();
    }
}
