using UnityEngine;
using UnityEngine.EventSystems;

public class BallDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private float swipeThreshold = 30f;
    [SerializeField] private float dragStartThreshold = 6f;

    private RectTransform rectTransform;
    private BallItem ballItem;
    private Vector2 beginPointerPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ballItem = GetComponent<BallItem>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        beginPointerPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!TryGetBallItem())
        {
            return;
        }

        Vector2 delta = eventData.position - beginPointerPosition;
        if (!TryGetSwipeDirection(delta, dragStartThreshold, out Vector2Int direction, out float distance))
        {
            ballItem.Board.ResetDragPreview();
            return;
        }

        ballItem.Board.PreviewSwapByDirection(ballItem, direction, distance);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - beginPointerPosition;

        if (!TryGetBallItem())
        {
            rectTransform.anchoredPosition = Vector2.zero;
            return;
        }

        if (TryGetSwipeDirection(delta, swipeThreshold, out Vector2Int direction, out _))
        {
            if (ballItem.Board.TrySwapByDirection(ballItem, direction))
            {
                return;
            }
        }

        ballItem.Board.ResetDragPreview();
        rectTransform.anchoredPosition = Vector2.zero;
    }

    bool TryGetBallItem()
    {
        if (ballItem == null)
        {
            ballItem = GetComponent<BallItem>();
        }

        return ballItem != null && ballItem.Board != null;
    }

    bool TryGetSwipeDirection(Vector2 delta, float threshold, out Vector2Int direction, out float distance)
    {
        direction = Vector2Int.zero;
        distance = 0f;

        if (delta.magnitude < threshold)
        {
            return false;
        }

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            direction = delta.x > 0f ? Vector2Int.right : Vector2Int.left;
            distance = Mathf.Abs(delta.x);
            return true;
        }

        direction = delta.y > 0f ? Vector2Int.up : Vector2Int.down;
        distance = Mathf.Abs(delta.y);
        return true;
    }
}
