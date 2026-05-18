using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 技能栏 Canvas UI — B 方案（修正版）。
/// SkillBar の pivot = (1, 0)（右下）を利用して、
/// 技能数が増えるほど SkillBar が左方向に広がる。
/// 子スロットは SkillBar 内部で左→右（index 0 が左端）に配置。
/// HorizontalLayoutGroup に依存しない。
/// </summary>
public class PlayerSkillBarCanvasUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerSkillManager  skillManager;
    [SerializeField] private PlayerSkillCanvasUI slotTemplate;  // SkillSlotTemplate を指定
    [SerializeField] private Transform           slotRoot;      // 空なら this.transform

    [Header("レイアウト")]
    [SerializeField] private Vector2 slotSize    = new Vector2(96f, 112f);
    [SerializeField] private float   spacing     = 8f;

    [Header("SkillBar 位置（右下からのオフセット）")]
    [SerializeField] private float rightOffset  = 40f;
    [SerializeField] private float bottomOffset = 40f;

    [Header("動作")]
    [SerializeField] private bool rebuildOnStart        = true;
    [SerializeField] private bool hideTemplateAtRuntime = true;

    private readonly List<PlayerSkillCanvasUI> _spawnedSlots = new List<PlayerSkillCanvasUI>();

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Start()
    {
        if (slotRoot == null) slotRoot = transform;
        ResolveSkillManager();
        if (rebuildOnStart) RebuildSlots();
    }

    // ─── 公开方法 ─────────────────────────────────────────────────

    public void RebuildSlots()
    {
        if (skillManager == null)
        {
            Debug.LogWarning("[PlayerSkillBarCanvasUI] PlayerSkillManager not found.");
            return;
        }
        if (slotTemplate == null)
        {
            Debug.LogWarning("[PlayerSkillBarCanvasUI] slotTemplate is not set.");
            return;
        }

        // ── テンプレートを非表示（B 方案）
        if (hideTemplateAtRuntime)
            slotTemplate.gameObject.SetActive(false);

        // ── 前回生成スロットを全削除
        foreach (var s in _spawnedSlots)
            if (s != null) Destroy(s.gameObject);
        _spawnedSlots.Clear();

        var states = skillManager.RuntimeStates;
        int count  = states != null ? states.Count : 0;

        // ── SkillBar 自身の RectTransform を設定
        //    pivot = (1, 0) にすることで、sizeDelta が増えると左方向に広がる。
        var rootRT = slotRoot as RectTransform;
        if (rootRT != null)
        {
            float totalWidth    = count * slotSize.x + Mathf.Max(0, count - 1) * spacing;
            rootRT.anchorMin        = new Vector2(1f, 0f);
            rootRT.anchorMax        = new Vector2(1f, 0f);
            rootRT.pivot            = new Vector2(1f, 0f);
            rootRT.anchoredPosition = new Vector2(-rightOffset, bottomOffset);
            rootRT.sizeDelta        = new Vector2(totalWidth, slotSize.y);
        }

        if (count == 0)
        {
            Debug.Log("[PlayerSkillBarCanvasUI] No skills registered.");
            return;
        }

        // ── 各技能格を生成・配置
        //    SkillBar 内部の座標：anchorMin/Max = (0,0) + pivot = (0,0) で左端を起点。
        //    index 0 が左端（X = 0）、index 1 がその右（X = slotSize.x + spacing）…
        for (int i = 0; i < count; i++)
        {
            var go   = Instantiate(slotTemplate.gameObject, slotRoot);
            go.name  = "SkillSlot_" + states[i].SkillId;
            go.SetActive(true);

            var rt        = go.GetComponent<RectTransform>();
            rt.anchorMin  = new Vector2(0f, 0f);
            rt.anchorMax  = new Vector2(0f, 0f);
            rt.pivot      = new Vector2(0f, 0f);
            rt.sizeDelta  = slotSize;
            rt.anchoredPosition = new Vector2(i * (slotSize.x + spacing), 0f);

            var slot = go.GetComponent<PlayerSkillCanvasUI>();
            if (slot == null)
            {
                Debug.LogWarning("[PlayerSkillBarCanvasUI] slotTemplate has no PlayerSkillCanvasUI.");
                continue;
            }
            slot.Initialize(skillManager, states[i]);
            _spawnedSlots.Add(slot);
        }

        Debug.Log($"[PlayerSkillBarCanvasUI] Built {_spawnedSlots.Count} slot(s).");
    }

    // ─── Private ─────────────────────────────────────────────────

    private void ResolveSkillManager()
    {
        if (skillManager != null) return;
        skillManager = FindFirstObjectByType<PlayerSkillManager>();
        if (skillManager == null)
            Debug.LogWarning("[PlayerSkillBarCanvasUI] PlayerSkillManager not found in scene.");
    }
}
