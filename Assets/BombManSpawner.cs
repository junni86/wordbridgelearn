using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

// BombMan 미니게임 — 9×11 격자 배치 + 벽돌/글자/캐릭터 시작 위치 결정
// 그리드 데이터(어디에 무엇이 있는지)는 BombManMiniGame이 단일 보유.
// 본 클래스는 시각 오브젝트 생성과 셀 좌표 ↔ 월드 좌표 변환 책임만 가짐.
public class BombManSpawner : MonoBehaviour
{
    [Header("프리팹")]
    public GameObject brickPrefab;   // 벽돌 (없으면 폴백으로 단색 사각 생성)
    public GameObject letterPrefab;  // 글자 물방울 (AlphabetPrefab.prefab 재사용)
    public SpriteRenderer background; // 가로/세로 범위 기준 (없으면 ScreenWallFitter)

    [Header("그리드 설정")]
    [SerializeField] int cols = 9;
    [SerializeField] int rows = 11;
    [SerializeField, Range(0.1f, 1f)] float bubbleWidthRatio = 0.92f;
    [SerializeField, Range(0f, 1f)] float bubbleGapRatio = 0.08f;
    [SerializeField] float fontSize = 76f;
    [SerializeField] TMP_FontAsset koreanBubbleFont;

    [Header("내용물 개수")]
    [Tooltip("그리드 위에 배치할 글자 물방울 개수 (정답 글자 포함)")]
    [SerializeField] int letterCount = 18;

    [Header("그리드 위치 미세조정")]
    [Tooltip("그리드 전체를 위로/아래로 미세 이동 (양수 = 위로)")]
    [SerializeField] float gridYOffset = 0f;

    public int Cols => cols;
    public int Rows => rows;
    public int LetterCount => letterCount;
    public float BubbleWidthRatio => bubbleWidthRatio;
    public float BubbleGapRatio => bubbleGapRatio;

    Sprite[] bubbleSprites;
    Sprite brickFallbackSprite;

    // 시각 오브젝트 보관 — 단어 종료 시 일괄 정리
    readonly List<GameObject> spawnedObjects = new();

    // 현재 적용된 셀 크기 (BombManMiniGame/캐릭터에서 참조)
    public float CurrentCellSize { get; private set; } = -1f;

    // 마지막으로 격자를 배치한 화면 크기 — Update에서 변경 감지 시 RelayoutAll
    float lastAppliedFullW = -1f;
    float lastAppliedFullH = -1f;

    // 현재 그리드 상태 참조 (RelayoutAll에서 셀별 위치 재계산용) — BombManMiniGame에서 갱신
    int[,] cellTypeRef;
    BombManBrick[,] bricksRef;
    BombManLetter[,] lettersRef;

    void Awake()
    {
        LoadBubbleSprites();
    }

    void Update()
    {
        // 화면 회전/리사이즈 등으로 가용 크기가 바뀌면 격자/셀 위치를 모두 다시 계산
        // (캐릭터는 자기 Update에서 CurrentCellSize 변화를 감지해 함께 따라옴)
        float fullW = GetFullWidth();
        float fullH = GetFullHeight();
        if (!Mathf.Approximately(fullW, lastAppliedFullW) ||
            !Mathf.Approximately(fullH, lastAppliedFullH))
        {
            RelayoutAll(fullW, fullH);
        }
    }

    void LoadBubbleSprites()
    {
        bubbleSprites = new Sprite[]
        {
            Resources.Load<Sprite>("imageBubbleBlue"),
            Resources.Load<Sprite>("imageBubbleGreen"),
            Resources.Load<Sprite>("imageBubbleyellow"),
            Resources.Load<Sprite>("imageBubbleRed"),
            Resources.Load<Sprite>("imageBubblePurple")
        };
    }

    float GetFullWidth()
    {
        if (background != null) return background.bounds.size.x;
        if (ScreenWallFitter.HalfW > 0f) return ScreenWallFitter.HalfW * 2f;
        if (Camera.main != null) return Camera.main.orthographicSize * Camera.main.aspect * 2f;
        return 10f;
    }

    float GetFullHeight()
    {
        if (background != null) return background.bounds.size.y;
        if (ScreenWallFitter.HalfH > 0f) return ScreenWallFitter.HalfH * 2f;
        if (Camera.main != null) return Camera.main.orthographicSize * 2f;
        return 10f;
    }

    // 한 셀 크기 — 가로/세로 둘 다 fit 가능한 작은 쪽
    public float ComputeCellSize(float fullW, float fullH)
    {
        if (cols <= 0 || rows <= 0) return 0f;
        float maxCellW = fullW / (cols + (cols - 1) * bubbleGapRatio);
        float maxCellH = fullH / (rows + (rows - 1) * bubbleGapRatio);
        return Mathf.Min(maxCellW, maxCellH);
    }

