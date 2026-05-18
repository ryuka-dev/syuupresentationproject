using UnityEngine;
using UnityEngine.InputSystem;

public class SkeletonDebugUI : MonoBehaviour
{
    public SkeletonSpawner spawner;
    [SerializeField] private PlayerEquipment   playerEquipment;
    [SerializeField] private PlayerCombatStats playerCombatStats;
    [SerializeField] private PlayerInventory   playerInventory;
    private PlayerSkillManager playerSkillManager;
    private const string IronBulwarkSkillId = "iron_bulwark";
    [SerializeField] private ItemData testCoreItem;

    private bool    showUI                 = false;
    private Vector2 scrollPosition;
    private Vector2 inventoryScrollPosition;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame)
            showUI = !showUI;
    }

    void OnGUI()
    {
        if (!showUI) return;

        float margin      = 20f;
        float panelWidth  = Mathf.Clamp(Screen.width * 0.32f, 320f, 420f);
        float panelHeight = Mathf.Max(300f, Screen.height - margin * 2f);

        // ─── 左パネル ────────────────────────────────────────
        GUILayout.BeginArea(new Rect(margin, margin, panelWidth, panelHeight), GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(
            scrollPosition,
            GUILayout.Width(panelWidth  - 10f),
            GUILayout.Height(panelHeight - 10f)
        );

        GUILayout.Label("Skeleton Spawner", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
        GUILayout.Space(10);
        GUILayout.Label($"Count: {spawner.GetCount()} / {spawner.maxCount}");

        if (GUILayout.Button("Spawn 1", GUILayout.Height(40))) spawner.SpawnSkeleton();
        if (GUILayout.Button("Spawn 5", GUILayout.Height(40)))
            for (int i = 0; i < 5; i++) spawner.SpawnSkeleton();

        GUILayout.Space(10);
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear All", GUILayout.Height(40))) spawner.ClearAll();
        GUI.backgroundColor = Color.white;
        GUILayout.Space(10);

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("\u6062\u590d\u73a9\u5bb6\u6ee1\u8840", GUILayout.Height(40))) RestorePlayerFullHealth();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("\u590d\u6d3b\u73a9\u5bb6\u6d4b\u8bd5", GUILayout.Height(40))) RespawnPlayerTest();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.75f, 0.2f);
        if (GUILayout.Button("\u590d\u6d3b\u5230\u6700\u8fd1\u5b58\u6863\u70b9", GUILayout.Height(40))) RespawnPlayerAtSavePointTest();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        GUILayout.Label("\u2500\u2500\u2500 \u654c\u4eba\u8c03\u8bd5 \u2500\u2500\u2500");

        var player        = FindPlayerGameObject();
        var targeting     = player        != null ? player.GetComponent<PlayerTargeting>()        : null;
        var currentTarget = targeting     != null ? targeting.CurrentTarget                        : null;
        var enemyAI       = currentTarget != null ? currentTarget.GetComponent<EnemyAI>()         : null;
        var healthComp    = currentTarget != null ? currentTarget.GetComponent<HealthComponent>()  : null;
        bool canReset     = enemyAI != null && enemyAI.enabled && healthComp != null && !healthComp.IsDead;

        string targetLabel;
        if (currentTarget == null) targetLabel = "\u5f53\u524d\u76ee\u6807\uff1a\u65e0";
        else if (enemyAI == null)  targetLabel = $"\u5f53\u524d\u76ee\u6807\uff1a{currentTarget.name}\uff08\u975e\u53ef\u91cd\u7f6e\u654c\u4eba\uff09";
        else if (!enemyAI.enabled) targetLabel = $"\u5f53\u524d\u76ee\u6807\uff1a{currentTarget.name}\uff08AI\u5df2\u7981\u7528\uff09";
        else if (healthComp == null || healthComp.IsDead) targetLabel = $"\u5f53\u524d\u76ee\u6807\uff1a{currentTarget.name}\uff08\u5df2\u6b7b\u4ea1\uff09";
        else targetLabel = $"\u5f53\u524d\u76ee\u6807\uff1a{currentTarget.name}";
        GUILayout.Label(targetLabel);

        GUI.backgroundColor = canReset ? new Color(1f, 0.5f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
        if (GUILayout.Button("\u91cd\u7f6e\u5f53\u524d\u76ee\u6807\u654c\u4eba", GUILayout.Height(40)))
            ResetCurrentTargetEnemy(enemyAI, healthComp);
        GUI.backgroundColor = Color.white;

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
        else GUILayout.Label("\u5f53\u524d Core\uff1a\uff08PlayerEquipment \u672a\u6302\u8f7d\uff09");

        // ── Core ボタン ──
        GUI.backgroundColor = new Color(0.5f, 0.85f, 1f);
        if (GUILayout.Button("\u88c5\u5907\u6d4b\u8bd5 Core", GUILayout.Height(40))) EquipTestCore();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
        if (GUILayout.Button("\u5f3a\u5236\u6e05\u7a7a Core\uff08Debug\uff09", GUILayout.Height(40))) UnequipTestCore();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.85f, 0.5f);
        if (GUILayout.Button("\u5378\u4e0b Core \u5230\u80cc\u5305", GUILayout.Height(40))) UnequipCoreToInventory();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("\u88c5\u5907\u80cc\u5305\u4e2d\u7684\u7b2c\u4e00\u4e2a Core", GUILayout.Height(40)))
            EquipFirstCoreFromInventory();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(6);

        // ── Armor ボタン（新規） ──
        GUI.backgroundColor = new Color(0.7f, 1f, 0.85f);
        if (GUILayout.Button("\u88c5\u5907\u80cc\u5305\u4e2d\u7684\u7b2c\u4e00\u4e2a Armor", GUILayout.Height(40)))
            EquipFirstArmorFromInventory();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
        if (GUILayout.Button("\u5378\u4e0b Armor \u5230\u80cc\u5305", GUILayout.Height(40)))
            UnequipArmorToInventory();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(6);

        // ── Accessory ボタン（新規） ──
        GUI.backgroundColor = new Color(0.9f, 0.75f, 1f);
        if (GUILayout.Button("\u88c5\u5907\u80cc\u5305\u4e2d\u7684\u7b2c\u4e00\u4e2a Accessory", GUILayout.Height(40)))
            EquipFirstAccessoryFromInventory();
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.85f, 0.75f);
        if (GUILayout.Button("\u5378\u4e0b Accessory \u5230\u80cc\u5305", GUILayout.Height(40)))
            UnequipAccessoryToInventory();
        GUI.backgroundColor = Color.white;

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
        else GUILayout.Label("PlayerCombatStats \u672a\u6302\u8f7d");

        GUI.backgroundColor = new Color(0.6f, 1f, 0.7f);
        if (GUILayout.Button("\u5e94\u7528\u5f53\u524d\u6700\u5927\u751f\u547d\u5024", GUILayout.Height(40)))
            ApplyCurrentMaxHealth();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        GUILayout.Space(10);
        DrawMitigationStatusSection();
        GUILayout.Space(4);
        GUILayout.Label("F1: Toggle");

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        // ─── 装备状態窓口 ────────────────────────────────────
        DrawEquipmentStatusWindow(margin, panelWidth);

        // ─── 右パネル（背包调试） ────────────────────────────
        float invPanelWidth  = Mathf.Clamp(Screen.width * 0.28f, 320f, 460f);
        float invPanelHeight = Mathf.Max(300f, Screen.height - margin * 2f);
        float invPanelX      = Screen.width  - invPanelWidth  - margin;
        float invPanelY      = margin;

        GUILayout.BeginArea(new Rect(invPanelX, invPanelY, invPanelWidth, invPanelHeight), GUI.skin.box);
        inventoryScrollPosition = GUILayout.BeginScrollView(
            inventoryScrollPosition,
            GUILayout.Width(invPanelWidth  - 10f),
            GUILayout.Height(invPanelHeight - 10f)
        );
        DrawInventoryDebugPanel();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ─── 装备状態 Debug ウィンドウ ────────────────────────────

