using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 场景中可拾取的地面物品。
/// 玩家进入触发范围后，按 E 键拾取。
/// </summary>
public class PickupItem : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private string playerTag = "Player";

    private bool _playerInRange;
    private PlayerInventory _playerInventory;


private void Update()
    {
        if (!_playerInRange) return;

        if (Keyboard.current == null) return;

        if (!Keyboard.current.eKey.wasPressedThisFrame) return;

        if (itemData == null)
        {
            Debug.LogWarning("[PickupItem] itemData is not assigned.");
            return;
        }

        if (_playerInventory == null)
        {
            Debug.LogWarning("[PickupItem] PlayerInventory not found on player.");
            return;
        }

        bool added = _playerInventory.AddItem(itemData);
        if (!added)
        {
            Debug.LogWarning($"[PickupItem] 背包已满，无法拾取 {itemData.ItemName}。");
            return;   // 地上物品を残す
        }
        Destroy(gameObject);
    }

private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInRange = true;
            _playerInventory = other.GetComponent<PlayerInventory>();
        }
    }

private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInRange = false;
            _playerInventory = null;
        }
    }


    /// <summary>
    /// EnemyDropper などが実行時に ItemData を注入するためのメソッド。
    /// </summary>
    public void SetItemData(ItemData data)
    {
        itemData = data;
    }
}