    // (col,row) → 월드 좌표 (그리드는 화면 중앙 정렬 + gridYOffset)
    public Vector3 ComputeCellPos(int col, int row, float fullW, float fullH)
    {
        float cell = ComputeCellSize(fullW, fullH);
        float gap = cell * bubbleGapRatio;
        float spacing = cell + gap;
        float totalW = cols * cell + (cols - 1) * gap;
        float totalH = rows * cell + (rows - 1) * gap;
        float startX = -totalW * 0.5f + cell * 0.5f;
        float startY = totalH * 0.5f - cell * 0.5f + gridYOffset;
        return new Vector3(startX + spacing * col, startY - spacing * row, 0f);
    }

    // 캐릭터/폭탄에서 매 프레임 호출하는 간편 버전 (현재 화면 기준)
    public Vector3 ComputeCellPosCurrent(int col, int row)
    {
        return ComputeCellPos(col, row, GetFullWidth(), GetFullHeight());
    }

    // 월드 좌표 → 가장 가까운 셀 (반경 안일 때만 hit)
    public bool WorldToCell(Vector3 world, out int col, out int row)
    {
        col = -1; row = -1;
        float fullW = GetFullWidth();
        float fullH = GetFullHeight();
        float cell = ComputeCellSize(fullW, fullH);
        if (cell <= 0f) return false;

        Vector3 cell00 = ComputeCellPos(0, 0, fullW, fullH);
        float gap = cell * bubbleGapRatio;
        float spacing = cell + gap;
        float fx = (world.x - cell00.x) / spacing;
        float fy = (cell00.y - world.y) / spacing;
        int cEst = Mathf.RoundToInt(fx);
        int rEst = Mathf.RoundToInt(fy);

        if (cEst < 0 || cEst >= cols) return false;
        if (rEst < 0 || rEst >= rows) return false;

        col = cEst;
        row = rEst;
        return true;
    }

    // ──────────────────────────────────────────────────────────────
    // 그리드 빌드 — 캐릭터 시작 위치 결정 + 벽돌/글자 배치
    // BombManMiniGame.GridData에 셀 상태와 글자/벽돌 컴포넌트 참조를 채워줌

    public class BuildResult
    {
        public int charCol;
        public int charRow;
        public int[,] cellType;             // 0=empty, 1=brick, 2=letter
        public BombManBrick[,] bricks;
        public BombManLetter[,] letters;
    }

