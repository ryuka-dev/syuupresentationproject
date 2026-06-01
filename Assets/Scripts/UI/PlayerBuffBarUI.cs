using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>プレイヤー Buff バー UI。PlayerBuffController から ActiveBuffs を読んでアイコンを表示する。</summary>
public class PlayerBuffBarUI : MonoBehaviour
{
    [Header("データソース")]
    [SerializeField] private PlayerBuffController buffController;

    [Header("UI 参照")]
    [SerializeField] private RectTransform   contentRoot;
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private PlayerBuffIconUI buffIconTemplate;

    private readonly Dictionary<string, PlayerBuffIconUI> _activeIcons
        = new Dictionary<string, PlayerBuffIconUI>();

    private void Awake()
    {
        if (buffController == null)
            buffController = FindFirstObjectByType<PlayerBuffController>();
        if (buffIconTemplate != null)
            buffIconTemplate.gameObject.SetActive(false);
        if (gridLayout != null && contentRoot != null)
        {
            float cw = gridLayout.cellSize.x, sx = gridLayout.spacing.x;
            int cols = Mathf.Max(1, Mathf.FloorToInt((contentRoot.rect.width + sx) / (cw + sx)));
            gridLayout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = cols;
        }
    }

    private void Update()
    {
        if (buffController == null || buffIconTemplate == null) return;
        var active = buffController.ActiveBuffs;
        var toRemove = new List<string>();
        foreach (var kvp in _activeIcons)
            if (!active.ContainsKey(kvp.Key)) toRemove.Add(kvp.Key);
        foreach (var id in toRemove)
        {
            if (_activeIcons.TryGetValue(id, out var icon) && icon != null) Destroy(icon.gameObject);
            _activeIcons.Remove(id);
        }
        foreach (var kvp in active)
        {
            if (_activeIcons.TryGetValue(kvp.Key, out var existing)) existing.UpdateDisplay(kvp.Value);
            else
            {
                var inst = Instantiate(buffIconTemplate, contentRoot);
                inst.gameObject.SetActive(true);
                inst.Bind(kvp.Key, kvp.Value);
                _activeIcons[kvp.Key] = inst;
            }
        }
    }
}
