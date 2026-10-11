using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Khối kéo thả trong khay. Được GameManager tạo ra, không cần gắn tay.
public class DraggableShape : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public GameManager gm;
    [HideInInspector] public bool[,] cells;      // [hàng, cột]
    [HideInInspector] public int rows, cols;
    [HideInInspector] public Sprite sprite;
    [HideInInspector] public RectTransform homeSlot;
    [HideInInspector] public RectTransform rt;

    bool dragging;
    Vector2 target;

    void Awake() { rt = (RectTransform)transform; }

    Vector2 Target(PointerEventData e)
    {
        return e.position + new Vector2(0f, gm.dragOffsetY);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (gm == null || gm.IsGameOver) return;
        dragging = true;
        rt.SetParent(gm.DragLayer, true);
        rt.SetAsLastSibling();
        target = Target(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        target = Target(e);
    }

    void Update()
    {
        if (!dragging) return;
        float k = 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime);
        rt.position = Vector3.Lerp(rt.position, new Vector3(target.x, target.y, rt.position.z), k);
        rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one, k);
        gm.ShowPreview(this);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;
        dragging = false;
        rt.position = new Vector3(target.x, target.y, rt.position.z);
        gm.ClearPreview();
        if (!gm.TryPlace(this)) ReturnHome();
    }

    public void ReturnHome()
    {
        rt.SetParent(homeSlot, false);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one * gm.trayScale;
    }
}
