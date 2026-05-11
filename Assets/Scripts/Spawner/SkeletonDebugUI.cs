using UnityEngine;
using UnityEngine.InputSystem;

public class SkeletonDebugUI : MonoBehaviour
{
    public SkeletonSpawner spawner;
    [SerializeField] private PlayerEquipment   playerEquipment;
    [SerializeField] private PlayerCombatStats playerCombatStats;
    [SerializeField] private ItemData testCoreItem;

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

        GUILayout.BeginArea(new Rect(20, 20, 300, 1000));

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

        // ─── 敌人调试 ────────────────────────────────────────
        GUILayout.Space(10);
        GUILayout.Label("\u2500\u2500\u2500 \u654c\u4eba\u8c03\u8bd5 \u2500\u2500\u2500");

        var player        = FindPlayerGameObject();
        var targeting     = player        != null ? player.GetComponent<PlayerTargeting>()        : null;
        var currentTarget = targeting     != null ? targeting.CurrentTarget                        : null;
        var enemyAI       = currentTarget != null ? currentTarget.GetComponent<EnemyAI>()         : null;
        var healthComp    = currentTarget != null ? currentTarget.GetComponent<HealthComponent>()  : null;

        bool canReset = enemyAI    != null && enemyAI.enabled
                     && healthComp != null && !healthComp.IsDead;

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

        GUI.backgroundColor = canReset ? new Color(1f, 0.5f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
        if (GUILayout.Button("\u91cd\u7f6e\u5f53\u524d\u76ee\u6807\u654c\u4eba", GUILayout.Height(40)))
            ResetCurrentTargetEnemy(enemyAI, healthComp);
        GUI.backgroundColor = Color.white;

        // ─── 装备调试 ────────────────────────────────────────
        GUILayout.Space(10);
        GUILayout.Label("\u2500\u2500\u2500 \u88c5\u5907\u8c03\u8bd5 \u2500\u2500\u2500");

        var pe = ResolvePlayerEquipment(warnIfMissing: false);
        if (pe != null)
        {
            string coreLabel = pe.HasCoreEquipped
                ? $"\u5f53\u524d Core\uff1a{pe.EquippedCore.ItemName}\uff08{pe.EquippedCore.ItemId}\uff09"
                : "\u5f53\u524d Core\uff1a\u65e0";
            GUILayout.Label(coreLabel);
        }
        else
        {
            GUILayout.Label("\u5f53\u524d Core\uff1a\uff08PlayerEquipment \u672a\u6302\u8f7d\uff09");
        }

        GUI.backgroundColor = new Color(0.5f, 0.85f, 1f);
        if (GUILayout.Button("\u88c5\u5907\u6d4b\u8bd5 Core", GUILayout.Height(40)))
            EquipTestCore();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
        if (GUILayout.Button("\u5378\u4e0b\u6d4b\u8bd5 Core", GUILayout.Height(40)))
            UnequipTestCore();
        GUI.backgroundColor = Color.white;

        // ─── 战斗属性调试 ─────────────────────────────────────
        GUILayout.Space(10);
        GUILayout.Label("\u2500\u2500\u2500 \u6218\u6597\u5c5e\u6027\u8c03\u8bd5 \u2500\u2500\u2500");

        var cs = ResolvePlayerCombatStats();
        if (cs != null)
        {
            GUILayout.Label($"Base Normal Attack Damage: {cs.BaseNormalAttackDamage}");
            GUILayout.Label($"Equipment Attack Bonus: {cs.EquipmentAttackPowerBonus}");
            GUILayout.Label($"Current Normal Attack Damage: {cs.CurrentNormalAttackDamage}");
            GUILayout.Label($"Equipment Max Health Bonus: {cs.EquipmentMaxHealthBonus}");
            GUILayout.Label($"Base Max Health: {cs.BaseMaxHealth}");
            GUILayout.Label($"Current Max Health: {cs.CurrentMaxHealth}");
        }
        else
        {
            GUILayout.Label("PlayerCombatStats \u672a\u6302\u8f7d");
        }

        GUI.backgroundColor = new Color(0.6f, 1f, 0.7f);
        if (GUILayout.Button("\u5e94\u7528\u5f53\u524d\u6700\u5927\u751f\u547d\u5024", GUILayout.Height(40)))
            ApplyCurrentMaxHealth();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        GUILayout.Label("F1: Toggle");

        GUILayout.EndArea();
    }

    // ─── 最大生命値適用 ──────────────────────────────────────

