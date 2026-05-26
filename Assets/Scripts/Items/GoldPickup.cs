using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 地面に落ちた金貨の拾取物。
/// プレイヤーが範囲内に入り E キーを押すと PlayerWallet に金貨を追加して消滅する。
/// PickupItem と同じ OnTriggerEnter / Exit + eKey パターンを採用。
/// </summary>
public class GoldPickup : MonoBehaviour
{
    [SerializeField] private int    amount     = 1;
    [SerializeField] private string playerTag  = "Player";

    /// <summary>この金貨掉落物の金額</summary>
    public int Amount => amount;

    /// <summary>EnemyDropper などが実行時に金額を注入するメソッド</summary>
    public void SetAmount(int value)
    {
        amount = Mathf.Max(1, value);
    }

    // ── 内部状態 ─────────────────────────────────────────────────
    private bool         _playerInRange;
    private PlayerWallet _playerWallet;

    // ── Lifecycle ────────────────────────────────────────────────
    private void Update()
    {
        if (!_playerInRange) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current.eKey.wasPressedThisFrame) return;

        if (_playerWallet == null)
        {
            Debug.LogWarning("[GoldPickup] PlayerWallet not found on player. Gold not collected.");
            return;
        }

        _playerWallet.AddGold(amount);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        _playerInRange = true;
        _playerWallet  = other.GetComponent<PlayerWallet>();
        if (_playerWallet == null)
            Debug.LogWarning("[GoldPickup] Player entered range but PlayerWallet not found.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        _playerInRange = false;
        _playerWallet  = null;
    }
}
