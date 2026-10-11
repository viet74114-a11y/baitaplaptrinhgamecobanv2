using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Lưới")]
    public RectTransform gridRect;      // object Grid
    public GameObject cellPrefab;       // có thể để trống
    public int size = 8;
    public bool clearColumns = true;

    [Header("Khay & kéo thả")]
    public RectTransform[] slots;       // Slot_1..3
    public RectTransform dragLayer;     // có thể để trống
    public Sprite[] sprites;            // các ảnh block
    public float trayScale = 0.6f;
    public float dragOffsetY = 0f;

    [Header("Điểm")]
    public TMP_Text scoreText;
    public TMP_Text gameOverScoreText;

    [Header("Combo")]
    public RectTransform comboPopup;    // ComboPopup
    public Image comboDigit;            // ảnh chữ số trong popup
    public Sprite[] digitSprites;       // 6 ảnh: số 2,3,4,5,6,7 theo thứ tự
    public TMP_Text comboLabel;         // không bắt buộc (có thể để trống)
    public int comboGrace = 2;          // số lượt đặt không xóa mà vẫn giữ combo

    [Header("Gợi ý")]
    public TMP_Text hintCountText;
    public int hintsPerGame = 3;

    [Header("Dụng cụ (Đổi khối & Bom)")]
    public TMP_Text refreshCountText;
    public int refreshesPerGame = 2;
    public TMP_Text bombCountText;
    public int bombsPerGame = 2;
    public Image bombButtonImage;       // ảnh của nút Bom (để đổi màu khi đang bật)

    public UIManager ui;

    public bool IsGameOver { get; private set; }
    public RectTransform DragLayer
    {
        get { return dragLayer != null ? dragLayer : (RectTransform)gridRect.root; }
    }

    static readonly string[][] Patterns =
    {
        new[]{"X"},
        new[]{"XX"}, new[]{"XXX"}, new[]{"XXXX"}, new[]{"XXXXX"},
        new[]{"X","X"}, new[]{"X","X","X"}, new[]{"X","X","X","X"}, new[]{"X","X","X","X","X"},
        new[]{"XX","XX"},
        new[]{"XXX","XXX","XXX"},
        new[]{"X.","XX"}, new[]{".X","XX"}, new[]{"XX","X."}, new[]{"XX",".X"},
        new[]{"X..","X..","XXX"}, new[]{"..X","..X","XXX"}, new[]{"XXX","X..","X.."}, new[]{"XXX","..X","..X"},
        new[]{"XXX",".X."}, new[]{".X.","XXX"}, new[]{"X.","XX","X."}, new[]{".X","XX",".X"},
        new[]{"XX.",".XX"}, new[]{".XX","XX."}
    };

    bool[,] filled;
    Image[,] cellImg;
    readonly List<DraggableShape> tray = new List<DraggableShape>();
    readonly List<Vector2Int> preview = new List<Vector2Int>();
    readonly List<Vector2Int> hintCells = new List<Vector2Int>();
    Coroutine comboCo, hintCo;

    int score, streak, missMoves, hintsLeft, refreshesLeft, bombsLeft;
    bool bombMode;

    float CellSize { get { return gridRect.rect.width / size; } }

    // ---------------- BẮT ĐẦU / KẾT THÚC ----------------
    public void NewGame()
    {
        StopAllCoroutines();
        comboCo = null; hintCo = null;
        IsGameOver = false;
        score = 0; streak = 0; missMoves = 0; hintsLeft = hintsPerGame;
        refreshesLeft = refreshesPerGame; bombsLeft = bombsPerGame; bombMode = false;
        if (comboPopup != null) comboPopup.gameObject.SetActive(false);
        BuildGrid();
        foreach (var s in DragLayer.GetComponentsInChildren<DraggableShape>(true)) Destroy(s.gameObject);
        UpdateScore(); UpdateHint(); UpdateTools();
        SpawnTray();
    }

    void BuildGrid()
    {
        for (int i = gridRect.childCount - 1; i >= 0; i--) Destroy(gridRect.GetChild(i).gameObject);
        var g = gridRect.GetComponent<GridLayoutGroup>();
        if (g != null)
        {
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = size;
            g.cellSize = new Vector2(CellSize, CellSize);
            g.spacing = Vector2.zero;
        }
        if (gridRect.GetComponent<Graphic>() == null)
        {
            var catcher = gridRect.gameObject.AddComponent<Image>();
            catcher.color = new Color(1, 1, 1, 0);
            catcher.raycastTarget = true;
        }
        if (gridRect.GetComponent<GridBombInput>() == null) gridRect.gameObject.AddComponent<GridBombInput>();
        gridRect.GetComponent<GridBombInput>().gm = this;
        filled = new bool[size, size];
        cellImg = new Image[size, size];
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
            {
                GameObject go = cellPrefab != null
                    ? Instantiate(cellPrefab, gridRect)
                    : new GameObject("Cell", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(gridRect, false);
                go.name = "Cell_" + r + "_" + c;
                var img = go.GetComponent<Image>();
                if (img == null) img = go.AddComponent<Image>();
                img.raycastTarget = false;
                cellImg[r, c] = img;
                SetEmpty(r, c);
            }
    }

    void SetEmpty(int r, int c)
    {
        filled[r, c] = false;
        var img = cellImg[r, c];
        img.sprite = null;
        img.color = new Color(1, 1, 1, 0);
    }

    void SetFilled(int r, int c, Sprite sp)
    {
        filled[r, c] = true;
        var img = cellImg[r, c];
        img.sprite = sp;
        img.color = Color.white;
    }

    void Paint(int r, int c, Sprite sp, Color col)
    {
        var img = cellImg[r, c];
        img.sprite = sp;
        img.color = col;
    }

    void Restore(int r, int c)
    {
        if (!filled[r, c]) { SetEmpty(r, c); return; }
    }

    // ---------------- KHAY ----------------
    void SpawnTray()
    {
        tray.Clear();
        foreach (var slot in slots)
        {
            for (int i = slot.childCount - 1; i >= 0; i--) Destroy(slot.GetChild(i).gameObject);
            tray.Add(CreateShape(slot));
        }
        if (!AnyFits()) GameOver();
    }

    DraggableShape CreateShape(RectTransform slot)
    {
        string[] p = Patterns[Random.Range(0, Patterns.Length)];
        int rows = p.Length, cols = 0;
        foreach (var s in p) cols = Mathf.Max(cols, s.Length);
        float cs = CellSize;

        var go = new GameObject("Shape", typeof(RectTransform), typeof(Image), typeof(DraggableShape));
        var rt = (RectTransform)go.transform;
        rt.SetParent(slot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cols * cs, rows * cs);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one * trayScale;
        var root = go.GetComponent<Image>();
        root.color = new Color(1, 1, 1, 0);
        root.raycastTarget = true;

        var ds = go.GetComponent<DraggableShape>();
        ds.gm = this; ds.rt = rt; ds.homeSlot = slot;
        ds.rows = rows; ds.cols = cols;
        ds.cells = new bool[rows, cols];
        ds.sprite = (sprites != null && sprites.Length > 0) ? sprites[Random.Range(0, sprites.Length)] : null;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < p[r].Length; c++)
            {
                if (p[r][c] != 'X') continue;
                ds.cells[r, c] = true;
                var cg = new GameObject("b", typeof(RectTransform), typeof(Image));
                var crt = (RectTransform)cg.transform;
                crt.SetParent(rt, false);
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(cs, cs);
                crt.anchoredPosition = new Vector2((c - (cols - 1) * 0.5f) * cs, ((rows - 1) * 0.5f - r) * cs);
                var im = cg.GetComponent<Image>();
                im.sprite = ds.sprite;
                im.raycastTarget = false;
            }
        return ds;
    }

    // ---------------- VỊ TRÍ TRÊN LƯỚI ----------------
    bool GetOrigin(DraggableShape s, out int r0, out int c0)
    {
        var canvas = gridRect.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, RectTransformUtility.WorldToScreenPoint(cam, s.rt.position), cam, out lp);
        Rect rc = gridRect.rect;
        float cs = rc.width / size;
        float cx = (lp.x - rc.xMin) / cs;
        float cy = (rc.yMax - lp.y) / cs;
        c0 = Mathf.RoundToInt(cx - s.cols * 0.5f);
        r0 = Mathf.RoundToInt(cy - s.rows * 0.5f);
        return true;
    }

    bool Fits(DraggableShape s, int r0, int c0)
    {
        for (int r = 0; r < s.rows; r++)
            for (int c = 0; c < s.cols; c++)
            {
                if (!s.cells[r, c]) continue;
                int rr = r0 + r, cc = c0 + c;
                if (rr < 0 || cc < 0 || rr >= size || cc >= size) return false;
                if (filled[rr, cc]) return false;
            }
        return true;
    }

    bool AnyFits()
    {
        foreach (var s in tray)
        {
            if (s == null) continue;
            for (int r = -0; r < size; r++)
                for (int c = 0; c < size; c++)
                    if (Fits(s, r, c)) return true;
        }
        return false;
    }

    // ---------------- XEM TRƯỚC ----------------
    public void ShowPreview(DraggableShape s)
    {
        ClearPreview();
        int r0, c0;
        GetOrigin(s, out r0, out c0);
        if (!Fits(s, r0, c0)) return;
        for (int r = 0; r < s.rows; r++)
            for (int c = 0; c < s.cols; c++)
                if (s.cells[r, c])
                {
                    Paint(r0 + r, c0 + c, s.sprite, new Color(1, 1, 1, 0.5f));
                    preview.Add(new Vector2Int(r0 + r, c0 + c));
                }
    }

    public void ClearPreview()
    {
        foreach (var v in preview) Restore(v.x, v.y);
        preview.Clear();
    }

    // ---------------- ĐẶT KHỐI ----------------
    public bool TryPlace(DraggableShape s)
    {
        int r0, c0;
        GetOrigin(s, out r0, out c0);
        if (!Fits(s, r0, c0)) return false;
        StopHint();

        int placed = 0;
        for (int r = 0; r < s.rows; r++)
            for (int c = 0; c < s.cols; c++)
                if (s.cells[r, c]) { SetFilled(r0 + r, c0 + c, s.sprite); placed++; }

        tray.Remove(s);
        Destroy(s.gameObject);

        int cleared = ClearLines();
        score += placed;
        if (cleared > 0)
        {
            streak++;
            missMoves = 0;
            int level = streak + cleared - 1;     // mức combo
            score += cleared * size * 10 * Mathf.Max(1, level);
            if (level >= 2) ShowCombo(level);
        }
        else
        {
            missMoves++;
            if (missMoves > comboGrace) streak = 0;
        }
        UpdateScore();

        if (tray.Count == 0) SpawnTray();
        else if (!AnyFits()) GameOver();
        return true;
    }

    int ClearLines()
    {
        var rowsFull = new List<int>();
        var colsFull = new List<int>();
        for (int r = 0; r < size; r++)
        {
            bool ok = true;
            for (int c = 0; c < size; c++) if (!filled[r, c]) { ok = false; break; }
            if (ok) rowsFull.Add(r);
        }
        if (clearColumns)
            for (int c = 0; c < size; c++)
            {
                bool ok = true;
                for (int r = 0; r < size; r++) if (!filled[r, c]) { ok = false; break; }
                if (ok) colsFull.Add(c);
            }
        foreach (int r in rowsFull) for (int c = 0; c < size; c++) SetEmpty(r, c);
        foreach (int c in colsFull) for (int r = 0; r < size; r++) SetEmpty(r, c);
        return rowsFull.Count + colsFull.Count;
    }

    // ---------------- COMBO ----------------
    void ShowCombo(int level)
    {
        if (comboLabel != null) comboLabel.text = "COMBO+" + level;
        if (comboDigit != null && digitSprites != null && digitSprites.Length > 0)
        {
            int idx = Mathf.Clamp(level, 2, 2 + digitSprites.Length - 1) - 2;
            comboDigit.sprite = digitSprites[idx];
        }
        if (comboPopup == null) return;
        if (comboCo != null) StopCoroutine(comboCo);
        comboCo = StartCoroutine(ComboRoutine());
    }

    IEnumerator ComboRoutine()
    {
        comboPopup.gameObject.SetActive(true);
        comboPopup.SetAsLastSibling();
        float t = 0;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            comboPopup.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.2f, t / 0.25f);
            yield return null;
        }
        t = 0;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            comboPopup.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, t / 0.1f);
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);
        comboPopup.gameObject.SetActive(false);
        comboCo = null;
    }

    // ---------------- GỢI Ý ----------------
    // Gắn hàm này vào OnClick của nút Btn_Hint
    public void ShowHint()
    {
        if (IsGameOver || hintsLeft <= 0 || tray.Count == 0) return;
        DraggableShape bestS = null; int bestR = 0, bestC = 0, best = -1;
        foreach (var s in tray)
        {
            if (s == null) continue;
            for (int r = 0; r < size; r++)
                for (int c = 0; c < size; c++)
                {
                    if (!Fits(s, r, c)) continue;
                    int n = CountClears(s, r, c);
                    if (n > best) { best = n; bestS = s; bestR = r; bestC = c; }
                }
        }
        if (bestS == null) return;
        hintsLeft--; UpdateHint();
        StopHint();
        hintCo = StartCoroutine(HintRoutine(bestS, bestR, bestC));
    }

    int CountClears(DraggableShape s, int r0, int c0)
    {
        var tmp = (bool[,])filled.Clone();
        for (int r = 0; r < s.rows; r++)
            for (int c = 0; c < s.cols; c++)
                if (s.cells[r, c]) tmp[r0 + r, c0 + c] = true;
        int n = 0;
        for (int r = 0; r < size; r++)
        {
            bool ok = true;
            for (int c = 0; c < size; c++) if (!tmp[r, c]) { ok = false; break; }
            if (ok) n++;
        }
        if (clearColumns)
            for (int c = 0; c < size; c++)
            {
                bool ok = true;
                for (int r = 0; r < size; r++) if (!tmp[r, c]) { ok = false; break; }
                if (ok) n++;
            }
        return n;
    }

    IEnumerator HintRoutine(DraggableShape s, int r0, int c0)
    {
        hintCells.Clear();
        for (int r = 0; r < s.rows; r++)
            for (int c = 0; c < s.cols; c++)
                if (s.cells[r, c]) hintCells.Add(new Vector2Int(r0 + r, c0 + c));
        float t = 0;
        while (t < 2.5f && s != null)
        {
            t += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
            foreach (var v in hintCells)
                if (!filled[v.x, v.y]) Paint(v.x, v.y, s.sprite, new Color(1f, 1f, 0.4f, 0.35f + 0.4f * pulse));
            if (s.transform.parent == s.homeSlot)
                s.rt.localScale = Vector3.one * trayScale * (1f + 0.12f * pulse);
            yield return null;
        }
        StopHint();
    }

    void StopHint()
    {
        if (hintCo != null) { StopCoroutine(hintCo); hintCo = null; }
        foreach (var v in hintCells) if (cellImg != null && !filled[v.x, v.y]) SetEmpty(v.x, v.y);
        hintCells.Clear();
        foreach (var s in tray) if (s != null && s.transform.parent == s.homeSlot) s.rt.localScale = Vector3.one * trayScale;
    }


    // ---------------- DỤNG CỤ: ĐỔI KHỐI & BOM ----------------
    // Gắn vào OnClick của nút Btn_Refresh
    public void RefreshTray()
    {
        if (IsGameOver || refreshesLeft <= 0) return;
        refreshesLeft--; UpdateTools();
        StopHint(); ClearPreview();
        foreach (var s in tray) if (s != null) Destroy(s.gameObject);
        SpawnTray();
    }

    // Gắn vào OnClick của nút Btn_Bomb: bấm 1 lần để bật, rồi bấm vào một ô trên lưới
    public void ToggleBomb()
    {
        if (IsGameOver) return;
        if (!bombMode && bombsLeft <= 0) return;
        bombMode = !bombMode;
        UpdateTools();
    }

    public void OnGridClick(Vector2 screenPos, Camera cam)
    {
        if (!bombMode || IsGameOver || bombsLeft <= 0) return;
        Vector2 lp;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPos, cam, out lp)) return;
        Rect rc = gridRect.rect;
        float cs = rc.width / size;
        int c0 = Mathf.FloorToInt((lp.x - rc.xMin) / cs);
        int r0 = Mathf.FloorToInt((rc.yMax - lp.y) / cs);
        if (r0 < 0 || c0 < 0 || r0 >= size || c0 >= size) return;
        int removed = 0;
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int r = r0 + dr, c = c0 + dc;
                if (r < 0 || c < 0 || r >= size || c >= size) continue;
                if (filled[r, c]) { SetEmpty(r, c); removed++; }
            }
        bombsLeft--; bombMode = false;
        score += removed * 5;
        UpdateScore(); UpdateTools();
        if (tray.Count > 0 && !AnyFits()) GameOver();
    }

    void UpdateTools()
    {
        if (refreshCountText != null) refreshCountText.text = refreshesLeft.ToString();
        if (bombCountText != null) bombCountText.text = bombsLeft.ToString();
        if (bombButtonImage != null) bombButtonImage.color = bombMode ? new Color(1f, 0.85f, 0.3f) : Color.white;
    }

    // ---------------- ĐIỂM / HẾT GAME ----------------
    void UpdateScore() { if (scoreText != null) scoreText.text = score.ToString(); }
    void UpdateHint() { if (hintCountText != null) hintCountText.text = hintsLeft.ToString(); }

    void GameOver()
    {
        IsGameOver = true;
        if (gameOverScoreText != null) gameOverScoreText.text = score.ToString();
        if (ui != null) ui.ShowGameOver();
    }
}