    private void ApplyCurrentMaxHealth()
    {
        var cs = ResolvePlayerCombatStats();
        if (cs == null)
        {
            Debug.LogWarning("[DebugUI] ApplyCurrentMaxHealth: PlayerCombatStats not found.");
            return;
        }
        var p = FindPlayerGameObject();
        if (p == null)
        {
            Debug.LogWarning("[DebugUI] ApplyCurrentMaxHealth: Player not found.");
            return;
        }
        var health = p.GetComponent<HealthComponent>();
        if (health == null)
        {
            Debug.LogWarning("[DebugUI] ApplyCurrentMaxHealth: Player HealthComponent not found.");
            return;
        }
        float before = health.maxHealth;
        float beforeCurrent = health.currentHealth;
        health.SetMaxHealth(cs.CurrentMaxHealth, keepCurrentRatio: false);
        Debug.Log($"[DebugUI] ApplyCurrentMaxHealth: maxHealth {before}->{health.maxHealth}, currentHealth {beforeCurrent}->{health.currentHealth}");
    }

    // ─── 装备调试ヘルパー ──────────────────────────────────

    private PlayerEquipment ResolvePlayerEquipment(bool warnIfMissing)
    {
        if (playerEquipment != null) return playerEquipment;
        var p = FindPlayerGameObject();
        if (p != null) playerEquipment = p.GetComponent<PlayerEquipment>();
        if (playerEquipment == null && warnIfMissing)
            Debug.LogWarning("[DebugUI] PlayerEquipment not found on Player.");
        return playerEquipment;
    }

    private PlayerCombatStats ResolvePlayerCombatStats()
    {
        if (playerCombatStats != null) return playerCombatStats;
        var p = FindPlayerGameObject();
        if (p != null) playerCombatStats = p.GetComponent<PlayerCombatStats>();
        return playerCombatStats;
    }

    private void EquipTestCore()
    {
        if (testCoreItem == null)
        {
            Debug.LogWarning("[DebugUI] testCoreItem is null. Assign a Core ItemData in the Inspector.");
            return;
        }
        var pe = ResolvePlayerEquipment(warnIfMissing: true);
        if (pe == null) return;
        pe.EquipCore(testCoreItem);
    }

    private void UnequipTestCore()
    {
        var pe = ResolvePlayerEquipment(warnIfMissing: true);
        if (pe == null) return;
        pe.UnequipCore();
    }

    // ─── 既存のヘルパー（変更なし）──────────────────────────

    private GameObject FindPlayerGameObject()
    {
        var factions = FindObjectsByType<FactionComponent>(FindObjectsSortMode.None);
        foreach (var fc in factions)
            if (fc.faction == Faction.Player) return fc.gameObject;
        return null;
    }

    private void RestorePlayerFullHealth()
    {
        var player = FindPlayerGameObject();
        if (player == null) { Debug.LogWarning("[DebugUI] Player not found."); return; }
        var health = player.GetComponent<HealthComponent>();
        if (health == null) { Debug.LogWarning("[DebugUI] Player HealthComponent not found."); return; }
        Debug.Log("[DebugUI] Restore player full health button clicked.");
        health.RestoreFullHealth();
    }

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
        if (levelManager != null) levelManager.ClearLevelResultForRespawn();
        else Debug.LogWarning("[DebugUI] LevelObjectiveManager not found. UI not cleared.");
        Debug.Log("[DebugUI] Player respawn test executed.");
    }

    private void RespawnPlayerAtSavePointTest()
    {
        var player = FindPlayerGameObject();
        if (player == null) { Debug.LogWarning("[DebugUI] Player not found."); return; }
        var health         = player.GetComponent<HealthComponent>();
        var deathHandler   = player.GetComponent<PlayerDeathHandler>();
        var respawnTracker = player.GetComponent<PlayerRespawnPointTracker>();
        if (health == null)         { Debug.LogWarning("[DebugUI] Player HealthComponent not found."); return; }
        if (deathHandler == null)   { Debug.LogWarning("[DebugUI] PlayerDeathHandler not found."); return; }
        if (respawnTracker == null) { Debug.LogWarning("[DebugUI] PlayerRespawnPointTracker not found."); return; }
        player.transform.SetPositionAndRotation(
            respawnTracker.CurrentRespawnPosition,
            respawnTracker.CurrentRespawnRotation);
        health.RestoreFullHealth();
        deathHandler.ResetForRespawn();
        var levelManager = FindFirstObjectByType<LevelObjectiveManager>();
        if (levelManager != null) levelManager.ClearLevelResultForRespawn();
        else Debug.LogWarning("[DebugUI] LevelObjectiveManager not found. UI not cleared.");
        Debug.Log($"[DebugUI] Player respawned at SavePoint: {respawnTracker.CurrentRespawnPosition}");
    }

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
