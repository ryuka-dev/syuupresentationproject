using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 玩家目标选择系统（New Input System）
/// 左键選択は MouseInputGate.LeftWorldPressedThisFrame のみで判定。
/// MouseInputGate がない場合は左键選択を無効化（安全失敗）、Tab は継続。
/// </summary>
public class PlayerTargeting : MonoBehaviour
{
    public Transform CurrentTarget { get; private set; }

    [Header("Tab 目标选择")]
    [SerializeField] private bool    allowTabTargeting         = true;
    [SerializeField] private float   tabTargetMaxDistance      = 30f;
    [SerializeField] private Vector2 tabTargetViewportPadding  = Vector2.zero;

    [Header("Input")]
    [SerializeField] private MouseInputGate mouseInputGate;

    private FactionComponent _selfFaction;
    private bool             _gateWarned;

    private struct TabTargetCandidate
    {
        public Transform Target;
        public float     ViewportX;
        public float     Distance;
    }

    private void Awake()
    {
        _selfFaction = GetComponent<FactionComponent>();
        if (_selfFaction == null)
            Debug.LogWarning("[PlayerTargeting] FactionComponent not found. Enemy targeting disabled.");

        if (mouseInputGate == null)
            mouseInputGate = GetComponent<MouseInputGate>()
                          ?? FindFirstObjectByType<MouseInputGate>();
        if (mouseInputGate == null)
        {
            Debug.LogWarning("[PlayerTargeting] MouseInputGate not found. Left-click targeting disabled.");
            _gateWarned = true;
        }
    }

    private void Update()
    {
        HandleMouseTargetInput();
        HandleTabTargetInput();
    }

    public void ClearTarget()
    {
        CurrentTarget = null;
        Debug.Log("[PlayerTargeting] CurrentTarget cleared.");
    }

    // ── 左键選択：MouseInputGate 必須、fallback なし ──────────────
    private void HandleMouseTargetInput()
    {
        // MouseInputGate がない場合は何もしない（安全失敗）
        if (mouseInputGate == null) return;
        if (!mouseInputGate.LeftWorldPressedThisFrame) return;

        if (Camera.main == null)
        {
            Debug.LogWarning("[PlayerTargeting] Camera.main is null.");
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
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

    // ── Tab 選択 ────────────────────────────────────────────────
    private void HandleTabTargetInput()
    {
        if (!allowTabTargeting) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current.tabKey.wasPressedThisFrame) return;
        SelectNextTargetFromLeftToRight();
    }

    private void SelectNextTargetFromLeftToRight()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[PlayerTargeting] Camera.main is null."); return; }

        var candidates = BuildTabCandidates(cam);
        if (candidates.Count == 0) { ClearTarget(); return; }

        candidates.Sort((a, b) =>
        {
            int cmp = a.ViewportX.CompareTo(b.ViewportX);
            return cmp != 0 ? cmp : a.Distance.CompareTo(b.Distance);
        });

        int currentIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
            if (candidates[i].Target == CurrentTarget) { currentIndex = i; break; }

        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % candidates.Count;
        SetTarget(candidates[nextIndex].Target);
        Debug.Log($"[PlayerTargeting] Tab 选择目标：{CurrentTarget.name}");
    }

    private List<TabTargetCandidate> BuildTabCandidates(Camera cam)
    {
        var candidates = new List<TabTargetCandidate>();
        var allHealth  = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);

        foreach (var health in allHealth)
        {
            if (health == null || health.IsDead || health.transform == transform) continue;
            var faction = health.GetComponent<FactionComponent>();
            if (faction == null || !IsValidEnemyTarget(health, faction)) continue;

            float dist = Vector3.Distance(transform.position, health.transform.position);
            if (dist > tabTargetMaxDistance) continue;

            Vector3 viewport = cam.WorldToViewportPoint(health.transform.position);
            if (viewport.z <= 0f) continue;
            float px = tabTargetViewportPadding.x, py = tabTargetViewportPadding.y;
            if (viewport.x < -px || viewport.x > 1f + px) continue;
            if (viewport.y < -py || viewport.y > 1f + py) continue;

            candidates.Add(new TabTargetCandidate
            {
                Target    = faction.transform,
                ViewportX = viewport.x,
                Distance  = dist,
            });
        }
        return candidates;
    }

    private bool IsValidEnemyTarget(HealthComponent health, FactionComponent faction)
    {
        if (health == null || faction == null || _selfFaction == null) return false;
        return _selfFaction.ShouldAttack(faction.faction);
    }

    private void SetTarget(Transform t)
    {
        CurrentTarget = t;
        Debug.Log($"[PlayerTargeting] 当前目标：{CurrentTarget.name}");
    }
}
