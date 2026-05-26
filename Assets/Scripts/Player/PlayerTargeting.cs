using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 玩家目标选择系统（New Input System）
/// 挂载在 Player 对象上。
///
/// 支持两种选择方式：
///   1. 鼠标左键 Raycast 点击（MouseInputGate.LeftWorldPressedThisFrame で UI 起点を除外）
///   2. Tab 键从屏幕左到右循环选中敌人
/// </summary>
public class PlayerTargeting : MonoBehaviour
{
    // ─── Public API ───────────────────────────────────────────────

    public Transform CurrentTarget { get; private set; }

    // ─── Tab 目标选择設定 ─────────────────────────────────────────

    [Header("Tab 目标选择")]
    [SerializeField] private bool    allowTabTargeting           = true;
    [SerializeField] private float   tabTargetMaxDistance        = 30f;
    [SerializeField] private Vector2 tabTargetViewportPadding   = Vector2.zero;

    [Header("Input")]
    [SerializeField] private MouseInputGate mouseInputGate;

    // ─── 运行时 ───────────────────────────────────────────────────

    private FactionComponent _selfFaction;

    // Tab 候選キャッシュ用構造体
    private struct TabTargetCandidate
    {
        public Transform Target;
        public float     ViewportX;
        public float     Distance;
    }

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        _selfFaction = GetComponent<FactionComponent>();
        if (_selfFaction == null)
            Debug.LogWarning("[PlayerTargeting] Player 上找不到 FactionComponent，敌对判断将失效。");

        if (mouseInputGate == null)
            mouseInputGate = GetComponentInChildren<MouseInputGate>()
                          ?? FindFirstObjectByType<MouseInputGate>();
        if (mouseInputGate == null)
            Debug.LogWarning("[PlayerTargeting] MouseInputGate not found. Left-click targeting will be disabled.");
    }

    private void Update()
    {
        HandleMouseTargetInput();
        HandleTabTargetInput();
    }

    // ─── Public メソッド ──────────────────────────────────────────

    public void ClearTarget()
    {
        CurrentTarget = null;
        Debug.Log("[PlayerTargeting] CurrentTarget cleared.");
    }

    // ─── Mouse 选择ロジック ───────────────────────────────────────

    private void HandleMouseTargetInput()
    {
        // MouseInputGate.LeftWorldPressedThisFrame : UI 上でのクリックは除外済み
        bool shouldProcess = mouseInputGate != null
            ? mouseInputGate.LeftWorldPressedThisFrame
            : (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);  // fallback

        if (!shouldProcess) return;

        if (Camera.main == null)
        {
            Debug.LogWarning("[PlayerTargeting] Camera.main 为空，无法发射射线。");
            return;
        }

        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var health  = hit.collider.GetComponentInParent<HealthComponent>();
        var faction = hit.collider.GetComponentInParent<FactionComponent>();

        if (health == null || faction == null)
        {
            Debug.Log("[PlayerTargeting] 点击目标无 HealthComponent 或 FactionComponent，忽略。");
            return;
        }

        if (!IsValidEnemyTarget(health, faction))
        {
            Debug.Log($"[PlayerTargeting] {faction.gameObject.name} 不是敌对目标，忽略。");
            return;
        }

        SetTarget(faction.transform);
    }

    // ─── Tab 選択ロジック ─────────────────────────────────────────

    private void HandleTabTargetInput()
    {
        if (!allowTabTargeting)  return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current.tabKey.wasPressedThisFrame) return;

        SelectNextTargetFromLeftToRight();
    }

    private void SelectNextTargetFromLeftToRight()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[PlayerTargeting] Camera.main 为空，无法进行 Tab 选择。");
            return;
        }

        var candidates = BuildTabCandidates(cam);

        if (candidates.Count == 0)
        {
            ClearTarget();
            return;
        }

        candidates.Sort((a, b) =>
        {
            int cmp = a.ViewportX.CompareTo(b.ViewportX);
            return cmp != 0 ? cmp : a.Distance.CompareTo(b.Distance);
        });

        int currentIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Target == CurrentTarget)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex;
        if (currentIndex < 0)
            nextIndex = 0;
        else
            nextIndex = (currentIndex + 1) % candidates.Count;

        SetTarget(candidates[nextIndex].Target);
        Debug.Log($"[PlayerTargeting] Tab 选择目标：{CurrentTarget.name} (viewport.x={candidates[nextIndex].ViewportX:F2})");
    }

    private List<TabTargetCandidate> BuildTabCandidates(Camera cam)
    {
        var candidates = new List<TabTargetCandidate>();
        var allHealth  = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);

        foreach (var health in allHealth)
        {
            if (health == null)                continue;
            if (health.IsDead)                 continue;
            if (health.transform == transform) continue;

            var faction = health.GetComponent<FactionComponent>();
            if (faction == null) continue;
            if (!IsValidEnemyTarget(health, faction)) continue;

            float dist = Vector3.Distance(transform.position, health.transform.position);
            if (dist > tabTargetMaxDistance) continue;

            Vector3 viewport = cam.WorldToViewportPoint(health.transform.position);
            if (viewport.z <= 0f) continue;

            float padX = tabTargetViewportPadding.x;
            float padY = tabTargetViewportPadding.y;
            if (viewport.x < -padX || viewport.x > 1f + padX) continue;
            if (viewport.y < -padY || viewport.y > 1f + padY) continue;

            candidates.Add(new TabTargetCandidate
            {
                Target    = faction.transform,
                ViewportX = viewport.x,
                Distance  = dist,
            });
        }

        return candidates;
    }

    // ─── 共通ユーティリティ ───────────────────────────────────────

    private bool IsValidEnemyTarget(HealthComponent health, FactionComponent faction)
    {
        if (health == null || faction == null) return false;
        if (_selfFaction == null)              return false;
        return _selfFaction.ShouldAttack(faction.faction);
    }

    private void SetTarget(Transform target)
    {
        CurrentTarget = target;
        Debug.Log($"[PlayerTargeting] 当前目标：{CurrentTarget.name}");
    }
}
