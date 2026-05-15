using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

// LineWord 미니게임 — 4벽 내부에 바둑판(cols × rows) 형태로 물방울 셀 생성
// - 정답 단어를 가로 또는 세로 라인에 무작위로 배치 (시작 위치/방향 모두 랜덤)
// - 나머지 셀은 정답에 포함되지 않은 글자로 랜덤 채움
// - 셀 크기/여백 계산은 PassWallSpawner의 공식을 차용 (bubbleWidthRatio, bubbleGapRatio)
public class LineWordSpawner : MonoBehaviour
{
    [Header("프리팹 / 배경")]
    public GameObject prefab;          // 셀 프리팹 (알파벳 버블 프리팹 재사용)
    public SpriteRenderer background;  // 가로/세로 범위 기준 (없으면 ScreenWallFitter/카메라 기준)

    [Header("그리드 설정")]
    [SerializeField] int cols = 8;     // 가로 칸 수
    [SerializeField] int rows = 10;    // 세로 칸 수
    // 셀 폭이 한 슬롯의 몇 %인지 (0~1) — 화면 크기 무관
    [SerializeField, Range(0.1f, 1f)] float bubbleWidthRatio = 0.85f;
    // 셀 사이 여백을 "셀 폭의 몇 %"로 지정
    [SerializeField, Range(0f, 1f)] float bubbleGapRatio = 0.15f;
    [SerializeField] float fontSize = 5.5f;
    [SerializeField] TMP_FontAsset koreanBubbleFont; // 한글 모드 전용 폰트

    [Header("그리드 위치 미세조정")]
    [Tooltip("그리드 전체를 위로/아래로 미세 이동 (양수 = 위로)")]
    [SerializeField] float gridYOffset = 0f;

    public int Cols => cols;
    public int Rows => rows;
    public float BubbleWidthRatio => bubbleWidthRatio;
    public float BubbleGapRatio => bubbleGapRatio;

    // 외부에서 행 수 변경 — 단어마다 난이도 상승시킬 때 사용
    // (실제 그리드는 다음 BeginWord에서 새 rows로 빌드됨)
    public void SetRows(int newRows)
    {
        rows = Mathf.Max(1, newRows);
    }

    Sprite[] bubbleSprites;
    readonly List<LineWordDroplet> spawnedDroplets = new();
    LineWordDroplet[,] grid; // [col, row] 직접 접근용

    string currentAnswer = string.Empty;
    List<string> currentCharPool;
    bool currentIsKorean;

    // 현재 적용된 화면 크기 캐시 — 리사이즈 시 감지
    float lastFullW = -1f;
    float lastFullH = -1f;

    void Awake()
    {
        LoadBubbleSprites();
    }

