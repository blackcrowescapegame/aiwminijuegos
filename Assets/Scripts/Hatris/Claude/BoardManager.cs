using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BoardManager — owns the logical grid, detects completed rows,
/// lowers all blocks together, and notifies the ScoreManager.
/// 
/// HOW TO SET UP IN UNITY 6:
///  1. Create an empty GameObject named "BoardManager" in your scene.
///  2. Add this script to it.
///  3. Assign a UI Panel as the "boardRoot" (this is the RectTransform that
///     acts as the parent of every dropped block image).
///  4. Set cols / rows to your desired grid size (default 10 × 20).
///  5. Set cellSize to match your block prefab's RectTransform width/height.
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int cols = 10;
    public int rows = 20;
    public float cellSize = 40f;           // pixels — must match block prefab size

    [Header("References")]
    public RectTransform boardRoot;        // Parent UI panel for all blocks
    public ScoreManager scoreManager;
    public PieceSpawner pieceSpawner;

    // grid[col, row] = the RectTransform of the UI block occupying that cell (null = empty)
    private RectTransform[,] grid;

    // Rows that have already been scored — never re-detected or re-flashed
    private HashSet<int> scoredRows = new HashSet<int>();

    [Header("Line Clear Events")]
    [Tooltip("Fired when exactly 1 line is cleared")]
    public UnityEngine.Events.UnityEvent onSingleClear;
    [Tooltip("Fired when exactly 2 lines are cleared at once")]
    public UnityEngine.Events.UnityEvent onDoubleClear;
    [Tooltip("Fired when exactly 3 lines are cleared at once")]
    public UnityEngine.Events.UnityEvent onTripleClear;
    [Tooltip("Fired when 4 or more lines are cleared at once")]
    public UnityEngine.Events.UnityEvent onTetrisClear;

    // --- Singleton-lite: other scripts grab the instance ---
    public static BoardManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        grid = new RectTransform[cols, rows];

        // Force boardRoot to bottom-left pivot so GridToAnchoredPos always
        // matches visually — row 0 = bottom of the panel, regardless of
        // how the RectTransform was configured in the Inspector.
        if (boardRoot != null)
        {
            // Set exact board size
            boardRoot.sizeDelta = new Vector2(cols * cellSize, rows * cellSize);

            // Use bottom-left pivot so anchoredPosition(0,0) = bottom-left corner
            boardRoot.pivot = new Vector2(0f, 0f);

            // Anchor to center of parent canvas
            boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            boardRoot.anchorMax = new Vector2(0.5f, 0.5f);

            // Offset so board is centered: move left by half width, down by half height
            boardRoot.anchoredPosition = new Vector2(
                -(cols * cellSize) * 0.5f,
                -(rows * cellSize) * 0.5f
            );

            // Add RectMask2D to clip any blocks that visually stray outside
            if (boardRoot.GetComponent<UnityEngine.UI.RectMask2D>() == null)
                boardRoot.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();


        }
    }

    // ───────────────────────────────────────────────────────────────
    //  Public helpers called by FallingPiece
    // ───────────────────────────────────────────────────────────────

    /// <summary>Returns true if (col, row) is inside the board and empty.</summary>
    public bool IsCellFree(int col, int row)
    {
        if (col < 0 || col >= cols || row < 0 || row >= rows) return false;
        return grid[col, row] == null;
    }

    /// <summary>
    /// Called when a piece locks in place.
    /// Registers each block cell, then checks for completed rows.
    /// </summary>
    public void LockCells(List<(int col, int row, RectTransform rt)> cells)
    {
        foreach (var (col, row, rt) in cells)
        {
            if (col >= 0 && col < cols && row >= 0 && row < rows)
            {
                grid[col, row] = rt;
                // blocks are already re-parented to boardRoot by FallingPiece.Lock()
            }
        }

        StartCoroutine(CheckAndClearRows());
    }

    // ───────────────────────────────────────────────────────────────
    //  Row clearing + lowering
    // ───────────────────────────────────────────────────────────────

    private IEnumerator CheckAndClearRows()
    {
        List<int> completedRows = new List<int>();

        for (int row = 0; row < rows; row++)
        {
            if (!scoredRows.Contains(row) && IsRowComplete(row))
                completedRows.Add(row);
        }

        if (completedRows.Count == 0)
        {
            pieceSpawner.SpawnNext();
            yield break;
        }

        // Flash the completed rows as visual feedback, then score them.
        // Blocks are never moved or destroyed — they stay exactly where they are.
        foreach (int row in completedRows)
            scoredRows.Add(row);

        yield return StartCoroutine(FlashRows(completedRows));

        scoreManager.AddClearedRows(completedRows.Count);

        // Fire the matching line-clear event
        switch (completedRows.Count)
        {
            case 1: onSingleClear?.Invoke(); break;
            case 2: onDoubleClear?.Invoke(); break;
            case 3: onTripleClear?.Invoke(); break;
            default: onTetrisClear?.Invoke(); break; // 4+
        }

        pieceSpawner.SpawnNext();
    }

    private bool IsRowComplete(int row)
    {
        for (int col = 0; col < cols; col++)
            if (grid[col, row] == null) return false;
        return true;
    }

    /// <summary>
    /// Flashes completed rows white 3 times as visual feedback, then
    /// tints them gold permanently so the player can see which lines scored.
    /// </summary>
    private IEnumerator FlashRows(List<int> completedRows)
    {
        // Store each block's original color before we flash
        var originalColors = new Dictionary<RectTransform, Color>();
        foreach (int row in completedRows)
            for (int col = 0; col < cols; col++)
                if (grid[col, row] != null)
                {
                    var img = grid[col, row].GetComponent<Image>();
                    if (img != null) originalColors[grid[col, row]] = img.color;
                }

        // Flash white 3 times
        for (int i = 0; i < 3; i++)
        {
            foreach (int row in completedRows)
                SetRowColor(row, Color.white);
            yield return new WaitForSeconds(0.08f);

            // Restore originals between flashes
            foreach (var kvp in originalColors)
            {
                var img = kvp.Key.GetComponent<Image>();
                if (img != null) img.color = kvp.Value;
            }
            yield return new WaitForSeconds(0.08f);
        }

        // Permanently tint completed rows gold so they are visually distinct
        foreach (int row in completedRows)
            SetRowColor(row, new Color(1f, 0.85f, 0.1f));
    }

    private void SetRowColor(int row, Color color)
    {
        for (int col = 0; col < cols; col++)
        {
            if (grid[col, row] == null) continue;
            var img = grid[col, row].GetComponent<Image>();
            if (img != null) img.color = color;
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  Coordinate helpers
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts grid (col, row) to an anchoredPosition inside boardRoot.
    /// Row 0 = bottom, row (rows-1) = top.
    /// The board pivot should be at bottom-left (0, 0).
    /// </summary>
    public Vector2 GridToAnchoredPos(int col, int row)
    {
        return new Vector2(col * cellSize, row * cellSize);
    }

    /// <summary>Snaps a world/anchored position to the nearest grid cell.</summary>
    public (int col, int row) AnchoredPosToGrid(Vector2 pos)
    {
        int col = Mathf.RoundToInt(pos.x / cellSize);
        int row = Mathf.RoundToInt(pos.y / cellSize);
        return (col, row);
    }

    /// <summary>True if game over (top row has any block).</summary>
    public bool IsTopRowOccupied()
    {
        for (int col = 0; col < cols; col++)
            if (grid[col, rows - 1] != null) return true;
        return false;
    }
}
