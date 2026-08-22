using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIBoardManager : MonoBehaviour
{
    [System.Serializable]
    public class LevelConfig
    {
        public string id = "1-1";
        public int width = 5;
        public int height = 5;
        public int colorCount = 4;
        public int targetColorIndex = 1;
        public int targetCount = 3;
    }

    [System.Serializable]
    public class LevelCompleteEvent : UnityEvent<string>
    {
    }

    struct MovingBall
    {
        public BallItem ball;
        public Transform targetCell;
        public Vector3 startPosition;
        public Vector3 targetPosition;
    }

    [Header("References")]
    public GridLayoutGroup gridContainer;
    public GameObject cellPrefab;
    public GameObject ballPrefab;
    public Text scoreText;
    public Text targetText;
    public Text levelText;
    public GameObject upperLevelPanel;
    public LevelCompleteEvent onLevelComplete;
    [SerializeField] private bool createHudIfMissing = true;
    [SerializeField] private bool createLevelSelectIfMissing = true;

    [Header("Board")]
    [SerializeField] private int width = 5;
    [SerializeField] private int height = 5;
    [SerializeField] private Vector2 cellSize = new Vector2(70f, 70f);
    [SerializeField] private Vector2 spacing = new Vector2(2f, 2f);
    [SerializeField] private Vector2 ballSize = new Vector2(58f, 58f);

    [Header("Animation")]
    [SerializeField] private float swapDuration = 0.18f;
    [SerializeField] private float dropDuration = 0.22f;
    [SerializeField] private float refillDropOffset = 140f;
    [SerializeField] private float completeReturnDelay = 0.8f;

    [Header("Levels")]
    [SerializeField] private bool useBuiltInLevelCatalog = true;
    [SerializeField] private bool loadTestLevelOnStart = true;
    [SerializeField] private string testLevelId = "1-1";
    [SerializeField] private int currentLevelIndex;
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>
    {
        new LevelConfig { id = "1-1", width = 5, height = 5, colorCount = 4, targetColorIndex = 1, targetCount = 3 },
        new LevelConfig { id = "1-2", width = 5, height = 5, colorCount = 4, targetColorIndex = 2, targetCount = 6 },
        new LevelConfig { id = "1-3", width = 6, height = 5, colorCount = 4, targetColorIndex = 0, targetCount = 8 },
        new LevelConfig { id = "2-1", width = 6, height = 6, colorCount = 5, targetColorIndex = 3, targetCount = 10 },
        new LevelConfig { id = "2-2", width = 7, height = 6, colorCount = 5, targetColorIndex = 4, targetCount = 14 },
        new LevelConfig { id = "3-1", width = 8, height = 8, colorCount = 6, targetColorIndex = 5, targetCount = 18 }
    };

    private GameObject[,] cellArray;
    private BallItem[,] ballArray;
    private Canvas rootCanvas;
    private Font hudFont;
    private GameObject generatedLevelSelectPanel;
    private bool isResolving;
    private bool levelComplete;
    private int targetCollected;
    private BallItem previewBall;
    private BallItem previewOther;

    private readonly Color[] colorTable = new Color[]
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        Color.magenta,
        Color.cyan
    };

    private readonly string[] colorNames = new string[]
    {
        "Red",
        "Green",
        "Blue",
        "Yellow",
        "Magenta",
        "Cyan"
    };

    public int CurrentLevel => currentLevelIndex + 1;
    public int TargetCollected => targetCollected;
    public int TargetCount => CurrentLevelConfig.targetCount;

    LevelConfig CurrentLevelConfig => levels[Mathf.Clamp(currentLevelIndex, 0, levels.Count - 1)];

    void Start()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        hudFont = GetRuntimeFont();
        if (useBuiltInLevelCatalog)
        {
            levels = CreateBuiltInLevels();
        }

        ConfigureGrid();
        EnsureHud();
        EnsureGeneratedLevelSelect();
        ClearExistingBoard();

        if (loadTestLevelOnStart)
        {
            LoadLevel(testLevelId);
        }
        else
        {
            gridContainer.gameObject.SetActive(false);
            if (upperLevelPanel != null)
            {
                upperLevelPanel.SetActive(true);
            }
            else if (generatedLevelSelectPanel != null)
            {
                generatedLevelSelectPanel.SetActive(true);
            }

            RefreshHud();
        }
    }

    public bool TrySwapByDirection(BallItem ball, Vector2Int direction)
    {
        if (isResolving || levelComplete || ball == null)
        {
            ResetDragPreview();
            return false;
        }

        int targetX = ball.X + direction.x;
        int targetY = ball.Y - direction.y;
        if (!IsInsideBoard(targetX, targetY))
        {
            ResetDragPreview();
            return false;
        }

        BallItem other = ballArray[targetX, targetY];
        if (other == null || !AreAdjacent(ball, other))
        {
            ResetDragPreview();
            return false;
        }

        StartCoroutine(TrySwapRoutine(ball, other));
        return true;
    }

    public void PreviewSwapByDirection(BallItem ball, Vector2Int direction, float distance)
    {
        if (isResolving || levelComplete || ball == null)
        {
            return;
        }

        int targetX = ball.X + direction.x;
        int targetY = ball.Y - direction.y;
        if (!IsInsideBoard(targetX, targetY))
        {
            ResetDragPreview();
            return;
        }

        BallItem other = ballArray[targetX, targetY];
        if (other == null)
        {
            ResetDragPreview();
            return;
        }

        if (previewBall != ball || previewOther != other)
        {
            ResetDragPreview();
            previewBall = ball;
            previewOther = other;
        }

        float maxDistance = GetStepDistance(direction) * 0.9f;
        Vector2 offset = new Vector2(direction.x, direction.y) * Mathf.Clamp(distance, 0f, maxDistance);
        ball.GetComponent<RectTransform>().anchoredPosition = offset;
        other.GetComponent<RectTransform>().anchoredPosition = -offset;
    }

    public void ResetDragPreview()
    {
        ResetPreviewBall(previewBall);
        ResetPreviewBall(previewOther);
        previewBall = null;
        previewOther = null;
    }

    void ResetPreviewBall(BallItem ball)
    {
        if (ball != null)
        {
            ball.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        }
    }

    public bool LoadLevel(string levelId)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].id == levelId)
            {
                LoadLevel(i);
                return true;
            }
        }

        Debug.LogWarning($"Level id not found: {levelId}");
        return false;
    }

    public void LoadLevelById(string levelId)
    {
        LoadLevel(levelId);
    }

    public void LoadLevel(int levelIndex)
    {
        currentLevelIndex = Mathf.Clamp(levelIndex, 0, levels.Count - 1);
        ApplyCurrentLevel();
        ConfigureGrid();
        targetCollected = 0;
        levelComplete = false;
        isResolving = false;

        if (upperLevelPanel != null)
        {
            upperLevelPanel.SetActive(false);
        }

        if (generatedLevelSelectPanel != null)
        {
            generatedLevelSelectPanel.SetActive(false);
        }

        gridContainer.gameObject.SetActive(true);
        CreateBoard();
        RefreshHud();
    }

    [ContextMenu("Reload Test Level")]
    public void ReloadTestLevel()
    {
        LoadLevel(testLevelId);
    }

    IEnumerator TrySwapRoutine(BallItem first, BallItem second)
    {
        isResolving = true;
        previewBall = null;
        previewOther = null;

        Transform firstStartCell = cellArray[first.X, first.Y].transform;
        Transform secondStartCell = cellArray[second.X, second.Y].transform;

        SwapBoardPositions(first, second);
        HashSet<BallItem> matches = FindMatches();

        if (matches.Count == 0)
        {
            yield return AnimateSwapToCells(first, secondStartCell, second, firstStartCell);
            SwapBoardPositions(first, second);
            yield return AnimateSwapToCells(first, firstStartCell, second, secondStartCell);
            MoveBallToCell(first, first.X, first.Y);
            MoveBallToCell(second, second.X, second.Y);
            isResolving = false;
            yield break;
        }

        yield return AnimateSwapToCells(first, secondStartCell, second, firstStartCell);
        MoveBallToCell(first, first.X, first.Y);
        MoveBallToCell(second, second.X, second.Y);
        yield return ResolveMatchesRoutine(matches);
    }

    void ApplyCurrentLevel()
    {
        if (levels.Count == 0)
        {
            width = 5;
            height = 5;
            return;
        }

        LevelConfig level = CurrentLevelConfig;
        width = Mathf.Max(3, level.width);
        height = Mathf.Max(3, level.height);
        level.colorCount = Mathf.Clamp(level.colorCount, 3, colorTable.Length);
        level.targetColorIndex = Mathf.Clamp(level.targetColorIndex, 0, level.colorCount - 1);
        level.targetCount = Mathf.Max(1, level.targetCount);
    }

    void ConfigureGrid()
    {
        ContentSizeFitter fitter = gridContainer.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.enabled = false;
        }

        gridContainer.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridContainer.constraintCount = width;
        gridContainer.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridContainer.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridContainer.childAlignment = TextAnchor.MiddleCenter;
        gridContainer.cellSize = cellSize;
        gridContainer.spacing = spacing;

        RectTransform gridRt = gridContainer.GetComponent<RectTransform>();
        gridRt.anchorMin = new Vector2(0.5f, 0.5f);
        gridRt.anchorMax = new Vector2(0.5f, 0.5f);
        gridRt.pivot = new Vector2(0.5f, 0.5f);
        gridRt.anchoredPosition = Vector2.zero;
        gridRt.sizeDelta = GetBoardSize();
    }

    void CreateBoard()
    {
        ClearExistingBoard();

        cellArray = new GameObject[width, height];
        ballArray = new BallItem[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject cellObj = Instantiate(cellPrefab, gridContainer.transform);
                cellObj.name = $"Cell_{x}_{y}";
                cellArray[x, y] = cellObj;

                int colorIndex = GetRandomColorIndexForInitialBoard(x, y);
                CreateBall(x, y, colorIndex);
            }
        }
    }

    void ClearExistingBoard()
    {
        for (int i = gridContainer.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = gridContainer.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    BallItem CreateBall(int x, int y, int colorIndex)
    {
        GameObject ballObj = Instantiate(ballPrefab, cellArray[x, y].transform);
        BallItem ball = ballObj.GetComponent<BallItem>();
        if (ball == null)
        {
            ball = ballObj.AddComponent<BallItem>();
        }

        RectTransform ballRt = ballObj.GetComponent<RectTransform>();
        ballRt.anchorMin = new Vector2(0.5f, 0.5f);
        ballRt.anchorMax = new Vector2(0.5f, 0.5f);
        ballRt.pivot = new Vector2(0.5f, 0.5f);
        ballRt.anchoredPosition = Vector2.zero;
        ballRt.sizeDelta = ballSize;

        ball.ballImage = ballObj.GetComponent<Image>();
        ball.Init(this, x, y, colorIndex, colorTable[colorIndex]);
        ballArray[x, y] = ball;
        return ball;
    }

    int GetRandomColorIndexForInitialBoard(int x, int y)
    {
        List<int> candidates = new List<int>();
        int activeColorCount = GetActiveColorCount();
        for (int i = 0; i < activeColorCount; i++)
        {
            candidates.Add(i);
        }

        if (x >= 2 && ballArray[x - 1, y].ColorIndex == ballArray[x - 2, y].ColorIndex)
        {
            candidates.Remove(ballArray[x - 1, y].ColorIndex);
        }

        if (y >= 2 && ballArray[x, y - 1].ColorIndex == ballArray[x, y - 2].ColorIndex)
        {
            candidates.Remove(ballArray[x, y - 1].ColorIndex);
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    void SwapBoardPositions(BallItem first, BallItem second)
    {
        int firstX = first.X;
        int firstY = first.Y;
        int secondX = second.X;
        int secondY = second.Y;

        ballArray[firstX, firstY] = second;
        ballArray[secondX, secondY] = first;

        first.SetGridPosition(secondX, secondY);
        second.SetGridPosition(firstX, firstY);
    }

    IEnumerator AnimateSwapToCells(BallItem first, Transform firstTargetCell, BallItem second, Transform secondTargetCell)
    {
        RectTransform firstRt = first.GetComponent<RectTransform>();
        RectTransform secondRt = second.GetComponent<RectTransform>();

        Transform animationLayer = rootCanvas != null ? rootCanvas.transform : gridContainer.transform;
        firstRt.SetParent(animationLayer, true);
        secondRt.SetParent(animationLayer, true);
        firstRt.SetAsLastSibling();
        secondRt.SetAsLastSibling();

        Vector3 firstStart = firstRt.position;
        Vector3 secondStart = secondRt.position;
        Vector3 firstTarget = firstTargetCell.position;
        Vector3 secondTarget = secondTargetCell.position;
        float elapsed = 0f;

        while (elapsed < swapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / swapDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            firstRt.position = Vector3.Lerp(firstStart, firstTarget, easedT);
            secondRt.position = Vector3.Lerp(secondStart, secondTarget, easedT);
            yield return null;
        }

        firstRt.position = firstTarget;
        secondRt.position = secondTarget;
    }

    IEnumerator ResolveMatchesRoutine(HashSet<BallItem> matches)
    {
        while (matches.Count > 0)
        {
            CollectTargetBalls(matches);
            RefreshHud();
            RemoveMatchedBalls(matches);
            yield return new WaitForSeconds(0.06f);
            yield return DropBallsRoutine();
            yield return FillEmptyCellsRoutine();
            matches = FindMatches();
        }

        isResolving = false;
        CheckLevelComplete();
    }

    void CollectTargetBalls(HashSet<BallItem> matches)
    {
        int targetColorIndex = CurrentLevelConfig.targetColorIndex;
        foreach (BallItem ball in matches)
        {
            if (ball != null && ball.ColorIndex == targetColorIndex)
            {
                targetCollected++;
            }
        }
    }

    void RemoveMatchedBalls(HashSet<BallItem> matches)
    {
        foreach (BallItem ball in matches)
        {
            if (ball == null)
            {
                continue;
            }

            ballArray[ball.X, ball.Y] = null;
            Destroy(ball.gameObject);
        }
    }

    IEnumerator DropBallsRoutine()
    {
        List<MovingBall> movingBalls = new List<MovingBall>();
        Transform animationLayer = rootCanvas != null ? rootCanvas.transform : gridContainer.transform;

        for (int x = 0; x < width; x++)
        {
            int writeY = height - 1;
            for (int readY = height - 1; readY >= 0; readY--)
            {
                BallItem ball = ballArray[x, readY];
                if (ball == null)
                {
                    continue;
                }

                ballArray[x, readY] = null;
                ballArray[x, writeY] = ball;

                if (writeY != readY)
                {
                    RectTransform ballRt = ball.GetComponent<RectTransform>();
                    Vector3 startPosition = ballRt.position;
                    Transform targetCell = cellArray[x, writeY].transform;
                    ballRt.SetParent(animationLayer, true);
                    ball.SetGridPosition(x, writeY);

                    movingBalls.Add(new MovingBall
                    {
                        ball = ball,
                        targetCell = targetCell,
                        startPosition = startPosition,
                        targetPosition = targetCell.position
                    });
                }
                else
                {
                    ball.SetGridPosition(x, writeY);
                }

                writeY--;
            }
        }

        yield return AnimateMovingBalls(movingBalls, dropDuration);

        foreach (MovingBall move in movingBalls)
        {
            MoveBallToCell(move.ball, move.ball.X, move.ball.Y);
        }
    }

    IEnumerator FillEmptyCellsRoutine()
    {
        List<MovingBall> movingBalls = new List<MovingBall>();
        Transform animationLayer = rootCanvas != null ? rootCanvas.transform : gridContainer.transform;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (ballArray[x, y] != null)
                {
                    continue;
                }

                int colorIndex = Random.Range(0, GetActiveColorCount());
                BallItem ball = CreateBall(x, y, colorIndex);
                RectTransform ballRt = ball.GetComponent<RectTransform>();
                Vector3 targetPosition = cellArray[x, y].transform.position;
                ballRt.SetParent(animationLayer, true);
                ballRt.position = targetPosition + Vector3.up * refillDropOffset;

                movingBalls.Add(new MovingBall
                {
                    ball = ball,
                    targetCell = cellArray[x, y].transform,
                    startPosition = ballRt.position,
                    targetPosition = targetPosition
                });
            }
        }

        yield return AnimateMovingBalls(movingBalls, dropDuration);

        foreach (MovingBall move in movingBalls)
        {
            MoveBallToCell(move.ball, move.ball.X, move.ball.Y);
        }
    }

    IEnumerator AnimateMovingBalls(List<MovingBall> movingBalls, float duration)
    {
        if (movingBalls.Count == 0)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            foreach (MovingBall move in movingBalls)
            {
                if (move.ball != null)
                {
                    move.ball.GetComponent<RectTransform>().position =
                        Vector3.Lerp(move.startPosition, move.targetPosition, easedT);
                }
            }

            yield return null;
        }

        foreach (MovingBall move in movingBalls)
        {
            if (move.ball != null)
            {
                move.ball.GetComponent<RectTransform>().position = move.targetPosition;
            }
        }
    }

    void MoveBallToCell(BallItem ball, int x, int y)
    {
        ball.transform.SetParent(cellArray[x, y].transform, false);
        ball.SetGridPosition(x, y);

        RectTransform ballRt = ball.GetComponent<RectTransform>();
        ballRt.anchoredPosition = Vector2.zero;
    }

    HashSet<BallItem> FindMatches()
    {
        HashSet<BallItem> matches = new HashSet<BallItem>();

        for (int y = 0; y < height; y++)
        {
            int runStart = 0;
            for (int x = 1; x <= width; x++)
            {
                if (x < width && HasSameColor(x, y, x - 1, y))
                {
                    continue;
                }

                int runLength = x - runStart;
                if (runLength >= 3)
                {
                    for (int matchX = runStart; matchX < x; matchX++)
                    {
                        matches.Add(ballArray[matchX, y]);
                    }
                }

                runStart = x;
            }
        }

        for (int x = 0; x < width; x++)
        {
            int runStart = 0;
            for (int y = 1; y <= height; y++)
            {
                if (y < height && HasSameColor(x, y, x, y - 1))
                {
                    continue;
                }

                int runLength = y - runStart;
                if (runLength >= 3)
                {
                    for (int matchY = runStart; matchY < y; matchY++)
                    {
                        matches.Add(ballArray[x, matchY]);
                    }
                }

                runStart = y;
            }
        }

        matches.Remove(null);
        return matches;
    }

    bool HasSameColor(int firstX, int firstY, int secondX, int secondY)
    {
        BallItem first = ballArray[firstX, firstY];
        BallItem second = ballArray[secondX, secondY];
        return first != null && second != null && first.ColorIndex == second.ColorIndex;
    }

    bool AreAdjacent(BallItem first, BallItem second)
    {
        int distance = Mathf.Abs(first.X - second.X) + Mathf.Abs(first.Y - second.Y);
        return distance == 1;
    }

    bool IsInsideBoard(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    int GetActiveColorCount()
    {
        return Mathf.Clamp(CurrentLevelConfig.colorCount, 3, colorTable.Length);
    }

    Vector2 GetBoardSize()
    {
        float boardWidth = width * cellSize.x + (width - 1) * spacing.x;
        float boardHeight = height * cellSize.y + (height - 1) * spacing.y;
        return new Vector2(boardWidth, boardHeight);
    }

    float GetStepDistance(Vector2Int direction)
    {
        return direction.x != 0 ? cellSize.x + spacing.x : cellSize.y + spacing.y;
    }

    void CheckLevelComplete()
    {
        if (levelComplete || targetCollected < CurrentLevelConfig.targetCount)
        {
            return;
        }

        levelComplete = true;
        RefreshHud();
        StartCoroutine(ReturnToUpperLevelAfterDelay());
    }

    IEnumerator ReturnToUpperLevelAfterDelay()
    {
        yield return new WaitForSeconds(completeReturnDelay);
        ReturnToUpperLevel();
    }

    public void ReturnToUpperLevel()
    {
        ResetDragPreview();
        isResolving = false;
        gridContainer.gameObject.SetActive(false);

        if (upperLevelPanel != null)
        {
            upperLevelPanel.SetActive(true);
        }
        else if (generatedLevelSelectPanel != null)
        {
            generatedLevelSelectPanel.SetActive(true);
        }

        onLevelComplete?.Invoke(CurrentLevelConfig.id);
    }

    void RefreshHud()
    {
        LevelConfig level = CurrentLevelConfig;

        if (levelText != null)
        {
            levelText.text = levelComplete ? $"Level {level.id} Complete" : $"Level {level.id}";
        }

        if (scoreText != null)
        {
            scoreText.text = $"{GetColorName(level.targetColorIndex)}: {targetCollected}/{level.targetCount}";
        }

        if (targetText != null)
        {
            targetText.text = $"Target: collect {level.targetCount} {GetColorName(level.targetColorIndex)}";
        }
    }

    void EnsureHud()
    {
        if (!createHudIfMissing || rootCanvas == null)
        {
            return;
        }

        if (levelText == null)
        {
            levelText = CreateHudText("LevelText", new Vector2(24f, -24f), new Vector2(300f, 30f));
        }

        if (scoreText == null)
        {
            scoreText = CreateHudText("ScoreText", new Vector2(24f, -58f), new Vector2(300f, 30f));
        }

        if (targetText == null)
        {
            targetText = CreateHudText("TargetText", new Vector2(24f, -92f), new Vector2(420f, 30f));
        }
    }

    void EnsureGeneratedLevelSelect()
    {
        if (!createLevelSelectIfMissing || rootCanvas == null || upperLevelPanel != null || generatedLevelSelectPanel != null)
        {
            return;
        }

        generatedLevelSelectPanel = new GameObject("GeneratedLevelSelectPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        generatedLevelSelectPanel.transform.SetParent(rootCanvas.transform, false);

        RectTransform panelRt = generatedLevelSelectPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(460f, 560f);

        Image panelImage = generatedLevelSelectPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        Text title = CreateText("Title", generatedLevelSelectPanel.transform, "Choose Level", 30, TextAnchor.MiddleCenter);
        RectTransform titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -22f);
        titleRt.sizeDelta = new Vector2(360f, 42f);

        for (int i = 0; i < levels.Count; i++)
        {
            CreateGeneratedLevelButton(i, generatedLevelSelectPanel.transform);
        }

        generatedLevelSelectPanel.SetActive(false);
    }

    void CreateGeneratedLevelButton(int index, Transform parent)
    {
        GameObject buttonObj = new GameObject($"LevelButton_{levels[index].id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        RectTransform rt = buttonObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        int column = index % 3;
        int row = index / 3;
        rt.anchoredPosition = new Vector2(-140f + column * 140f, -86f - row * 92f);
        rt.sizeDelta = new Vector2(120f, 68f);

        Image image = buttonObj.GetComponent<Image>();
        image.color = new Color(0.18f, 0.2f, 0.24f, 0.95f);

        int capturedIndex = index;
        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => LoadLevel(capturedIndex));

        LevelConfig level = levels[index];
        Text label = CreateText("Label", buttonObj.transform, $"{level.id}\n{GetColorName(level.targetColorIndex)} x{level.targetCount}", 18, TextAnchor.MiddleCenter);
        RectTransform labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
    }

    List<LevelConfig> CreateBuiltInLevels()
    {
        return new List<LevelConfig>
        {
            new LevelConfig { id = "1-1", width = 5, height = 5, colorCount = 4, targetColorIndex = 1, targetCount = 3 },
            new LevelConfig { id = "1-2", width = 5, height = 5, colorCount = 4, targetColorIndex = 1, targetCount = 6 },
            new LevelConfig { id = "1-3", width = 5, height = 6, colorCount = 4, targetColorIndex = 2, targetCount = 8 },
            new LevelConfig { id = "1-4", width = 6, height = 6, colorCount = 4, targetColorIndex = 0, targetCount = 10 },

            new LevelConfig { id = "2-1", width = 6, height = 6, colorCount = 5, targetColorIndex = 3, targetCount = 10 },
            new LevelConfig { id = "2-2", width = 6, height = 6, colorCount = 5, targetColorIndex = 4, targetCount = 12 },
            new LevelConfig { id = "2-3", width = 7, height = 6, colorCount = 5, targetColorIndex = 2, targetCount = 14 },
            new LevelConfig { id = "2-4", width = 7, height = 7, colorCount = 5, targetColorIndex = 0, targetCount = 16 },

            new LevelConfig { id = "3-1", width = 7, height = 7, colorCount = 6, targetColorIndex = 5, targetCount = 16 },
            new LevelConfig { id = "3-2", width = 8, height = 7, colorCount = 6, targetColorIndex = 1, targetCount = 18 },
            new LevelConfig { id = "3-3", width = 8, height = 8, colorCount = 6, targetColorIndex = 4, targetCount = 20 },
            new LevelConfig { id = "3-4", width = 8, height = 8, colorCount = 6, targetColorIndex = 3, targetCount = 24 }
        };
    }

    Text CreateHudText(string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        Text text = CreateText(objectName, rootCanvas.transform, string.Empty, 24, TextAnchor.MiddleLeft);
        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        text.raycastTarget = false;
        return text;
    }

    Text CreateText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment)
    {
        GameObject textObj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObj.transform.SetParent(parent, false);

        Text text = textObj.GetComponent<Text>();
        text.font = hudFont;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.text = value;
        return text;
    }

    Font GetRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    string GetColorName(int colorIndex)
    {
        int index = Mathf.Clamp(colorIndex, 0, colorNames.Length - 1);
        return colorNames[index];
    }
}
