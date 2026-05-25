using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// タイトルバーに付けると、親ウィンドウをドラッグで移動できる。
/// PointerDown 時にウィンドウを最前面に移動する。
/// </summary>
public class DraggableUIWindow : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
{
    [SerializeField] private RectTransform windowRect;

    private Vector2 _dragOffset;

    private void Awake()
    {
        if (windowRect == null && transform.parent != null)
            windowRect = transform.parent.GetComponent<RectTransform>();
    }

    // 押した瞬間に置顶
    public void OnPointerDown(PointerEventData eventData)
    {
        if (windowRect != null)
            windowRect.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (windowRect == null) return;
        if (windowRect.parent is RectTransform parentRT)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, eventData.position, eventData.pressEventCamera, out var local);
            _dragOffset = windowRect.anchoredPosition - local;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (windowRect == null) return;
        if (windowRect.parent is RectTransform parentRT)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, eventData.position, eventData.pressEventCamera, out var local);
            windowRect.anchoredPosition = local + _dragOffset;
        }
    }
}
