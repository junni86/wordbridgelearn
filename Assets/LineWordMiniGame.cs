using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// LineWord 미니게임 — 4벽 안 바둑판 그리드에서 정답 라인을 직선 드래그로 입력
// - 그리드 생성/배치는 LineWordSpawner 담당
// - 본 클래스는 입력(터치/마우스 드래그)을 받아 셀 hit 판정 후 호스트로 글자 전달
// - 첫 셀 → 두 번째 셀로 진입하는 순간 방향(가로/세로) 락
// - 락된 라인 위의 새 셀에 진입할 때마다 한 글자씩 NotifyLetterTyped 호출
public class LineWordMiniGame : MiniGameBase
{
    public static LineWordMiniGame Instance { get; private set; }

    [Header("스포너")]
    public LineWordSpawner spawner; // 자식에 부착

    [Header("이펙트 프리팹")]
    public GameObject dropletPrefab;     // 정답 시 잠깐 표시할 파티클 (선택)
    public GameObject wrongMarkPrefab;   // 오답 X 표시 (선택)

    // 드래그 상태
    bool isDragging;
    int firstCol = -1, firstRow = -1;        // 누름 시작 셀
    int lockedAxis = -1;                     // -1=락 안됨, 0=가로(row 고정), 1=세로(col 고정)
    int lockedRow = -1, lockedCol = -1;      // 락된 라인의 row 또는 col
    int lastCol = -1, lastRow = -1;          // 직전 프레임에 hover 했던 셀

    // 이 세션(현재 인스턴스) 동안의 단어 진행 차수 — rows 단계적 상승용
    // 1번째 단어 = 6행, 2번째 = 7행, ..., 5번째 = 10행, 6번째 = 다시 6행 ...
    // 미니게임이 교체되면 인스턴스가 새로 만들어지므로 자연히 0부터 다시 시작
    int wordCountInSession = 0;
    const int START_ROWS = 6;
    const int ROW_CYCLE = 5; // 6,7,8,9,10 → 5단계 순환

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void BeginWord(string answer, List<string> koreanCharPool, bool isKorean)
    {
        // 드래그 상태 리셋 + 새 그리드 생성
        ResetDragState();
        if (spawner == null) return;

        // 진입 차수에 따라 행 수 결정 — 1번째=6, 2번째=7, ... 5번째=10, 그 다음 다시 6
        int rowsForThisRound = START_ROWS + (wordCountInSession % ROW_CYCLE);
        wordCountInSession++;
        spawner.SetRows(rowsForThisRound);

        spawner.BeginWord(answer, koreanCharPool, isKorean);
    }

    public override void Cleanup()
    {
        ResetDragState();
        if (spawner != null) spawner.DestroyAll();
    }

    // 정답 입력 — 셀을 영구 확대 + 앞으로 (제거 X). 파티클은 선택적으로 표시
    public override void HandleCorrect(MonoBehaviour source, int filledIndex, string letter)
    {
        if (source == null) return;

        LineWordDroplet d = source as LineWordDroplet;
        if (d == null) d = source.GetComponent<LineWordDroplet>();
        if (d == null) return;

        if (dropletPrefab != null)
            Instantiate(dropletPrefab, d.transform.position, Quaternion.identity);

        d.PlayCorrectEffect();
    }

    // 오답 입력 — X마크만 잠깐 표시, 셀은 그대로 유지
    public override void HandleWrong(MonoBehaviour source, string expected)
    {
        if (source == null) return;
        ShowWrongMark(source.transform);
    }

    void ShowWrongMark(Transform parent)
    {
        if (wrongMarkPrefab == null || parent == null) return;
        GameObject mark = Instantiate(wrongMarkPrefab, parent);
        Destroy(mark, 0.5f);
    }

