using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// B キーでバックパックUIの開閉を制御するコントローラ。
/// Esc でも閉じる。
/// </summary>
public class InventoryInputController : MonoBehaviour
{
    [SerializeField] private InventoryCanvasUI inventoryCanvasUI;

    private void Awake()
    {
        if (inventoryCanvasUI == null)
            inventoryCanvasUI = FindFirstObjectByType<InventoryCanvasUI>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.bKey.wasPressedThisFrame)
        {
            inventoryCanvasUI?.Toggle();
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame && inventoryCanvasUI != null && inventoryCanvasUI.IsOpen)
        {
            inventoryCanvasUI.Close();
        }
    }
}