private void DrawEquipmentStatusWindow(float margin, float leftPanelWidth)
    {
        const float gap        = 12f;
        const float equipWidth = 310f;

        float equipX      = margin + leftPanelWidth + gap;
        float equipY      = margin;
        float equipHeight = CalculateEquipmentStatusWindowHeight(equipWidth);

        GUILayout.BeginArea(new Rect(equipX, equipY, equipWidth, equipHeight), GUI.skin.box);

        var boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("--- \u88c5\u5907\u72b6\u6001 ---", boldStyle);
        GUILayout.Space(4);

        var eqp = ResolvePlayerEquipment(warnIfMissing: false);
        if (eqp == null)
        {
            GUILayout.Label("PlayerEquipment not found");
        }
        else
        {
            DrawEquipmentSlotLine("Core",      eqp.EquippedCore);
            DrawEquipmentSlotLine("Armor",     eqp.EquippedArmor);
            DrawEquipmentSlotLine("Accessory", eqp.EquippedAccessory);
        }

        GUILayout.Space(6);
        GUILayout.Label("--- \u6218\u6597\u5c5e\u6027\u6c47\u603b ---", boldStyle);

        var cs = ResolvePlayerCombatStats();
        if (cs == null)
        {
            GUILayout.Label("PlayerCombatStats not found");
        }
        else
        {
            GUILayout.Label($"Equipment ATK Bonus: {cs.EquipmentAttackPowerBonus}");
            GUILayout.Label($"Equipment Max HP Bonus: {cs.EquipmentMaxHealthBonus}");
            GUILayout.Label($"Current Normal Attack: {cs.CurrentNormalAttackDamage}");
            GUILayout.Label($"Current Max Health: {cs.CurrentMaxHealth}");
        }

        GUILayout.EndArea();
    }