    public BuildResult Build(string answer, List<string> charPool, bool isKorean)
    {
        DestroyAll();
        var result = new BuildResult
        {
            cellType = new int[cols, rows],
            bricks = new BombManBrick[cols, rows],
            letters = new BombManLetter[cols, rows],
        };

        // 1. 캐릭터 시작 위치 — 가장자리 1칸 제외(주변 4셀이 항상 유효하게)
        int sc = Random.Range(1, cols - 1);
        int sr = Random.Range(1, rows - 1);
        result.charCol = sc;
        result.charRow = sr;

        // 캐릭터 자기 셀 + 상하좌우 4셀은 빈칸 강제
        HashSet<(int, int)> forcedEmpty = new()
        {
            (sc, sr), (sc, sr - 1), (sc, sr + 1), (sc - 1, sr), (sc + 1, sr)
        };

        // 2. 글자 배치할 빈 셀 후보 — forcedEmpty 제외한 모든 셀
        List<(int, int)> candidates = new();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (!forcedEmpty.Contains((c, r)))
                    candidates.Add((c, r));

        // 후보 셔플
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        // 3. 글자 풀 — 정답 글자(중복 포함) 먼저, 그 외엔 정답에 없는 랜덤 글자
        List<string> letterPool = BuildLetterPool(answer, charPool, isKorean, letterCount);

        // 4. 글자를 letterCount개 후보 셀에 배치
        int placedLetters = Mathf.Min(letterCount, candidates.Count);
        for (int i = 0; i < placedLetters; i++)
        {
            var (c, r) = candidates[i];
            result.cellType[c, r] = 2; // letter
        }

        // 5. 나머지 후보는 벽돌
        for (int i = placedLetters; i < candidates.Count; i++)
        {
            var (c, r) = candidates[i];
            result.cellType[c, r] = 1; // brick
        }

        // 6. 실제 시각 오브젝트 생성
        float fullW = GetFullWidth();
        float fullH = GetFullHeight();
        float cell = ComputeCellSize(fullW, fullH);
        CurrentCellSize = cell;

        int letterIdx = 0;
        int colorIdx = 0;
        List<Sprite> colorPool = BuildColorPool(placedLetters);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int type = result.cellType[c, r];
                if (type == 0) continue;

                Vector3 pos = ComputeCellPos(c, r, fullW, fullH);

                if (type == 1)
                {
                    var brick = SpawnBrick(c, r, pos, cell);
                    result.bricks[c, r] = brick;
                }
                else if (type == 2)
                {
                    string letter = letterPool[letterIdx % letterPool.Count];
                    letterIdx++;
                    Sprite color = colorPool.Count > 0 ? colorPool[colorIdx % colorPool.Count] : null;
                    colorIdx++;
                    var letterObj = SpawnLetter(letter, c, r, pos, color, cell, isKorean);
                    result.letters[c, r] = letterObj;
                }
            }
        }

        // RelayoutAll에서 셀들을 다시 배치할 수 있도록 그리드 참조 저장
        cellTypeRef = result.cellType;
        bricksRef = result.bricks;
        lettersRef = result.letters;
        lastAppliedFullW = fullW;
        lastAppliedFullH = fullH;

        return result;
    }

    // BombManMiniGame이 cellType/bricks/letters를 갱신할 때 호출 (폭발 등으로 셀이 비워질 때)
    // RelayoutAll이 최신 그리드를 참조하도록 동기화
    public void SyncGridRefs(int[,] cellType, BombManBrick[,] bricks, BombManLetter[,] letters)
    {
        cellTypeRef = cellType;
        bricksRef = bricks;
        lettersRef = letters;
    }

    // 화면 크기 변경에 맞춰 모든 벽돌/글자 셀의 위치와 스케일 재계산
    // (캐릭터는 자기 Update에서 CurrentCellSize 감지로 따라오므로 별도 처리 불필요)
    void RelayoutAll(float fullW, float fullH)
    {
        if (cellTypeRef == null || bricksRef == null || lettersRef == null)
        {
            lastAppliedFullW = fullW;
            lastAppliedFullH = fullH;
            return;
        }

        float cell = ComputeCellSize(fullW, fullH);
        if (cell <= 0f) return;
        CurrentCellSize = cell;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int type = cellTypeRef[c, r];
                if (type == 0) continue;

                Vector3 pos = ComputeCellPos(c, r, fullW, fullH);

                if (type == 1 || type == 3) // brick 또는 폭탄 자리 (폭탄은 셀 위치 자체만 일관)
                {
                    var brick = bricksRef[c, r];
                    if (brick != null)
                    {
                        brick.transform.position = pos;
                        ApplyCellScale(brick.gameObject, cell);
                    }
                }
                else if (type == 2)
                {
                    var letter = lettersRef[c, r];
                    if (letter != null)
                    {
                        letter.transform.position = pos;
                        ApplyCellScale(letter.gameObject, cell);
                    }
                }
            }
        }

        lastAppliedFullW = fullW;
        lastAppliedFullH = fullH;
    }

    // 셀 GameObject의 시각 크기를 새 cellSize에 맞춰 재적용
    void ApplyCellScale(GameObject obj, float cellSize)
    {
        if (obj == null) return;
        var sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        float spriteW = sr.sprite.bounds.size.x;
        if (spriteW <= 0f) return;
        obj.transform.localScale = Vector3.one * (cellSize / spriteW);
    }

    // 정답 글자(중복 포함) 우선 + 나머지는 정답에 없는 글자로 채워 letterCount개 글자 풀 구성
    List<string> BuildLetterPool(string answer, List<string> charPool, bool isKorean, int target)
    {
        List<string> pool = new();

        // 정답 글자 — 단어에 들어있는 모든 글자(중복 포함)
        if (!string.IsNullOrEmpty(answer))
        {
            foreach (char c in answer)
            {
                if (isKorean)
                {
                    if (c < '가' || c > '힣') continue;
                    pool.Add(c.ToString());
                }
                else
                {
                    pool.Add(c.ToString());
                }
            }
        }

        HashSet<string> wordChars = new();
        foreach (string s in pool) wordChars.Add(s);

        // 채움 후보 — 정답에 없는 글자
        List<string> fillers = new();
        if (isKorean)
        {
            if (charPool != null)
                foreach (string ch in charPool)
                    if (!wordChars.Contains(ch)) fillers.Add(ch);
        }
        else
        {
            for (char c = 'A'; c <= 'Z'; c++)
            {
                string cs = c.ToString();
                if (!wordChars.Contains(cs)) fillers.Add(cs);
            }
        }
        for (int i = fillers.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (fillers[i], fillers[j]) = (fillers[j], fillers[i]);
        }
        if (fillers.Count == 0) fillers.Add(isKorean ? "가" : "A");

        // target 개에 도달할 때까지 채움
        int idx = 0;
        while (pool.Count < target)
            pool.Add(fillers[idx++ % fillers.Count]);

        // 정답 글자가 항상 등장하지만 위치가 한 곳에 몰리지 않도록 전체 셔플
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    List<Sprite> BuildColorPool(int count)
    {
        List<Sprite> pool = new();
        if (bubbleSprites == null || bubbleSprites.Length == 0) return pool;
        for (int i = 0; i < count; i++) pool.Add(bubbleSprites[i % bubbleSprites.Length]);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool;
    }

    // 벽돌 1개 — prefab이 연결돼 있으면 사용, 아니면 폴백 (단색 SpriteRenderer)
    BombManBrick SpawnBrick(int col, int row, Vector3 pos, float cellSize)
    {
        GameObject obj;
        if (brickPrefab != null)
        {
            obj = Instantiate(brickPrefab);
        }
        else
        {
            obj = new GameObject($"Brick_{col}_{row}");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetBrickFallbackSprite();
            sr.color = new Color(0.55f, 0.35f, 0.2f); // 갈색 벽돌 폴백
        }
        obj.transform.SetParent(transform, false);
        obj.transform.position = pos;
        obj.AddComponent<SortingGroup>();

        // 스프라이트 폭 기준 셀 크기로 스케일
        var srMain = obj.GetComponentInChildren<SpriteRenderer>();
        float spriteW = (srMain != null && srMain.sprite != null) ? srMain.sprite.bounds.size.x : 1f;
        if (spriteW <= 0f) spriteW = 1f;
        obj.transform.localScale = Vector3.one * (cellSize / spriteW);

        var brick = obj.GetComponent<BombManBrick>();
        if (brick == null) brick = obj.AddComponent<BombManBrick>();
        brick.Init(col, row);

        spawnedObjects.Add(obj);
        return brick;
    }

    // 폴백 벽돌 스프라이트 — 1×1 흰색 텍스처로 만든 동적 스프라이트 (캐시)
    Sprite GetBrickFallbackSprite()
    {
        if (brickFallbackSprite != null) return brickFallbackSprite;
        Texture2D tex = new(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        brickFallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return brickFallbackSprite;
    }

    // 글자 물방울 1개 — LineWordSpawner / PassWallSpawner 와 동일 스타일
    BombManLetter SpawnLetter(string letter, int col, int row, Vector3 pos,
                              Sprite bubbleSprite, float cellSize, bool isKorean)
    {
        if (letterPrefab == null)
        {
            Debug.LogError("[BombManSpawner] letterPrefab이 연결되지 않음");
            return null;
        }

        GameObject obj = Instantiate(letterPrefab);
        obj.transform.SetParent(transform, false);
        obj.transform.position = pos;
        obj.AddComponent<SortingGroup>();

        SpriteRenderer sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            if (bubbleSprite != null) sr.sprite = bubbleSprite;
            sr.sortingOrder = 0;
        }

        float spriteWidth = (sr != null && sr.sprite != null) ? sr.sprite.bounds.size.x : 1f;
        if (spriteWidth <= 0f) spriteWidth = 1f;
        obj.transform.localScale = Vector3.one * (cellSize / spriteWidth);

        GameObject textObj = new("LetterText");
        textObj.transform.SetParent(obj.transform, false);

        TMP_Text tmp = textObj.AddComponent<TextMeshPro>();
        if (isKorean && koreanBubbleFont != null) tmp.font = koreanBubbleFont;
        tmp.text = letter;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(11f, 11f);
        tmp.rectTransform.anchoredPosition = new Vector2(0f, 0.6f);
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.extraPadding = true;
        tmp.GetComponent<Renderer>().sortingOrder = 1;
        tmp.color = Color.white;

        Material mat = tmp.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.4f);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.1f, 0.3f, 0.9f));
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.4f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.05f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.05f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);

        if (obj.TryGetComponent(out AlphabetClick click))
        {
            click.Init(letter);
            click.enabled = false;
        }
        if (obj.TryGetComponent(out RandomMove rm)) Destroy(rm);

        var bl = obj.GetComponent<BombManLetter>();
        if (bl == null) bl = obj.AddComponent<BombManLetter>();
        bl.Init(letter, col, row);

        spawnedObjects.Add(obj);
        return bl;
    }

    public void DestroyAll()
    {
        foreach (var obj in spawnedObjects)
            if (obj != null) Destroy(obj);
        spawnedObjects.Clear();
        CurrentCellSize = -1f;
    }
}
