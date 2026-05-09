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

        GUILayout.BeginArea(new Rect(20, 20, 300, 560));

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

        // \u2500\u2500\u2500 \u654c\u4eba\u8c03\u8bd5\u533a\u57df \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
        GUILayout.Space(10);
        GUILayout.Label("\u2500\u2500\u2500 \u654c\u4eba\u8c03\u8bd5 \u2500\u2500\u2500");

        // \u4ece Player \u53d6\u5f53\u524d\u9501\u5b9a\u76ee\u6807\u53ca\u76f8\u5173\u7ec4\u4ef6
        var player       = FindPlayerGameObject();
        var targeting    = player     != null ? player.GetComponent<PlayerTargeting>()    : null;
        var currentTarget = targeting != null ? targeting.CurrentTarget                   : null;
        var enemyAI      = currentTarget != null ? currentTarget.GetComponent<EnemyAI>()          : null;
        var healthComp   = currentTarget != null ? currentTarget.GetComponent<HealthComponent>()  : null;

        // \u5168\u6761\u4ef6\u5224\u5b9a\uff1a\u76ee\u6807\u4e0d\u4e3a null\u3001EnemyAI \u5b58\u5728\u4e14\u542f\u7528\u3001HealthComponent \u5b58\u5728\u4e14\u672a\u6b7b\u4ea1
        bool canReset = enemyAI   != null && enemyAI.enabled
                     && healthComp != null && !healthComp.IsDead;

        // \u72b6\u6001\u6807\u7b7e\u663e\u793a
        string targetLabel;
        if (currentTarget == null)
            targetLabel = "\u5f53\u524d\u76ee\u6807\uff1a\u65e0";
        else if (enemyAI == null)
            targetLabel = $"\u5f53\u524d\u76ee\u6807\uff1a{currentTarget.name}\uff08\u975e\u53ef\u91cd\u7f6e\u654c\u4eba\uff09";
        else if (!enemyAI.enabled)
            targetLabel = $"\u5f53\u524d\u76ee\u6807\uff1a{currentTarget.name}\uff08AI\u5df2\u7981\u7528\uff09";
        else if (healthComp == null || healthComp.IsDead)
            targetLabel = $"\u5f53\u524d\u76ee\u6807\uff1a{currentTarget.name}\uff08\u5df2\u6b7b\u4ea1\uff09";
        else
            targetLabel = $"\u5f53\u524d\u76ee\u6807\uff1a{currentTarget.name}";
        GUILayout.Label(targetLabel);

        // \u6309\u9215\uff1a\u6ee1\u8db3\u6761\u4ef6\u65f6\u663e\u793a\u7ea2\u8272\uff0c\u4e0d\u6ee1\u8db3\u65f6\u663e\u793a\u7070\u8272
        GUI.backgroundColor = canReset ? new Color(1f, 0.5f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
        if (GUILayout.Button("\u91cd\u7f6e\u5f53\u524d\u76ee\u6807\u654c\u4eba", GUILayout.Height(40)))
            ResetCurrentTargetEnemy(enemyAI, healthComp);
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        GUILayout.Label("F1: Toggle");

        GUILayout.EndArea();
    }

    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    // \u8f85\u52a9: FactionComponent(Player) \u304b\u3089 GameObject \u3092\u53d6\u5f97
    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
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

    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    // \u6309\u9215 1: \u6062\u590d\u73a9\u5bb6\u6ee1\u8840
    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    private void RestorePlayerFullHealth()
    {
        var player = FindPlayerGameObject();
        if (player == null) { Debug.LogWarning("[DebugUI] Player not found."); return; }

        var health = player.GetComponent<HealthComponent>();
        if (health == null) { Debug.LogWarning("[DebugUI] Player HealthComponent not found."); return; }

        Debug.Log("[DebugUI] Restore player full health button clicked.");
        health.RestoreFullHealth();
    }

    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    // \u6309\u9215 2: \u539f\u5730\u590d\u6d3b\u73a9\u5bb6\u6d4b\u8bd5
    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
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

    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    // \u6309\u9215 3: \u590d\u6d3b\u5230\u6700\u8fd1\u5b58\u6863\u70b9
    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    private void RespawnPlayerAtSavePointTest()
    {
        var player = FindPlayerGameObject();
        if (player == null) { Debug.LogWarning("[DebugUI] Player not found."); return; }

        var health         = player.GetComponent<HealthComponent>();
        var deathHandler   = player.GetComponent<PlayerDeathHandler>();
        var respawnTracker = player.GetComponent<PlayerRespawnPointTracker>();

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

        // \u5148\u4f20\u9001\u5230\u6700\u8fd1\u5b58\u6863\u70b9\uff0c\u518d\u6062\u590d\u7269\u7406/\u63a7\u5236\uff08ResetForRespawn \u4f1a\u6e05\u96f6\u901f\u5ea6\uff09
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

    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    // \u6309\u9215 4: \u91cd\u7f6e\u5f53\u524d\u76ee\u6807\u654c\u4eba\uff08\u5e26\u6d3b\u4f53\u68c0\u67e5\uff09
    // \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    private void ResetCurrentTargetEnemy(EnemyAI enemyAI, HealthComponent healthComp)
    {
        if (enemyAI == null)
        {
            Debug.LogWarning("[DebugUI] ResetCurrentTargetEnemy: \u5f53\u524d\u76ee\u6807\u4e3a\u7a7a\u6216\u65e0 EnemyAI\u3002");
            return;
        }
        if (!enemyAI.enabled)
        {
            Debug.LogWarning($"[DebugUI] ResetCurrentTargetEnemy: {enemyAI.gameObject.name} \u7684 AI \u5df2\u7981\u7528\uff0c\u8df3\u8fc7\u91cd\u7f6e\u3002");
            return;
        }
        if (healthComp == null || healthComp.IsDead)
        {
            Debug.LogWarning($"[DebugUI] ResetCurrentTargetEnemy: {enemyAI.gameObject.name} \u5df2\u6b7b\u4ea1\u6216\u65e0 HealthComponent\uff0c\u8df3\u8fc7\u91cd\u7f6e\u3002");
            return;
        }

        enemyAI.ResetToSpawn();
        Debug.Log($"[DebugUI] ResetToSpawn() \u5df2\u8c03\u7528: {enemyAI.gameObject.name}");
    }
}
