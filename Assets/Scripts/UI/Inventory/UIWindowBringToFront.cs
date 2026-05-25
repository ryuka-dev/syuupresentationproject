using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ウィンドウ背景に付けると、クリック時にそのウィンドウを最前面に移動する。
/// windowRoot 未指定時は自分自身の RectTransform を使用。
/// Button / 子要素の onClick は妨げない。
/// </summary>
public class UIWindowBringToFront : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private RectTransform windowRoot;

    private void Awake()
    {
        if (windowRoot == null)
            windowRoot = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (windowRoot != null)
            windowRoot.SetAsLastSibling();
    }
}
