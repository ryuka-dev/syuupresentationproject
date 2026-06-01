using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Combat Momentum（戦闘勢能）ゲージ HUD UI。
/// 3 個の Image 点の alpha で現在の Momentum 点数を表示する。
/// </summary>
public class PlayerCombatMomentumGaugeUI : MonoBehaviour
{
    [Header("データソース")]
    [SerializeField] private PlayerGuardCounterController guardCounterController;

    [Header("UI 参照")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image[]  momentumDots;

    [Header("見た目")]
    [SerializeField] private Color litColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color dimColor = new Color(1f, 0.85f, 0.3f, 0.18f);

    private int _lastMomentum = -1;

    private void Awake()
    {
        if (guardCounterController == null)
            guardCounterController = FindFirstObjectByType<PlayerGuardCounterController>();
        if (titleText != null)
            titleText.text = "战斗势能  Combat Momentum";
    }

    private void Update()
    {
        if (guardCounterController == null || momentumDots == null) return;
        int current = guardCounterController.CurrentCombatMomentum;
        if (current == _lastMomentum) return;
        _lastMomentum = current;
        for (int i = 0; i < momentumDots.Length; i++)
        {
            if (momentumDots[i] == null) continue;
            momentumDots[i].color = (i < current) ? litColor : dimColor;
        }
    }
}
