using UnityEngine;
using UnityEngine.EventSystems;

// Tự được GameManager gắn vào object Grid, dùng để bắt cú bấm của công cụ Bom.
public class GridBombInput : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector] public GameManager gm;

    public void OnPointerClick(PointerEventData e)
    {
        if (gm != null) gm.OnGridClick(e.position, e.pressEventCamera);
    }
}
