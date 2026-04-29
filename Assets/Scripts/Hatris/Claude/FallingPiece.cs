using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FallingPiece — controls movement/rotation of the active piece.
///
/// ARCHITECTURE:
///   The piece root RectTransform is parented to boardRoot and positioned at
///   GridToAnchoredPos(pivotCol, pivotRow). Each PieceBlock child sits at
///   its own local offset (localCol * cellSize, localRow * cellSize) relative
///   to the piece root — so no re-parenting is ever needed during gameplay.
///
///   On Lock(), blocks are re-parented to boardRoot individually (so they stay
///   put after the piece root is destroyed), then the piece root is destroyed.
///
/// CONTROLS:
///   ← / A     Move left
///   → / D     Move right
///   ↓ / S     Soft drop
///   ↑ / W     Rotate clockwise (with wall-kick)
///   Space     Hard drop
/// </summary>
public class FallingPiece : MonoBehaviour
{
    [Header("Fall Speed")]
    public float fallInterval = 0.5f;

    private int pivotCol;
    private int pivotRow;

    private PieceBlock[] blocks;
    private BoardManager board;
    private RectTransform rt;

    private float fallTimer;
    private bool locked = false;

    // ── Init ──────────────────────────────────────────────────────────

    void Awake()
    {
        blocks = GetComponentsInChildren<PieceBlock>();
        board  = BoardManager.Instance;
        rt     = GetComponent<RectTransform>();

        if (blocks.Length == 0)
            Debug.LogError($"[FallingPiece] '{gameObject.name}' has no PieceBlock components on its children! Add a PieceBlock component to each child block GameObject.");
    }

    /// <summary>
    /// Called by PieceSpawner after Instantiate.
    /// Sets the piece's grid position and fall speed.
    /// Piece root is already a child of boardRoot — we just move it.
    /// </summary>
    public void Initialize(int spawnCol, int spawnRow, float interval)
    {
        pivotCol     = spawnCol;
        pivotRow     = spawnRow;
        fallInterval = interval;

        // Ensure each child block has the right size and pivot
        float cell = board.cellSize;
        foreach (var b in blocks)
        {
            var brt      = b.GetComponent<RectTransform>();
            brt.pivot    = Vector2.zero;
            brt.anchorMin = brt.anchorMax = Vector2.zero;
            brt.sizeDelta = new Vector2(cell, cell);
            // local position = offset in grid cells
            brt.anchoredPosition = new Vector2(b.localCol * cell, b.localRow * cell);
        }

        // Set piece root pivot to bottom-left and move it to spawn position
        rt.pivot     = Vector2.zero;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = new Vector2(board.cellSize, board.cellSize);
        rt.anchoredPosition = board.GridToAnchoredPos(pivotCol, pivotRow);


    }

    // ── Update ────────────────────────────────────────────────────────

    [Header("Soft Drop")]
    [Tooltip("How fast the piece falls while Down is held (seconds per row)")]
    public float softDropInterval = 0.05f;

    private float softDropTimer;
    private bool softDropping;

    [Header("Audio")]
    [Tooltip("Clip played when the piece rotates")]
    public AudioClip rotateClip;
    [Tooltip("Clip played when the piece locks in place")]
    public AudioClip placeClip;
    [Range(0f, 1f)] public float audioVolume = 1f;

    [Header("Lock Feedback")]
    [Tooltip("How much blocks scale up on the punch (1.0 = no punch, 1.2 = 20% bigger)")]
    public float punchScale = 1.25f;
    [Tooltip("Duration of the scale punch in seconds")]
    public float punchDuration = 0.08f;

    void Update()
    {
        if (locked) return;

        HandleInput();

        // Normal auto-fall (skipped while soft-dropping — soft drop drives movement instead)
        if (!softDropping)
        {
            fallTimer += Time.deltaTime;
            if (fallTimer >= fallInterval)
            {
                fallTimer = 0f;
                TryMove(0, -1);
            }
        }
    }

