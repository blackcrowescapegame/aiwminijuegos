using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PieceSpawner — randomly picks from your list of piece prefabs and spawns
/// the next piece at the top of the board.
///
/// HOW TO SET UP:
///  1. Add this script to a GameObject in your scene (e.g. "GameManager").
///  2. Assign boardRoot (same RectTransform used by BoardManager).
///  3. Populate pieceList with your premade piece prefabs.
///  4. Assign nextPieceDisplay — a small UI panel to show the upcoming piece (optional).
///  5. Call SpawnNext() to start the game, or check autoStartOnPlay.
/// </summary>
public class PieceSpawner : MonoBehaviour
{
    [Header("References")]
    public BoardManager boardManager;
    public RectTransform boardRoot;           // Parent for spawned pieces
    public RectTransform nextPiecePreviewRoot;// Small panel showing next piece (optional)
    public GameController gameController;

    [Header("Piece Prefabs")]
    [Tooltip("Drag all your piece prefabs here. Each must have FallingPiece + PieceBlock children.")]
    public List<GameObject> pieceList = new List<GameObject>();

    [Header("Spawn Settings")]
    public bool autoStartOnPlay = true;
    [Tooltip("Starting fall speed in seconds per row (lower = faster)")]
    public float startFallInterval = 0.6f;

    // The next piece to drop
    private GameObject nextPiecePrefab;
    private GameObject nextPiecePreviewInstance;

    private float currentFallInterval;

    void Start()
    {
        currentFallInterval = startFallInterval;

        if (pieceList == null || pieceList.Count == 0)
        {
            Debug.LogError("[PieceSpawner] No pieces assigned! Drag prefabs into pieceList.");
            return;
        }

        // Pick the first "next" piece
        nextPiecePrefab = GetRandomPiece();
        ShowNextPiecePreview();

        if (autoStartOnPlay)
            SpawnNext();
    }

    /// <summary>Spawns the queued piece and queues the following one.</summary>
    public void SpawnNext()
    {
        if (pieceList == null || pieceList.Count == 0) return;

        // Check game over
        if (boardManager.IsTopRowOccupied())
        {
            gameController?.TriggerGameOver();
            return;
        }

        // Instantiate the queued piece inside the board
        GameObject pieceGO = Instantiate(nextPiecePrefab, boardRoot);
        var rt = pieceGO.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;

        // Initialize the falling piece
        var fp = pieceGO.GetComponent<FallingPiece>();
        if (fp == null)
        {
            Debug.LogError($"[PieceSpawner] Prefab '{nextPiecePrefab.name}' is missing a FallingPiece component!");
            return;
        }

        // Spawn in the middle-top
        int spawnCol = boardManager.cols / 2;
        int spawnRow = boardManager.rows - 2;
        fp.Initialize(spawnCol, spawnRow, currentFallInterval);

        // Queue next
        nextPiecePrefab = GetRandomPiece();
        ShowNextPiecePreview();
    }

    /// <summary>Call this to gradually speed up as the score increases.</summary>
    public void SetFallInterval(float interval)
    {
        currentFallInterval = Mathf.Max(0.05f, interval);
    }

    // ───────────────────────────────────────────────────────────────
    //  Next-piece preview
    // ───────────────────────────────────────────────────────────────

    private void ShowNextPiecePreview()
    {
        if (nextPiecePreviewRoot == null) return;

        if (nextPiecePreviewInstance != null)
            Destroy(nextPiecePreviewInstance);

        nextPiecePreviewInstance = Instantiate(nextPiecePrefab, nextPiecePreviewRoot);

        // Disable the FallingPiece so it doesn't move or interfere
        var fp = nextPiecePreviewInstance.GetComponent<FallingPiece>();
        if (fp != null) fp.enabled = false;

        float cell = boardManager.cellSize;
        var blocks = nextPiecePreviewInstance.GetComponentsInChildren<PieceBlock>();

        // Apply the same pivot/anchor/size that Initialize() uses on each block
        foreach (var b in blocks)
        {
            var brt      = b.GetComponent<RectTransform>();
            brt.pivot    = Vector2.zero;
            brt.anchorMin = brt.anchorMax = Vector2.zero;
            brt.sizeDelta = new Vector2(cell, cell);
            brt.anchoredPosition = new Vector2(b.localCol * cell, b.localRow * cell);
        }

        // Find the bounding box of all blocks in local space
        if (blocks.Length == 0) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var b in blocks)
        {
            float x = b.localCol * cell;
            float y = b.localRow * cell;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x + cell > maxX) maxX = x + cell;
            if (y + cell > maxY) maxY = y + cell;
        }

        float pieceW = maxX - minX;
        float pieceH = maxY - minY;

        // Center the piece root inside the preview panel
        var rootRt       = nextPiecePreviewInstance.GetComponent<RectTransform>();
        rootRt.pivot     = Vector2.zero;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(pieceW, pieceH);
        // Offset so the piece bounding box is centered (account for minX/minY offset)
        rootRt.anchoredPosition = new Vector2(-pieceW * 0.5f - minX, -pieceH * 0.5f - minY);
    }

    private GameObject GetRandomPiece()
    {
        return pieceList[Random.Range(0, pieceList.Count)];
    }
}
