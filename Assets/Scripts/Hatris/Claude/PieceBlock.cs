using UnityEngine;

/// <summary>
/// PieceBlock — a tiny data component placed on every child block inside a piece prefab.
/// 
/// localCol / localRow define this block's offset from the piece's pivot cell.
/// 
/// Example — T-Piece (pivot = center):
///
///    [ ][ ][ ]      localCol: -1, 0, 1   (row 0)
///       [X]         localCol:  0          (row 1)  ← pivot
///
///   Block offsets: (-1,0), (0,0), (1,0), (0,1)
///
/// Example — L-Piece (pivot = bottom of vertical bar):
///
///    [ ]            localCol: 0 (row 2)
///    [X]            localCol: 0 (row 1)  ← pivot
///    [ ][ ]         localCol: 0, 1 (row 0)
///
///   Block offsets: (0,0), (0,1), (0,2), (1,0)
///
/// You can design ANY shape just by setting these offsets.
/// The FallingPiece script reads them at runtime; they're also used for rotation.
/// </summary>
public class PieceBlock : MonoBehaviour
{
    [Tooltip("Column offset from pivot (positive = right)")]
    public int localCol = 0;

    [Tooltip("Row offset from pivot (positive = up)")]
    public int localRow = 0;
}
