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

        GUILayout.BeginArea(new Rect(20, 20, 300, 370));

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

        GUILayout.Space(10);

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("\u6062\u590d\u73a9\u5bb6\u6ee1\u8840", GUILayout.Height(40)))
            RestorePlayerFullHealth();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("\u590d\u6d3b\u73a9\u5bb6\u6d4b\u8bd5", GUILayout.Height(40)))
            RespawnPlayerTest();
        GUI.backgroundColor = Color.white;

        GUILayout.Label("F1: Toggle");

        GUILayout.EndArea();
    }

    private void RestorePlayerFullHealth()
    {
        var factions = FindObjectsByType<FactionComponent>(FindObjectsSortMode.None);
        foreach (var fc in factions)
        {
            if (fc.faction == Faction.Player)
            {
                var health = fc.GetComponent<HealthComponent>();
                if (health != null)
                {
                    Debug.Log("[DebugUI] Restore player full health button clicked.");
                    health.RestoreFullHealth();
                    return;
                }
            }
        }
        Debug.LogWarning("[DebugUI] Player HealthComponent not found.");
    }

    private void RespawnPlayerTest()
    {
        var factions = FindObjectsByType<FactionComponent>(FindObjectsSortMode.None);
        foreach (var fc in factions)
        {
            if (fc.faction == Faction.Player)
            {
                var health       = fc.GetComponent<HealthComponent>();
                var deathHandler = fc.GetComponent<PlayerDeathHandler>();

                if (health == null)
                {
                    Debug.LogWarning("[DebugUI] Player HealthComponent not found.");
                    return;
                }
                if (deathHandler == null)
                {
                    Debug.LogWarning("[DebugUI] PlayerDeathHandler not found.");
                    return;
                }

                health.RestoreFullHealth();
                deathHandler.ResetForRespawn();

                var levelManager = FindFirstObjectByType<LevelObjectiveManager>();
                if (levelManager != null)
                    levelManager.ClearLevelResultForRespawn();
                else
                    Debug.LogWarning("[DebugUI] LevelObjectiveManager not found. UI not cleared.");

                Debug.Log("[DebugUI] Player respawn test executed.");
                return;
            }
        }
        Debug.LogWarning("[DebugUI] Player (FactionComponent) not found.");
    }
}