private void DrawEquipmentSlotLine(string slotName, ItemData item)
    {
        var lines = BuildEquipmentSlotLines(slotName, item);
        foreach (var line in lines)
            GUILayout.Label(line);
    }

private string[] BuildEquipmentSlotLines(string slotName, ItemData item)
    {
        if (item == null)
            return new[] { $"{slotName}: \u672a\u88c5\u5907" };

        var sb = new System.Text.StringBuilder("  id: " + item.ItemId);
        if (item.AttackPowerBonus > 0f) sb.Append($" / ATK +{item.AttackPowerBonus}");
        if (item.MaxHealthBonus   > 0f) sb.Append($" / HP +{item.MaxHealthBonus}");

        return new[] { $"{slotName}: {item.ItemName}", sb.ToString() };
    }



private float CalculateEquipmentStatusWindowHeight(float width)
    {
        // lineHeight は 24f を下限にすることで、未装備状態（9行）でも
        // 計算値が 240f を超え、1件装備するたびに確実に高さが増加する。
        float lineHeight = Mathf.Max(24f, GUI.skin.label.lineHeight + 4f);
        int lineCount = 0;

        lineCount += 1; // --- 装备状态 ---

        var eqp = ResolvePlayerEquipment(warnIfMissing: false);
        if (eqp == null)
        {
            lineCount += 1; // "PlayerEquipment not found"
        }
        else
        {
            lineCount += BuildEquipmentSlotLines("Core",      eqp.EquippedCore).Length;
            lineCount += BuildEquipmentSlotLines("Armor",     eqp.EquippedArmor).Length;
            lineCount += BuildEquipmentSlotLines("Accessory", eqp.EquippedAccessory).Length;
        }

        lineCount += 1; // --- 战斗属性汇总 ---

        var cs = ResolvePlayerCombatStats();
        if (cs == null)
        {
            lineCount += 1; // "PlayerCombatStats not found"
        }
        else
        {
            lineCount += 4; // ATK Bonus, Max HP Bonus, Normal Attack, Max Health
        }

        float height = 8f                  // top padding
                     + lineCount * lineHeight
                     + 4f                  // Space(4) after title
                     + 6f                  // Space(6) between sections
                     + 20f;               // bottom safety margin

        return Mathf.Max(240f, height);
    }




    // ─── 背包调试パネル ──────────────────────────────────────

    private void DrawInventoryDebugPanel()
    {
        GUILayout.Label("\u2500\u2500\u2500 \u80cc\u5305\u8c03\u8bd5 \u2500\u2500\u2500",
            new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
        GUILayout.Space(6);

        var inv = ResolvePlayerInventory();
        if (inv == null)
        {
            GUILayout.Label("PlayerInventory \u672a\u6302\u8f7d");
            return;
        }

        GUILayout.Label($"ItemCount: {inv.ItemCount}");
        GUILayout.Label($"StackCount: {inv.StackCount}");
        GUILayout.Space(6);

        if (inv.Items == null || inv.StackCount == 0)
        {
            GUILayout.Label("\u80cc\u5305\u4e3a\u7a7a");
            return;
        }

        foreach (var stack in inv.Items)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            if (stack == null)
            {
                GUILayout.Label("\u7a7a Stack");
                GUILayout.EndVertical();
                GUILayout.Space(4);
                continue;
            }

            if (stack.ItemData == null)
            {
                GUILayout.Label("\u7f3a\u5931 ItemData");
                GUILayout.EndVertical();
                GUILayout.Space(4);
                continue;
            }

            var data = stack.ItemData;
            GUILayout.Label($"{stack.ItemName} x {stack.Count}");
            GUILayout.Label($"  ID: {stack.ItemId}");
            GUILayout.Label($"  Type: {data.ItemType}");

            if (data.ItemType == ItemType.Equipment)
            {
                GUILayout.Label($"  Slot: {data.EquipmentSlotType}");
                if (data.AttackPowerBonus > 0f)
                    GUILayout.Label($"  ATK Bonus: {data.AttackPowerBonus}");
                if (data.MaxHealthBonus > 0f)
                    GUILayout.Label($"  Max HP Bonus: {data.MaxHealthBonus}");
            }

            GUILayout.EndVertical();
            GUILayout.Space(4);
        }
    }

    // ─── Core 装備 / 卸下 ────────────────────────────────────

    private void UnequipCoreToInventory()
    {
        var inv = ResolvePlayerInventory();
        var eqp = ResolvePlayerEquipment(warnIfMissing: true);
        if (inv == null) { Debug.LogWarning("[DebugUI] UnequipCoreToInventory: PlayerInventory not found."); return; }
        if (eqp == null) return;

        ItemData unequipped = eqp.UnequipCore();
        if (unequipped == null) return;

        if (inv.AddItem(unequipped))
            Debug.Log($"[DebugUI] Core \u300c{unequipped.ItemName}\u300d\u3092\u88c5\u5099\u6307\u304b\u3089\u5916\u3057\u3001\u80cc\u5305\u306b\u623b\u3057\u307e\u3057\u305f\uff08ID: {unequipped.ItemId}\uff09");
        else
            Debug.LogWarning($"[DebugUI] UnequipCoreToInventory: Core \u300c{unequipped.ItemName}\u300d\u3092\u5916\u3057\u307e\u3057\u305f\u304c\u3001\u80cc\u5305\u3078\u306e\u8ffd\u52a0\u306b\u5931\u6557\u3002");
    }

    private void EquipFirstCoreFromInventory()
    {
        var inv = ResolvePlayerInventory();
        var eqp = ResolvePlayerEquipment(warnIfMissing: true);
        if (inv == null) { Debug.LogWarning("[DebugUI] EquipFirstCoreFromInventory: PlayerInventory not found."); return; }
        if (eqp == null) return;

        var newCore = inv.FindFirstEquipmentBySlot(EquipmentSlotType.Core);
        if (newCore == null) { Debug.LogWarning("[DebugUI] EquipFirstCoreFromInventory: \u80cc\u5305\u4e2d\u6ca1\u6709 Core \u88c5\u5907\u3002"); return; }

        bool success = eqp.EquipCore(newCore, out ItemData replacedCore);
        if (!success) return;

        if (!inv.RemoveItem(newCore))
        {
            Debug.LogError("[DebugUI] EquipFirstCoreFromInventory: EquipCore \u6210\u529f\u4f46 RemoveItem \u5931\u8d25\uff01");
            return;
        }

        if (replacedCore != null)
        {
            inv.AddItem(replacedCore);
            Debug.Log($"[DebugUI] \u65e7 Core \u300c{replacedCore.ItemName}\u300d\u5df2\u56de\u5230\u80cc\u5305\u3002");
        }
        Debug.Log($"[DebugUI] \u5df2\u4ece\u80cc\u5305\u88c5\u5907 Core\uff1a{newCore.ItemName}\uff08ID: {newCore.ItemId}\uff09");
    }

    // ─── Armor 装備 / 卸下（新規） ───────────────────────────

    private void EquipFirstArmorFromInventory()
    {
        var inv = ResolvePlayerInventory();
        var eqp = ResolvePlayerEquipment(warnIfMissing: true);
        if (inv == null) { Debug.LogWarning("[DebugUI] EquipFirstArmorFromInventory: PlayerInventory not found."); return; }
        if (eqp == null) return;

        var newArmor = inv.FindFirstEquipmentBySlot(EquipmentSlotType.Armor);
        if (newArmor == null) { Debug.LogWarning("[DebugUI] EquipFirstArmorFromInventory: \u80cc\u5305\u4e2d\u6ca1\u6709 Armor \u88c5\u5907\u3002"); return; }

        bool success = eqp.EquipArmor(newArmor, out ItemData replacedArmor);
        if (!success) return;

        if (!inv.RemoveItem(newArmor))
        {
            Debug.LogError("[DebugUI] EquipFirstArmorFromInventory: EquipArmor \u6210\u529f\u4f46 RemoveItem \u5931\u8d25\uff01");
            return;
        }

        if (replacedArmor != null)
        {
            inv.AddItem(replacedArmor);
            Debug.Log($"[DebugUI] \u65e7 Armor \u300c{replacedArmor.ItemName}\u300d\u5df2\u56de\u5230\u80cc\u5305\u3002");
        }
        Debug.Log($"[DebugUI] \u5df2\u4ece\u80cc\u5305\u88c5\u5907 Armor\uff1a{newArmor.ItemName}\uff08ID: {newArmor.ItemId}\uff09");
    }

    private void UnequipArmorToInventory()
    {
        var inv = ResolvePlayerInventory();
        var eqp = ResolvePlayerEquipment(warnIfMissing: true);
        if (inv == null) { Debug.LogWarning("[DebugUI] UnequipArmorToInventory: PlayerInventory not found."); return; }
        if (eqp == null) return;

        ItemData unequipped = eqp.UnequipArmor();
        if (unequipped == null) return;

        if (inv.AddItem(unequipped))
            Debug.Log($"[DebugUI] Armor \u300c{unequipped.ItemName}\u300d\u3092\u88c5\u5099\u6307\u304b\u3089\u5916\u3057\u3001\u80cc\u5305\u306b\u623b\u3057\u307e\u3057\u305f\uff08ID: {unequipped.ItemId}\uff09");
        else
            Debug.LogWarning($"[DebugUI] UnequipArmorToInventory: Armor \u300c{unequipped.ItemName}\u300d\u3092\u5916\u3057\u307e\u3057\u305f\u304c\u3001\u80cc\u5305\u3078\u306e\u8ffd\u52a0\u306b\u5931\u6557\u3002");
    }

    // ─── Accessory 装備 / 卸下（新規） ───────────────────────

    private void EquipFirstAccessoryFromInventory()
    {
        var inv = ResolvePlayerInventory();
        var eqp = ResolvePlayerEquipment(warnIfMissing: true);
        if (inv == null) { Debug.LogWarning("[DebugUI] EquipFirstAccessoryFromInventory: PlayerInventory not found."); return; }
        if (eqp == null) return;

        var newAccessory = inv.FindFirstEquipmentBySlot(EquipmentSlotType.Accessory);
        if (newAccessory == null) { Debug.LogWarning("[DebugUI] EquipFirstAccessoryFromInventory: \u80cc\u5305\u4e2d\u6ca1\u6709 Accessory \u88c5\u5907\u3002"); return; }

        bool success = eqp.EquipAccessory(newAccessory, out ItemData replacedAccessory);
        if (!success) return;

        if (!inv.RemoveItem(newAccessory))
        {
            Debug.LogError("[DebugUI] EquipFirstAccessoryFromInventory: EquipAccessory \u6210\u529f\u4f46 RemoveItem \u5931\u8d25\uff01");
            return;
        }

        if (replacedAccessory != null)
        {
            inv.AddItem(replacedAccessory);
            Debug.Log($"[DebugUI] \u65e7 Accessory \u300c{replacedAccessory.ItemName}\u300d\u5df2\u56de\u5230\u80cc\u5305\u3002");
        }
        Debug.Log($"[DebugUI] \u5df2\u4ece\u80cc\u5305\u88c5\u5907 Accessory\uff1a{newAccessory.ItemName}\uff08ID: {newAccessory.ItemId}\uff09");
    }

    private void UnequipAccessoryToInventory()
    {
        var inv = ResolvePlayerInventory();
        var eqp = ResolvePlayerEquipment(warnIfMissing: true);
        if (inv == null) { Debug.LogWarning("[DebugUI] UnequipAccessoryToInventory: PlayerInventory not found."); return; }
        if (eqp == null) return;

        ItemData unequipped = eqp.UnequipAccessory();
        if (unequipped == null) return;

        if (inv.AddItem(unequipped))
            Debug.Log($"[DebugUI] Accessory \u300c{unequipped.ItemName}\u300d\u3092\u88c5\u5099\u6307\u304b\u3089\u5916\u3057\u3001\u80cc\u5305\u306b\u623b\u3057\u307e\u3057\u305f\uff08ID: {unequipped.ItemId}\uff09");
        else
            Debug.LogWarning($"[DebugUI] UnequipAccessoryToInventory: Accessory \u300c{unequipped.ItemName}\u300d\u3092\u5916\u3057\u307e\u3057\u305f\u304c\u3001\u80cc\u5305\u3078\u306e\u8ffd\u52a0\u306b\u5931\u6557\u3002");
    }

    // ─── 最大生命値適用 ──────────────────────────────────────

    private void ApplyCurrentMaxHealth()
    {
        var cs = ResolvePlayerCombatStats();
        if (cs == null) { Debug.LogWarning("[DebugUI] ApplyCurrentMaxHealth: PlayerCombatStats not found."); return; }
        cs.ApplyCurrentMaxHealth(keepCurrentRatio: false);
    }