    private void HandleInput()
    {
        // One-shot inputs
        if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) TryMove(-1,  0);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) TryMove( 1,  0);
        if (Input.GetKeyDown(KeyCode.UpArrow)    || Input.GetKeyDown(KeyCode.W)) TryRotate();
        if (Input.GetKeyDown(KeyCode.Space))                                      HardDrop();

        // Soft drop — held input drives its own faster timer
        bool downHeld = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        softDropping = downHeld;

        if (downHeld)
        {
            softDropTimer += Time.deltaTime;
            if (softDropTimer >= softDropInterval)
            {
                softDropTimer = 0f;
                TryMove(0, -1);
            }
        }
        else
        {
            softDropTimer = 0f;
        }
    }

    // ── Movement ──────────────────────────────────────────────────────

    private bool TryMove(int dc, int dr)
    {
        if (CanPlace(pivotCol + dc, pivotRow + dr, GetOffsets()))
        {
            pivotCol += dc;
            pivotRow += dr;
            rt.anchoredPosition = board.GridToAnchoredPos(pivotCol, pivotRow);
            return true;
        }

        if (dr == -1 && dc == 0)
            Lock();

        return false;
    }

    private void TryRotate()
    {
        var rotated = RotateCW(GetOffsets());

        int kickCol = pivotCol;
        if      (CanPlace(pivotCol,     pivotRow, rotated)) { /* ok */ }
        else if (CanPlace(pivotCol + 1, pivotRow, rotated)) { kickCol = pivotCol + 1; }
        else if (CanPlace(pivotCol - 1, pivotRow, rotated)) { kickCol = pivotCol - 1; }
        else return; // fully blocked

        pivotCol = kickCol;
        ApplyOffsets(rotated);
        UpdateBlockLocalPositions();
        rt.anchoredPosition = board.GridToAnchoredPos(pivotCol, pivotRow);

        PlaySound(rotateClip);
    }

    private void HardDrop()
    {
        int limit = board.rows;
        while (!locked && limit-- > 0)
            if (!TryMove(0, -1)) break;
    }

    // ── Locking ───────────────────────────────────────────────────────

    private void Lock()
    {
        if (locked) return;
        locked = true;
        StartCoroutine(LockRoutine());
    }

    private IEnumerator LockRoutine()
    {
        PlaySound(placeClip);

        // Scale punch: quickly scale blocks up then snap back to 1
        if (punchDuration > 0f && punchScale > 1f)
        {
            float elapsed = 0f;
            float half = punchDuration * 0.5f;

            // Scale up
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                float s = Mathf.Lerp(1f, punchScale, t);
                SetBlocksScale(s);
                yield return null;
            }
            // Scale back down
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                float s = Mathf.Lerp(punchScale, 1f, t);
                SetBlocksScale(s);
                yield return null;
            }
            SetBlocksScale(1f);
        }

        // Re-parent and register blocks
        var cells = new List<(int col, int row, RectTransform brt)>();
        foreach (var b in blocks)
        {
            int col = pivotCol + b.localCol;
            int row = pivotRow + b.localRow;
            var brt = b.GetComponent<RectTransform>();
            brt.SetParent(board.boardRoot, false);
            brt.anchoredPosition = board.GridToAnchoredPos(col, row);
            cells.Add((col, row, brt));
        }

        board.LockCells(cells);
        Destroy(gameObject);
    }

    private void SetBlocksScale(float s)
    {
        foreach (var b in blocks)
            b.GetComponent<RectTransform>().localScale = new Vector3(s, s, 1f);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        // Use PlayClipAtPoint so sound plays even after the GO is destroyed
        AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, audioVolume);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void UpdateBlockLocalPositions()
    {
        float cell = board.cellSize;
        foreach (var b in blocks)
            b.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(b.localCol * cell, b.localRow * cell);
    }

    private (int dc, int dr)[] GetOffsets()
    {
        var o = new (int, int)[blocks.Length];
        for (int i = 0; i < blocks.Length; i++)
            o[i] = (blocks[i].localCol, blocks[i].localRow);
        return o;
    }

    private void ApplyOffsets((int dc, int dr)[] offsets)
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i].localCol = offsets[i].dc;
            blocks[i].localRow = offsets[i].dr;
        }
    }

    private bool CanPlace(int col, int row, (int dc, int dr)[] offsets)
    {
        foreach (var (dc, dr) in offsets)
            if (!board.IsCellFree(col + dc, row + dr)) return false;
        return true;
    }

    private static (int dc, int dr)[] RotateCW((int dc, int dr)[] offsets)
    {
        var r = new (int, int)[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
            r[i] = (offsets[i].dr, -offsets[i].dc);
        return r;
    }
}
