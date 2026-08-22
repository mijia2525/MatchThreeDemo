using UnityEngine;
using UnityEngine.UI;

public class BallItem : MonoBehaviour
{
    public Image ballImage;
    public UIBoardManager Board { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int ColorIndex { get; private set; }

    public void Init(UIBoardManager board, int x, int y, int colorIndex, Color color)
    {
        Board = board;
        SetGridPosition(x, y);
        SetColorIndex(colorIndex, color);
    }

    public void SetGridPosition(int x, int y)
    {
        X = x;
        Y = y;
        name = $"Ball_{x}_{y}";
    }

    public void SetColorIndex(int colorIndex, Color color)
    {
        ColorIndex = colorIndex;
        SetColor(color);
    }

    public void SetColor(Color c)
    {
        if (ballImage != null)
        {
            ballImage.color = c;
        }
    }
}