private void DrawMitigationStatusSection()
    {
        GUILayout.Label("--- 玩家减伤状态 ---");

        var sm    = ResolvePlayerSkillManager();
        var state = sm != null ? sm.GetStateBySkillId(IronBulwarkSkillId) : null;

        if (state != null)
        {
            GUILayout.Label("Skill Source: PlayerSkillManager");
            GUILayout.Label($"Skill Id: {IronBulwarkSkillId}");
            GUILayout.Label($"Mitigation Active: {state.IsActive}");
            GUILayout.Label($"Active Remaining: {state.ActiveRemainingTime:F2} s");
            GUILayout.Label($"Cooldown Remaining: {state.CooldownRemainingTime:F2} s");
            string multiplierStr = state.SkillData != null
                ? $"{state.SkillData.DamageTakenMultiplier * 100f:F0}%"
                : "N/A";
            GUILayout.Label($"Damage Taken Multiplier: {multiplierStr}");
        }
        else
        {
            GUILayout.Label("PlayerSkillManager state not found");
            GUILayout.Label($"Skill Id: {IronBulwarkSkillId}");
        }
    }


    // ─── Resolve ヘルパー ─────────────────────────────────────

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

    private PlayerInventory ResolvePlayerInventory()
    {
        if (playerInventory != null) return playerInventory;
        var p = FindPlayerGameObject();
        if (p != null) playerInventory = p.GetComponent<PlayerInventory>();
        return playerInventory;
    }



