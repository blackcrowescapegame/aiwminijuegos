using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events; // Required for UnityEvents

public class EquivalenceMinigame : MonoBehaviour
{
    // 1. Define our States using Enums
    public enum Shape { Circle, Triangle, Square }
    public enum ColorState { Red, Green, Blue }
    public enum Orientation { Deg0, Deg90, Deg180, Deg270 }

    [Header("Player State")]
    public Shape playerShape = Shape.Circle;
    public ColorState playerColor = ColorState.Red;
    public Orientation playerOrientation = Orientation.Deg0;

    [Header("Target State (The Goal)")]
    public Shape targetShape;
    public ColorState targetColor;
    public Orientation targetOrientation;

    [Header("UI References")]
    public Image playerImage;
    public Image targetImage;

    [Header("Assets")]
    public Sprite[] shapeSprites;

    // Arrays defining the actual values for the Enums
    private Color[] colorValues = { Color.red, Color.green, Color.blue };
    private float[] rotationValues = { 0f, 90f, 180f, 270f };

    [Header("Events")]
    public UnityEvent onShapeMatched; // Triggers specifically when the shape aligns
    public UnityEvent onGameWon;      // Triggers when EVERYTHING aligns

    void Start()
    {
        // Use the new Reset method to set up the game initially
        ResetGame();
    }

    // --- GAME CONTROLS ---

    public void ResetGame()
    {
        // 1. Reset player state to defaults
        playerShape = Shape.Circle;
        playerColor = ColorState.Red;
        playerOrientation = Orientation.Deg0;

        // 2. Generate a new random target
        GenerateRandomTarget();

        // 3. Update the visual for the player
        UpdateVisuals(playerImage, playerShape, playerColor, playerOrientation);

        Debug.Log("Game Reset!");
    }

    // --- SHAPE ---

    public void CycleShapeForward()
    {
        Shape oldShape = playerShape; // Remember the old shape to check for a new match
        playerShape = (Shape)(((int)playerShape + 1) % 3);

        UpdateVisuals(playerImage, playerShape, playerColor, playerOrientation);
        CheckShapeMatchEvent(oldShape);
        CheckWinCondition();
    }

    public void CycleShapeBackward()
    {
        Shape oldShape = playerShape;
        playerShape = (Shape)(((int)playerShape - 1 + 3) % 3);

        UpdateVisuals(playerImage, playerShape, playerColor, playerOrientation);
        CheckShapeMatchEvent(oldShape);
        CheckWinCondition();
    }

    // --- COLOR ---

    public void CycleColorForward()
    {
        playerColor = (ColorState)(((int)playerColor + 1) % 3);
        UpdateVisuals(playerImage, playerShape, playerColor, playerOrientation);
        CheckWinCondition();
    }

    public void CycleColorBackward()
    {
        playerColor = (ColorState)(((int)playerColor - 1 + 3) % 3);
        UpdateVisuals(playerImage, playerShape, playerColor, playerOrientation);
        CheckWinCondition();
    }

    // --- ORIENTATION ---

    public void CycleOrientationPlus90()
    {
        playerOrientation = (Orientation)(((int)playerOrientation + 1) % 4);
        UpdateVisuals(playerImage, playerShape, playerColor, playerOrientation);
        CheckWinCondition();
    }

    public void CycleOrientationMinus90()
    {
        playerOrientation = (Orientation)(((int)playerOrientation - 1 + 4) % 4);
        UpdateVisuals(playerImage, playerShape, playerColor, playerOrientation);
        CheckWinCondition();
    }

    // --- VISUALS & LOGIC ---

    private void UpdateVisuals(Image img, Shape shape, ColorState color, Orientation rot)
    {
        img.sprite = shapeSprites[(int)shape];
        img.color = colorValues[(int)color];
        img.rectTransform.localRotation = Quaternion.Euler(0, 0, rotationValues[(int)rot]);
    }

    private void GenerateRandomTarget()
    {
        targetShape = (Shape)Random.Range(0, 3);
        targetColor = (ColorState)Random.Range(0, 3);
        targetOrientation = (Orientation)Random.Range(0, 4);

        UpdateVisuals(targetImage, targetShape, targetColor, targetOrientation);
    }

    private void CheckShapeMatchEvent(Shape previousShape)
    {
        // Only trigger the event if it just BECAME a match (prevents spamming the event)
        if (playerShape == targetShape && previousShape != targetShape)
        {
            onShapeMatched?.Invoke();
        }
    }

    private void CheckWinCondition()
    {
        if (playerShape == targetShape &&
            playerColor == targetColor &&
            playerOrientation == targetOrientation)
        {
            Debug.Log("MATCH FOUND! You win!");
            onGameWon?.Invoke();
        }
    }
}