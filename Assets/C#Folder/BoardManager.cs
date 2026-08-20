using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BoardManager : MonoBehaviour
{
    [Header("棋盘参数")]
    public int width = 8;
    public int height = 8;
    public float cellSize = 1.1f;
    public Tile tilePrefab;

    private Tile[,] _grid;
    private Tile _selectedTile;
    private bool _isProcessing = false;

    void Start()
    {
        _grid = new Tile[width, height];
        GenerateBoard();
    }

    void Update()
    {
        if (_isProcessing) return;
        //鼠标点击检测（URP推荐2D射线，替代OnMouseDown）
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero);
            if (hit.collider != null)
            {
                Tile clickTile = hit.collider.GetComponent<Tile>();
                if (clickTile != null)
                {
                    OnTileClick(clickTile);
                }
            }
        }
    }

    void GenerateBoard()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SpawnTile(x, y);
            }
        }
    }

    void SpawnTile(int x, int y)
    {
        Vector2 worldPos = GetWorldPos(x, y);
        Tile tile = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
        tile.gridX = x;
        tile.gridY = y;

        TileColorType randomColor;
        do
        {
            randomColor = (TileColorType)Random.Range(0, 4);
            tile.SetColor(randomColor);
        } while (CheckHasMatch(x, y, tile));

        _grid[x, y] = tile;
    }

    Vector2 GetWorldPos(int x, int y)
    {
        //棋盘居中，整体偏移，8*8棋盘居中
        float offsetX = -(width - 1) * cellSize / 2f;
        float offsetY = -(height - 1) * cellSize / 2f;
        return new Vector2(offsetX + x * cellSize, offsetY + y * cellSize);
    }

    //生成时检查当前方块会不会直接形成三消，避免开局自带消除
    bool CheckHasMatch(int x, int y, Tile tile)
    {
        //水平
        if (x >= 2)
        {
            Tile t1 = _grid[x - 1, y];
            Tile t2 = _grid[x - 2, y];
            if (t1 != null && t2 != null)
            {
                if (t1.colorType == tile.colorType && t2.colorType == tile.colorType)
                    return true;
            }
        }
        //垂直
        if (y >= 2)
        {
            Tile t1 = _grid[x, y - 1];
            Tile t2 = _grid[x, y - 2];
            if (t1 != null && t2 != null)
            {
                if (t1.colorType == tile.colorType && t2.colorType == tile.colorType)
                    return true;
            }
        }
        return false;
    }

    void OnTileClick(Tile tile)
    {
        if (_selectedTile == null)
        {
            _selectedTile = tile;
        }
        else
        {
            //判断是否相邻
            bool isNeighbor = (Mathf.Abs(_selectedTile.gridX - tile.gridX) == 1 && _selectedTile.gridY == tile.gridY)
                           || (Mathf.Abs(_selectedTile.gridY - tile.gridY) == 1 && _selectedTile.gridX == tile.gridX);

            if (isNeighbor)
            {
                StartCoroutine(SwapAndCheck(_selectedTile, tile));
            }
            _selectedTile = null;
        }
    }

    IEnumerator SwapAndCheck(Tile a, Tile b)
    {
        _isProcessing = true;
        SwapGridPos(a, b);
        yield return MoveTileSmooth(a, b);

        List<Tile> matchList = GetAllMatches();
        if (matchList.Count > 0)
        {
            yield return RemoveMatched(matchList);
            yield return FallTiles();
            yield return FillEmpty();
            //循环连锁消除
            while (GetAllMatches().Count > 0)
            {
                yield return RemoveMatched(GetAllMatches());
                yield return FallTiles();
                yield return FillEmpty();
            }
        }
        else
        {
            //没有匹配，交换回来
            SwapGridPos(a, b);
            yield return MoveTileSmooth(a, b);
        }
        _isProcessing = false;
    }

    void SwapGridPos(Tile a, Tile b)
    {
        //交换网格数组数据
        (_grid[a.gridX, a.gridY], _grid[b.gridX, b.gridY]) = (_grid[b.gridX, b.gridY], _grid[a.gridX, a.gridY]);
        (a.gridX, b.gridX) = (b.gridX, a.gridX);
        (a.gridY, b.gridY) = (b.gridY, a.gridY);
    }

    IEnumerator MoveTileSmooth(Tile a, Tile b, float dur = 0.2f)
    {
        float t = 0;
        Vector2 posA = GetWorldPos(a.gridX, a.gridY);
        Vector2 posB = GetWorldPos(b.gridX, b.gridY);
        Vector2 startA = a.transform.position;
        Vector2 startB = b.transform.position;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            a.transform.position = Vector2.Lerp(startA, posA, p);
            b.transform.position = Vector2.Lerp(startB, posB, p);
            yield return null;
        }
        a.transform.position = posA;
        b.transform.position = posB;
    }

    List<Tile> GetAllMatches()
    {
        HashSet<Tile> set = new HashSet<Tile>();
        //水平检测
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                Tile t0 = _grid[x, y];
                Tile t1 = _grid[x + 1, y];
                Tile t2 = _grid[x + 2, y];
                if (t0 != null && t1 != null && t2 != null)
                {
                    if (t0.colorType == t1.colorType && t1.colorType == t2.colorType)
                    {
                        set.Add(t0); set.Add(t1); set.Add(t2);
                    }
                }
            }
        }
        //垂直检测
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                Tile t0 = _grid[x, y];
                Tile t1 = _grid[x, y + 1];
                Tile t2 = _grid[x, y + 2];
                if (t0 != null && t1 != null && t2 != null)
                {
                    if (t0.colorType == t1.colorType && t1.colorType == t2.colorType)
                    {
                        set.Add(t0); set.Add(t1); set.Add(t2);
                    }
                }
            }
        }
        return new List<Tile>(set);
    }

    IEnumerator RemoveMatched(List<Tile> list)
    {
        foreach (var t in list)
        {
            _grid[t.gridX, t.gridY] = null;
            Destroy(t.gameObject);
        }
        yield return new WaitForSeconds(0.15f);
    }

    IEnumerator FallTiles()
    {
        bool hasFall;
        do
        {
            hasFall = false;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height - 1; y++)
                {
                    if (_grid[x, y] == null && _grid[x, y + 1] != null)
                    {
                        Tile upper = _grid[x, y + 1];
                        _grid[x, y] = upper;
                        _grid[x, y + 1] = null;
                        upper.gridY = y;
                        hasFall = true;
                    }
                }
            }
            yield return new WaitForSeconds(0.03f);
        } while (hasFall);

        //位置平滑归位
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (_grid[x, y] != null)
                {
                    _grid[x, y].transform.position = GetWorldPos(x, y);
                }
            }
        }
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator FillEmpty()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (_grid[x, y] == null)
                {
                    SpawnTile(x, y);
                }
            }
        }
        yield return null;
    }
}