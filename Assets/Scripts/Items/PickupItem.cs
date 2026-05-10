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

        Debug.Log($"获得：{itemData.ItemName}");
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInRange = false;
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
