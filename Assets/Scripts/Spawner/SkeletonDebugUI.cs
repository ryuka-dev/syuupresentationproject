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

        GUILayout.BeginArea(new Rect(20, 20, 300, 430));

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

        GUI.backgroundColor = new Color(1f, 0.75f, 0.2f);
        if (GUILayout.Button("\u590d\u6d3b\u5230\u6700\u8fd1\u5b58\u6863\u70b9", GUILayout.Height(40)))
            RespawnPlayerAtSavePointTest();
        GUI.backgroundColor = Color.white;

        GUILayout.Label("F1: Toggle");

        GUILayout.EndArea();
    }

    // ────────────────────────────────────────────────
    // ヘルパー：FactionComponent(Player) からゲームオブジェクトを取得
    // ────────────────────────────────────────────────
    private GameObject FindPlayerGameObject()
    {
        var factions = FindObjectsByType<FactionComponent>(FindObjectsSortMode.None);
        foreach (var fc in factions)
        {
            if (fc.faction == Faction.Player)
                return fc.gameObject;
        }
        return null;
    }

    // ────────────────────────────────────────────────
    // ボタン1: 恢复玩家满血
    // ────────────────────────────────────────────────
    private void RestorePlayerFullHealth()
    {
        var player = FindPlayerGameObject();
        if (player == null) { Debug.LogWarning("[DebugUI] Player not found."); return; }

        var health = player.GetComponent<HealthComponent>();
        if (health == null) { Debug.LogWarning("[DebugUI] Player HealthComponent not found."); return; }

        Debug.Log("[DebugUI] Restore player full health button clicked.");
        health.RestoreFullHealth();
    }

    // ────────────────────────────────────────────────
    // ボタン2: 原地复活玩家测试
    // ────────────────────────────────────────────────
    private void RespawnPlayerTest()
    {
        var player = FindPlayerGameObject();
        if (player == null) { Debug.LogWarning("[DebugUI] Player not found."); return; }

        var health       = player.GetComponent<HealthComponent>();
        var deathHandler = player.GetComponent<PlayerDeathHandler>();

        if (health == null)       { Debug.LogWarning("[DebugUI] Player HealthComponent not found."); return; }
        if (deathHandler == null) { Debug.LogWarning("[DebugUI] PlayerDeathHandler not found.");     return; }

        health.RestoreFullHealth();
        deathHandler.ResetForRespawn();

        var levelManager = FindFirstObjectByType<LevelObjectiveManager>();
        if (levelManager != null)
            levelManager.ClearLevelResultForRespawn();
        else
            Debug.LogWarning("[DebugUI] LevelObjectiveManager not found. UI not cleared.");

        Debug.Log("[DebugUI] Player respawn test executed.");
    }

    // ────────────────────────────────────────────────
    // ボタン3: 复活到最近存档点
    // ────────────────────────────────────────────────
    private void RespawnPlayerAtSavePointTest()
    {
        var player = FindPlayerGameObject();
        if (player == null) { Debug.LogWarning("[DebugUI] Player not found."); return; }

        var health          = player.GetComponent<HealthComponent>();
        var deathHandler    = player.GetComponent<PlayerDeathHandler>();
        var respawnTracker  = player.GetComponent<PlayerRespawnPointTracker>();

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
        if (respawnTracker == null)
        {
            Debug.LogWarning("[DebugUI] PlayerRespawnPointTracker not found.");
            return;
        }

        // 先传送到最近存档点，再恢复物理/控制（ResetForRespawn 会清零速度）
        player.transform.SetPositionAndRotation(
            respawnTracker.CurrentRespawnPosition,
            respawnTracker.CurrentRespawnRotation
        );

        health.RestoreFullHealth();
        deathHandler.ResetForRespawn();

        var levelManager = FindFirstObjectByType<LevelObjectiveManager>();
        if (levelManager != null)
            levelManager.ClearLevelResultForRespawn();
        else
            Debug.LogWarning("[DebugUI] LevelObjectiveManager not found. UI not cleared.");

        Debug.Log($"[DebugUI] Player respawned at SavePoint: {respawnTracker.CurrentRespawnPosition}");
    }
}