    void Update()
    {
        // 화면 회전/해상도 변경 등으로 화면 크기가 바뀌면 모든 셀의 위치+스케일 재적용
        float fullW = GetFullWidth();
        float fullH = GetFullHeight();
        if (!Mathf.Approximately(fullW, lastFullW) || !Mathf.Approximately(fullH, lastFullH))
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

    // 화면 가용 폭 — 배경 우선, 없으면 ScreenWallFitter, 그것도 없으면 카메라 기준
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

    // 그리드 한 셀의 월드 폭/높이 — 가로/세로 모두 같은 정사각 셀로, 작은 쪽에 fit
    public float ComputeCellSize(float fullW, float fullH)
    {
        if (cols <= 0 || rows <= 0) return 0f;

        // cell + (cols-1)*gap*cell = cell*(cols + (cols-1)*gap)
        // 가로/세로 각각 fit 가능한 최대 cell 크기 계산 후 더 작은 쪽 사용
        float maxCellW = fullW / (cols + (cols - 1) * bubbleGapRatio);
        float maxCellH = fullH / (rows + (rows - 1) * bubbleGapRatio);
        return Mathf.Min(maxCellW, maxCellH);
    }

    // (col,row) → 월드 좌표 (그리드는 화면 중앙에 정렬 + gridYOffset 보정)
    public Vector3 ComputeCellPos(int col, int row, float fullW, float fullH)
    {
        float cell = ComputeCellSize(fullW, fullH);
        float gap = cell * bubbleGapRatio;
        float spacing = cell + gap;

        // 행/열 전체 크기 — 가운데 정렬용
        float totalW = cols * cell + (cols - 1) * gap;
        float totalH = rows * cell + (rows - 1) * gap;

        float startX = -totalW * 0.5f + cell * 0.5f;
        float startY = totalH * 0.5f - cell * 0.5f + gridYOffset; // 위쪽이 row=0

        float x = startX + spacing * col;
        float y = startY - spacing * row;
        return new Vector3(x, y, 0f);
    }

    // 새 단어 시작 — 기존 셀 모두 제거 후 새 그리드 생성
    public void BeginWord(string answer, List<string> charPool, bool isKorean)
    {
        currentAnswer = isKorean ? (answer ?? string.Empty) : (answer ?? string.Empty).ToUpper();
        currentCharPool = charPool;
        currentIsKorean = isKorean;

        DestroyAll();
        BuildGrid();
    }

    public void DestroyAll()
    {
        foreach (var d in spawnedDroplets)
            if (d != null) Destroy(d.gameObject);
        spawnedDroplets.Clear();
        grid = null;
    }

    // 외부(LineWordMiniGame)에서 그리드 셀에 접근할 때 사용
    public LineWordDroplet GetCell(int col, int row)
    {
        if (grid == null) return null;
        if (col < 0 || col >= cols || row < 0 || row >= rows) return null;
        return grid[col, row];
    }

    // 정답 라인 배치 + 나머지 칸 랜덤 글자로 채워 그리드 생성
    void BuildGrid()
    {
        grid = new LineWordDroplet[cols, rows];

        // 1. 정답이 들어갈 라인 결정 — 가로/세로 50:50, 시작 위치 랜덤
        int answerLen = currentAnswer.Length;
        if (answerLen <= 0) return;

        bool horizontal = Random.value < 0.5f;
        // 정답이 그리드 한 변보다 길면 강제로 다른 방향 사용 (둘 다 안 들어가면 가능한 한 fit)
        if (horizontal && answerLen > cols) horizontal = false;
        else if (!horizontal && answerLen > rows) horizontal = true;
        // 그래도 안 맞으면 (예: 정답 11글자 + 그리드 8x10) 가능한 라인으로 클램프
        int maxLen = horizontal ? cols : rows;
        int placedLen = Mathf.Min(answerLen, maxLen);

        int startCol, startRow, dCol, dRow;
        if (horizontal)
        {
            startRow = Random.Range(0, rows);
            startCol = Random.Range(0, cols - placedLen + 1);
            dCol = 1; dRow = 0;
        }
        else
        {
            startCol = Random.Range(0, cols);
            startRow = Random.Range(0, rows - placedLen + 1);
            dCol = 0; dRow = 1;
        }

        // 2. 정답 글자가 차지할 (col,row) 좌표 집합 (HashSet으로 빠른 판정)
        var answerCells = new Dictionary<(int, int), string>(placedLen);
        for (int i = 0; i < placedLen; i++)
        {
            int c = startCol + dCol * i;
            int r = startRow + dRow * i;
            answerCells[(c, r)] = currentAnswer[i].ToString();
        }

        // 3. 나머지 칸 채움용 후보 풀 — 정답에 등장하는 글자는 제외
        HashSet<string> wordChars = new();
        foreach (char c in currentAnswer)
        {
            if (currentIsKorean)
            {
                if (c < '가' || c > '힣') continue;
                wordChars.Add(c.ToString());
            }
            else
            {
                wordChars.Add(c.ToString());
            }
        }

        List<string> fillers = new();
        if (currentIsKorean)
        {
            if (currentCharPool != null)
                foreach (string ch in currentCharPool)
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
        // 후보 셔플
        for (int i = fillers.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (fillers[i], fillers[j]) = (fillers[j], fillers[i]);
        }
        // 후보가 비어 있으면 안전 폴백 (이론상 거의 발생 안 함)
        if (fillers.Count == 0)
            fillers.Add(currentIsKorean ? "가" : "A");

        // 4. 색상 풀 — 전체 셀 수 만큼 균등 분배 후 셔플
        int totalCells = cols * rows;
        List<Sprite> colorPool = BuildColorPool(totalCells);

        // 5. 화면 폭/높이 + 셀 크기 계산
        float fullW = GetFullWidth();
        float fullH = GetFullHeight();
        float cell = ComputeCellSize(fullW, fullH);

        // 6. 그리드 순회하며 셀 생성
        int colorIdx = 0;
        int fillerIdx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                string letter;
                bool isAnswerCell;
                if (answerCells.TryGetValue((c, r), out string ansLetter))
                {
                    letter = ansLetter;
                    isAnswerCell = true;
                }
                else
                {
                    letter = fillers[fillerIdx % fillers.Count];
                    fillerIdx++;
                    isAnswerCell = false;
                }

                Sprite sprite = colorPool.Count > 0 ? colorPool[colorIdx % colorPool.Count] : null;
                colorIdx++;

                Vector3 pos = ComputeCellPos(c, r, fullW, fullH);
                SpawnCell(letter, c, r, isAnswerCell, pos, sprite, cell);
            }
        }

        lastFullW = fullW;
        lastFullH = fullH;
    }

