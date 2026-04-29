using UnityEngine;
using TMPro;

/// <summary>
/// ScoreManager — tracks score, lines cleared, and level.
/// Updates the UI labels and speeds up the spawner as levels increase.
///
/// HOW TO SET UP:
///  1. Add this to your GameManager GameObject.
///  2. Assign TMP labels for scoreLabel, linesLabel, levelLabel.
///  3. Assign pieceSpawner so speed can increase with level.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("UI References (TextMeshPro)")]
    public TMP_Text scoreLabel;
    public TMP_Text linesLabel;
    public TMP_Text levelLabel;

    [Header("References")]
    public PieceSpawner pieceSpawner;
    public GameController gameController;

    [Header("Scoring")]
    [Tooltip("Base points per line cleared (multiplied by lines × lines for combos)")]
    public int basePointsPerLine = 100;
    [Tooltip("How many lines to clear before leveling up")]
    public int linesPerLevel = 10;
    [Tooltip("How much the fall interval decreases per level (seconds)")]
    public float speedIncreasePerLevel = 0.04f;

    private int score;
    private int totalLines;
    private int level = 1;

    void Start() => UpdateUI();

    /// <summary>
    /// Called by BoardManager after rows are cleared.
    /// Scoring: 1 line = 100, 2 = 400, 3 = 900, 4 = 1600 (quadratic combo bonus).
    /// </summary>
    public void AddClearedRows(int count)
    {
        int points = basePointsPerLine * count * count * level;
        score += points;
        totalLines += count;

        int newLevel = (totalLines / linesPerLevel) + 1;
        if (newLevel != level)
        {
            level = newLevel;
            float newInterval = Mathf.Max(0.05f, 0.6f - (level - 1) * speedIncreasePerLevel);
            pieceSpawner?.SetFallInterval(newInterval);
        }

        UpdateUI();

        // Floating score popup (optional — fires a Unity event you can hook into)
        OnScoreAdded?.Invoke(points);

        // Check win condition after every line clear
        gameController?.CheckWinCondition(totalLines);
    }

    public void ResetScore()
    {
        score = 0;
        totalLines = 0;
        level = 1;
        pieceSpawner?.SetFallInterval(0.6f);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreLabel != null) scoreLabel.text = score.ToString("N0");
        if (linesLabel != null) linesLabel.text = totalLines.ToString();
        if (levelLabel != null) levelLabel.text = level.ToString();
    }

    // Optional event for score popups / effects
    public System.Action<int> OnScoreAdded;

    public int Score => score;
    public int Lines => totalLines;
    public int Level => level;
}