private PlayerSkillManager ResolvePlayerSkillManager()
    {
        if (playerSkillManager != null) return playerSkillManager;
        var p = FindPlayerGameObject();
        if (p != null) playerSkillManager = p.GetComponent<PlayerSkillManager>();
        return playerSkillManager;
    }



    private void EquipTestCore()
    {
        if (testCoreItem == null) { Debug.LogWarning("[DebugUI] testCoreItem is null. Assign a Core ItemData in the Inspector."); return; }
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

    // ─── 既存ヘルパー ────────────────────────────────────────

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
        if (deathHandler == null) { Debug.LogWarning("[DebugUI] PlayerDeathHandler not found."); return; }
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
        if (enemyAI == null) { Debug.LogWarning("[DebugUI] ResetCurrentTargetEnemy: \u5f53\u524d\u76ee\u6807\u4e3a\u7a7a\u6216\u65e0 EnemyAI\u3002"); return; }
        if (!enemyAI.enabled) { Debug.LogWarning($"[DebugUI] ResetCurrentTargetEnemy: {enemyAI.gameObject.name} \u7684 AI \u5df2\u7981\u7528\uff0c\u8df3\u8fc7\u91cd\u7f6e\u3002"); return; }
        if (healthComp == null || healthComp.IsDead) { Debug.LogWarning($"[DebugUI] ResetCurrentTargetEnemy: {enemyAI.gameObject.name} \u5df2\u6b7b\u4ea1\u6216\u65e0 HealthComponent\uff0c\u8df3\u8fc7\u91cd\u7f6e\u3002"); return; }
        enemyAI.ResetToSpawn();
        Debug.Log($"[DebugUI] ResetToSpawn() \u5df2\u8c03\u7528: {enemyAI.gameObject.name}");
    }
}
