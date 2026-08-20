using UnityEngine;

public enum TileColorType{
    Red,
    Blue,
    Green,
    Yellow
}

public class Tile : MonoBehaviour
{
    public TileColorType colorType;
    public int gridX;
    public int gridY;
    public SpriteRenderer _spriteRenderer;
    private readonly Color[] _colorList={
        new Color(0.86f,0.27f,0.27f), //Red #DC4646
        new Color(0.27f,0.51f,0.86f), //Blue #4682DC
        new Color(0.31f,0.71f,0.35f), //Green #50B45A
        new Color(1f,0.82f,0.27f)    //Yellow #FFD246
    };
    // Start is called before the first frame update
    void Awake(){
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetColor(TileColorType type){
        colorType = type;
        int idx = (int)type;
        _spriteRenderer.color= _colorList[idx];
    }
}