    void Update()
    {
        if (spawner == null || Camera.main == null) return;

        // 일시정지 중에는 입력 무시 — PausePanel(ContinueButton 등) 위 클릭이 격자 드래그로 새는 것 차단
        if (Time.timeScale == 0f)
        {
            ResetDragState();
            return;
        }

        // 터치/마우스 입력 통합 처리
        bool pressed = false, held = false, released = false;
        Vector2 screenPos = Vector2.zero;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            held = true;
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame) pressed = true;
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            released = true;
        }

        if (!held && Mouse.current != null)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                held = true;
                screenPos = Mouse.current.position.ReadValue();
                if (Mouse.current.leftButton.wasPressedThisFrame) pressed = true;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                released = true;
            }
        }

        // 누름 시작 / 손 뗌 처리
        if (released)
        {
            ResetDragState();
            return;
        }
        if (!held) return;

        // 화면 좌표 → 월드 좌표
        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));

        // 어느 셀 위에 있는지 판정 (가까운 셀 찾기)
        int hitCol, hitRow;
        if (!FindCellAt(world, out hitCol, out hitRow))
        {
            // 셀 바깥이면 현재 hover만 비움 — 입력은 발생시키지 않음
            // (다시 셀로 돌아오면 재진입으로 새 입력 발생)
            lastCol = -1; lastRow = -1;
            return;
        }

        if (pressed || firstCol < 0)
        {
            // 누름 시작 — 첫 셀
            firstCol = hitCol;
            firstRow = hitRow;
            lockedAxis = -1;
            isDragging = true;
            lastCol = hitCol;
            lastRow = hitRow;
            TryReportCell(hitCol, hitRow);
            return;
        }

        if (!isDragging) return;

        // 같은 셀 위에 머무는 동안은 재입력 X
        if (hitCol == lastCol && hitRow == lastRow) return;

        // 방향 락 결정 — 첫 셀 이후 처음 다른 셀에 진입할 때 한 번만
        if (lockedAxis < 0)
        {
            if (hitCol == firstCol && hitRow != firstRow)
            {
                // 세로 방향 락
                lockedAxis = 1;
                lockedCol = firstCol;
            }
            else if (hitRow == firstRow && hitCol != firstCol)
            {
                // 가로 방향 락
                lockedAxis = 0;
                lockedRow = firstRow;
            }
            else
            {
                // 대각선 등 직선이 아닌 진입 — 무시
                return;
            }
        }

        // 락된 라인을 벗어난 셀은 무시
        if (lockedAxis == 0 && hitRow != lockedRow) return;
        if (lockedAxis == 1 && hitCol != lockedCol) return;

        lastCol = hitCol;
        lastRow = hitRow;
        TryReportCell(hitCol, hitRow);
    }

    // 셀의 글자를 호스트로 전달 — 이미 consumed 된 셀은 중복 입력 방지
    void TryReportCell(int col, int row)
    {
        LineWordDroplet d = spawner.GetCell(col, row);
        if (d == null) return;
        if (d.consumed) return; // 같은 셀에 또 진입해도 한 번만 처리

        NotifyLetterTyped(d.letter, d);
        // consumed 처리는 HandleCorrect에서 PlayCorrectEffect 호출 시 수행
        // (오답이면 consumed 안 됨 → 다음 진입 때 또 시도 가능. 호스트가 currentIndex를 늘리지 않았으므로 동일 입력 반복 가능)
        // 단, 오답 시 같은 셀에서 매 프레임 반복 호출되는 것을 막기 위해 last 좌표는 갱신됨 (호출 후 그대로 머물면 위쪽 가드로 차단)
    }

    // 월드 좌표 → 가장 가까운 셀 (col,row) — 셀 반경 내에 있을 때만 hit 인정
    bool FindCellAt(Vector3 world, out int col, out int row)
    {
        col = -1; row = -1;
        if (spawner == null) return false;

        int cols = spawner.Cols;
        int rows = spawner.Rows;
        float fullW = GetSpawnerFullW();
        float fullH = GetSpawnerFullH();
        float cell = spawner.ComputeCellSize(fullW, fullH);
        if (cell <= 0f) return false;

        // 그리드 전체 사각 영역 안에 있는지 빠르게 확인 후 인덱스 계산
        float gap = cell * spawner.BubbleGapRatio;
        float spacing = cell + gap;
        float totalW = cols * cell + (cols - 1) * gap;
        float totalH = rows * cell + (rows - 1) * gap;

        // 그리드 좌상단(=col=0,row=0 셀 중심)의 월드 좌표를 spawner에서 직접 얻어 정확히 맞춤
        Vector3 cell00 = spawner.ComputeCellPos(0, 0, fullW, fullH);
        float startX = cell00.x;
        float startY = cell00.y;

        // 좌표를 스페이싱으로 나눠 인덱스 추정
        float fx = (world.x - startX) / spacing;
        float fy = (startY - world.y) / spacing; // y는 위→아래로 row 증가
        int cEst = Mathf.RoundToInt(fx);
        int rEst = Mathf.RoundToInt(fy);

        if (cEst < 0 || cEst >= cols) return false;
        if (rEst < 0 || rEst >= rows) return false;

        // 셀 중심으로부터 거리 확인 — 셀 반경(반지름) 안일 때만 hit
        Vector3 center = spawner.ComputeCellPos(cEst, rEst, fullW, fullH);
        float dx = world.x - center.x;
        float dy = world.y - center.y;
        float r = cell * 0.5f;
        if (dx * dx + dy * dy > r * r) return false;

        col = cEst;
        row = rEst;
        return true;
    }

    // 화면 폭/높이 — spawner 내부 계산을 그대로 따르도록 (배경 우선 → ScreenWallFitter → 카메라)
    float GetSpawnerFullW()
    {
        if (ScreenWallFitter.HalfW > 0f) return ScreenWallFitter.HalfW * 2f;
        if (Camera.main != null) return Camera.main.orthographicSize * Camera.main.aspect * 2f;
        return 10f;
    }

    float GetSpawnerFullH()
    {
        if (ScreenWallFitter.HalfH > 0f) return ScreenWallFitter.HalfH * 2f;
        if (Camera.main != null) return Camera.main.orthographicSize * 2f;
        return 10f;
    }

    void ResetDragState()
    {
        isDragging = false;
        firstCol = -1; firstRow = -1;
        lockedAxis = -1;
        lockedRow = -1; lockedCol = -1;
        lastCol = -1; lastRow = -1;
    }

    // 힌트 — 다음 입력 글자(nextLetter)와 일치하는 정답 라인의 첫 셀 1개만 잠시 키움
    // (이미 consumed=true 인 셀은 영구 확대 상태라 중복 곱해지지 않게 제외)
    public override void ShowHint(string nextLetter, float duration)
    {
        if (string.IsNullOrEmpty(nextLetter) || spawner == null) return;

        for (int r = 0; r < spawner.Rows; r++)
        {
            for (int c = 0; c < spawner.Cols; c++)
            {
                var d = spawner.GetCell(c, r);
                if (d == null || d.consumed) continue;
                if (d.letter == nextLetter)
                {
                    StartCoroutine(AnimateHintHighlight(new List<Transform> { d.transform }, duration));
                    return;
                }
            }
        }
    }
}
