using UnityEngine;

/// <summary>
/// プレイヤーの金貨ウォレット。
/// 金貨は PlayerInventory には入らない。
/// 第一版：int Gold のみ、上限なし、ストレージなし。
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    [SerializeField] private int gold = 0;

    /// <summary>現在の金貨数</summary>
    public int Gold => gold;

    /// <summary>金貨が変化したとき（新しい合計金貨数を渡す）</summary>
    public event System.Action<int> OnGoldChanged;

    // ── Public API ────────────────────────────────────────────────

    /// <summary>金貨を追加する。amount &lt;= 0 の場合は無視。</summary>
    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[PlayerWallet] AddGold called with invalid amount: {amount}");
            return;
        }
        gold += amount;
        Debug.Log($"[PlayerWallet] +{amount} Gold → Total: {gold}");
        OnGoldChanged?.Invoke(gold);
    }

    /// <summary>指定金額を支払えるか確認する（減算しない）。</summary>
    public bool CanSpendGold(int amount)
    {
        return amount >= 0 && gold >= amount;
    }

    /// <summary>金貨を消費する。失敗時は false を返し何もしない。</summary>
    public bool TrySpendGold(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[PlayerWallet] TrySpendGold called with invalid amount: {amount}");
            return false;
        }
        if (gold < amount)
        {
            Debug.LogWarning($"[PlayerWallet] TrySpendGold: not enough gold ({gold} < {amount})");
            return false;
        }
        gold -= amount;
        Debug.Log($"[PlayerWallet] -{amount} Gold → Total: {gold}");
        OnGoldChanged?.Invoke(gold);
        return true;
    }
}
