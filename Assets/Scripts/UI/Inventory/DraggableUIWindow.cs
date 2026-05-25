
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// タイトルバーに付けると、親ウィンドウをドラッグで移動できる。
/// windowRect 未指定時は親の RectTransform を自動使用。
/// </summary>
public class DraggableUIWindow : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] private RectTransform windowRect;

    private Vector2 _dragOffset;

    private void Awake()
    {
        if (windowRect == null && transform.parent != null)
            windowRect = transform.parent.GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (windowRect == null) return;
        var parentRT = windowRect.parent as RectTransform;
        if (parentRT == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT, eventData.position, eventData.pressEventCamera, out var local);
        _dragOffset = windowRect.anchoredPosition - local;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (windowRect == null) return;
        var parentRT = windowRect.parent as RectTransform;
        if (parentRT == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT, eventData.position, eventData.pressEventCamera, out var local);
        windowRect.anchoredPosition = local + _dragOffset;
    }
}