    // 5색 균등 분배 + 셔플
    List<Sprite> BuildColorPool(int count)
    {
        List<Sprite> pool = new();
        if (bubbleSprites == null || bubbleSprites.Length == 0) return pool;

        for (int i = 0; i < count; i++)
            pool.Add(bubbleSprites[i % bubbleSprites.Length]);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool;
    }

    // 셀 1개 생성 — PassWallSpawner.SpawnOne의 형태를 따름
    void SpawnCell(string letter, int col, int row, bool isAnswerCell,
                   Vector3 pos, Sprite bubbleSprite, float cellWidth)
    {
        GameObject obj = Instantiate(prefab);
        obj.transform.SetParent(transform, false);
        obj.transform.position = pos;
        // 한 셀(버블+글자)을 묶어 다른 셀과 독립 렌더링
        obj.AddComponent<SortingGroup>();

        // 버블 배경
        SpriteRenderer sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            if (bubbleSprite != null) sr.sprite = bubbleSprite;
            sr.sortingOrder = 0;
        }

        // 스프라이트 원본 폭에서 목표 셀 폭으로 스케일 계산
        float spriteWidth = (sr != null && sr.sprite != null) ? sr.sprite.bounds.size.x : 1f;
        if (spriteWidth <= 0f) spriteWidth = 1f;
        float scale = cellWidth / spriteWidth;
        obj.transform.localScale = Vector3.one * scale;

        // 글자 TMP — PassWall과 동일 스타일
        GameObject textObj = new("LetterText");
        textObj.transform.SetParent(obj.transform, false);

        TMP_Text tmp = textObj.AddComponent<TextMeshPro>();
        if (currentIsKorean && koreanBubbleFont != null) tmp.font = koreanBubbleFont;
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

        // 알파벳 게임 클릭 컴포넌트는 LineWord에서 사용하지 않음 — 비활성화
        if (obj.TryGetComponent(out AlphabetClick click))
        {
            click.Init(letter);
            click.enabled = false;
        }
        // 이동 컴포넌트가 붙어 있으면 제거 (LineWord는 정적 격자)
        if (obj.TryGetComponent(out RandomMove rm)) Destroy(rm);

        // 셀 데이터 + 효과 컴포넌트 부착
        LineWordDroplet droplet = obj.GetComponent<LineWordDroplet>();
        if (droplet == null) droplet = obj.AddComponent<LineWordDroplet>();
        droplet.Init(letter, col, row, isAnswerCell);

        grid[col, row] = droplet;
        spawnedDroplets.Add(droplet);
    }

    // 화면 크기가 바뀌면 모든 셀 위치/스케일 재계산 (정답 처리되어 확대된 셀은 비율 유지)
    void RelayoutAll(float fullW, float fullH)
    {
        if (grid == null || prefab == null) { lastFullW = fullW; lastFullH = fullH; return; }

        float cell = ComputeCellSize(fullW, fullH);
        if (cell <= 0f) return;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                LineWordDroplet d = grid[c, r];
                if (d == null) continue;

                d.transform.position = ComputeCellPos(c, r, fullW, fullH);

                SpriteRenderer sr = d.GetComponentInChildren<SpriteRenderer>();
                float spriteWidth = (sr != null && sr.sprite != null) ? sr.sprite.bounds.size.x : 1f;
                if (spriteWidth <= 0f) spriteWidth = 1f;
                float scale = cell / spriteWidth;
                Vector3 newBase = Vector3.one * scale;
                d.UpdateBaseScale(newBase);
            }
        }

        lastFullW = fullW;
        lastFullH = fullH;
    }
}
