using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GhostPiece — shows a translucent "shadow" of where the current piece will land.
///
/// HOW TO SET UP:
///  1. For each piece prefab, optionally create a matching ghost version
///     (same block layout, but with Image alpha ~30%, no FallingPiece script).
///  2. Add this script to a persistent GameObject (e.g. GameManager).
///  3. Assign ghostPrefabs matching your pieceList order, OR leave null to
///     auto-generate the ghost at runtime.
///
/// Alternatively: this script auto-generates a ghost by duplicating the active
/// piece and setting all Image colors to semi-transparent. This requires no
/// extra prefabs.
/// </summary>
public class GhostPiece : MonoBehaviour
{
    [Header("References")]
    public BoardManager board;
    public RectTransform boardRoot;

    [Range(0f, 1f)]
    public float ghostAlpha = 0.28f;

    private GameObject currentGhost;
    private FallingPiece trackedPiece;

    void Update()
    {
        // Find the active falling piece
        var piece = FindFirstObjectByType<FallingPiece>();
        if (piece == null)
        {
            DestroyGhost();
            return;
        }

        // Rebuild ghost if piece changed
        if (piece != trackedPiece)
        {
            DestroyGhost();
            trackedPiece = piece;
            BuildGhost(piece);
        }

        UpdateGhostPosition(piece);
    }

    private void BuildGhost(FallingPiece source)
    {
        currentGhost = Instantiate(source.gameObject, boardRoot);

        // Remove the FallingPiece logic so it doesn't interfere
        var fp = currentGhost.GetComponent<FallingPiece>();
        if (fp != null) Destroy(fp);

        // Make it semi-transparent
        foreach (var img in currentGhost.GetComponentsInChildren<Image>())
        {
            var c = img.color;
            c.a = ghostAlpha;
            img.color = c;
        }
    }

    private void UpdateGhostPosition(FallingPiece source)
    {
        if (currentGhost == null) return;

        // Read the source blocks' positions to mirror offsets
        var sourceBlocks = source.GetComponentsInChildren<PieceBlock>();
        var ghostBlocks   = currentGhost.GetComponentsInChildren<PieceBlock>();

        // Find drop distance — capped at board height to prevent infinite loop
        int dropRows = 0;
        for (int d = 1; d <= board.rows; d++)
        {
            bool canDrop = true;
            foreach (var b in sourceBlocks)
            {
                var rt = b.GetComponent<RectTransform>();
                var (col, row) = board.AnchoredPosToGrid(rt.anchoredPosition);
                row -= d;
                if (!board.IsCellFree(col, row)) { canDrop = false; break; }
            }
            if (!canDrop) break;
            dropRows = d;
        }

        // Position ghost blocks
        for (int i = 0; i < sourceBlocks.Length && i < ghostBlocks.Length; i++)
        {
            var srcRt   = sourceBlocks[i].GetComponent<RectTransform>();
            var ghostRt = ghostBlocks[i].GetComponent<RectTransform>();
            var (col, row) = board.AnchoredPosToGrid(srcRt.anchoredPosition);
            ghostRt.anchoredPosition = board.GridToAnchoredPos(col, row - dropRows);
        }
    }

    private void DestroyGhost()
    {
        if (currentGhost != null) Destroy(currentGhost);
        currentGhost = null;
        trackedPiece = null;
    }
}
