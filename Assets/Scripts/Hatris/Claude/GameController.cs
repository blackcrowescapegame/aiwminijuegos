using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// GameController — manages game states (Playing, Paused, GameOver, Won) and
/// win-condition tracking based on a configurable lines-to-win goal.
///
/// HOW TO SET UP:
///  1. Assign gameOverPanel and winPanel (hidden by default).
///  2. Set linesToWin to however many lines clear this scenario.
///  3. Wire onGameOver and onWin UnityEvents to whatever you need
///     (play a sound, show a panel, load a scene, etc).
///  4. In the onWin event you can call SetLinesToWin(int) to configure
///     the NEXT scenario's goal before the scene reloads or continues.
///  5. Call TriggerGameOver() from PieceSpawner (already wired).
///     ScoreManager calls CheckWinCondition() after every line clear.
///
/// SCENARIO DESIGN:
///   - Each "scenario" is just a different linesToWin value.
///   - Chain scenarios by wiring onWin → SetLinesToWin(newValue) + whatever else.
///   - Use PlayerPrefs or a ScriptableObject to persist the goal across scenes.
/// </summary>
public class GameController : MonoBehaviour
{
    // ── State ──────────────────────────────────────────────────────────

    public enum GameState { Playing, Paused, GameOver, Won }
    public GameState State { get; private set; } = GameState.Playing;

    // ── Inspector ──────────────────────────────────────────────────────

    [Header("Win Condition")]
    [Tooltip("How many lines must be cleared to win this scenario. Set to 0 to disable win condition.")]
    public int linesToWin = 20;

    [Tooltip("Label to show current goal progress (optional)")]
    public TMP_Text goalLabel;

    [Header("UI Panels")]
    public GameObject gameOverPanel;
    public GameObject winPanel;
    public GameObject pausePanel;

    [Header("UI Labels — Game Over")]
    public TMP_Text finalScoreLabel;
    public TMP_Text finalLinesLabel;

    [Header("UI Labels — Win")]
    public TMP_Text winScoreLabel;
    public TMP_Text winLinesLabel;

    [Header("References")]
    public ScoreManager scoreManager;

    [Header("Events")]
    [Tooltip("Fired when the top row is occupied and no new piece can spawn.")]
    public UnityEvent onGameOver;

    [Tooltip("Fired when the player clears the required number of lines.")]
    public UnityEvent onWin;

    // ── Unity lifecycle ────────────────────────────────────────────────

    void Start()
    {
        gameOverPanel?.SetActive(false);
        winPanel?.SetActive(false);
        pausePanel?.SetActive(false);
        UpdateGoalLabel();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            TogglePause();
    }

    // ── Win condition ──────────────────────────────────────────────────

    /// <summary>
    /// Called by ScoreManager after every line clear.
    /// Checks whether the player has reached the lines-to-win goal.
    /// </summary>
    public void CheckWinCondition(int totalLinesCleared)
    {
        if (State != GameState.Playing) return;
        if (linesToWin <= 0) return; // win condition disabled

        UpdateGoalLabel();

        if (totalLinesCleared >= linesToWin)
            TriggerWin();
    }

    /// <summary>
    /// Change the lines-to-win goal at runtime.
    /// Wire this to the onWin UnityEvent to chain scenarios:
    ///   onWin → SetLinesToWin(40) → LoadNextScene / Continue
    /// </summary>
    public void SetLinesToWin(int newGoal)
    {
        linesToWin = newGoal;
        UpdateGoalLabel();
    }

    // ── Trigger methods ────────────────────────────────────────────────

    /// <summary>Called by PieceSpawner when a new piece cannot be placed.</summary>
    public void TriggerGameOver()
    {
        if (State == GameState.Won) return; // win takes priority
        State = GameState.GameOver;
        Time.timeScale = 0f;

        if (finalScoreLabel != null) finalScoreLabel.text = scoreManager.Score.ToString("N0");
        if (finalLinesLabel != null) finalLinesLabel.text = scoreManager.Lines.ToString();

        gameOverPanel?.SetActive(true);
        onGameOver?.Invoke();
    }

    private void TriggerWin()
    {
        State = GameState.Won;
        Time.timeScale = 0f;

        if (winScoreLabel != null) winScoreLabel.text = scoreManager.Score.ToString("N0");
        if (winLinesLabel != null) winLinesLabel.text = scoreManager.Lines.ToString();

        winPanel?.SetActive(true);
        onWin?.Invoke();
    }

    // ── Button / event callbacks ───────────────────────────────────────

    public void Restart()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void TogglePause()
    {
        if (State == GameState.GameOver || State == GameState.Won) return;

        if (State == GameState.Playing)
        {
            State = GameState.Paused;
            Time.timeScale = 0f;
            pausePanel?.SetActive(true);
        }
        else
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
            pausePanel?.SetActive(false);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private void UpdateGoalLabel()
    {
        if (goalLabel == null || linesToWin <= 0) return;
        int remaining = Mathf.Max(0, linesToWin - (scoreManager != null ? scoreManager.Lines : 0));
        goalLabel.text = $"{remaining} lines to go";
    }
}
