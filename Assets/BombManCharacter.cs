using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// BombMan 미니게임 캐릭터
// - 9×11 그리드 위에서 셀 단위로 가로/세로 이동 (대각선 X)
// - 터치/마우스를 누르고 있는 동안 그 위치(셀)를 향해 자동 이동
//   · 더 큰 축(가로/세로) 먼저 이동, 벽돌·글자·폭탄에 막히면 정지
// - 화면을 더블 탭(doubleTapInterval 이내 2번)하면 캐릭터 현재 셀에 폭탄 설치 요청
// - 자기 폭탄 폭발에 휘말리면 OnCaughtByExplosion() — 0.5초 정지 + 오답 패널티(호스트 위임)
[RequireComponent(typeof(Collider2D))]
public class BombManCharacter : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("셀 간 이동 속도 (월드 단위 / 초)")]
    public float moveSpeed = 4f;

    [Tooltip("자폭 시 정지 시간 (초)")]
    public float caughtPauseDuration = 0.5f;

    [Header("크기 (셀 폭 대비)")]
    [Tooltip("캐릭터 폭이 한 셀 폭의 몇 %인지 (0~1)")]
    [Range(0.3f, 1.2f)] public float widthRatioOfCell = 0.85f;

    [Header("더블 탭")]
    [Tooltip("더블 탭으로 인정되는 최대 시간 간격 (초)")]
    public float doubleTapInterval = 0.4f;

    BombManMiniGame owner;
    BombManSpawner spawner;

    // 논리 위치 — 현재 안착한 셀 / 이동 중이면 다음 목표 셀
    int currentCol = -1, currentRow = -1;
    int nextCol = -1, nextRow = -1;
    bool isMoving;
    bool isPaused;

    // 더블 탭 감지 — 화면 어디든 두 번 탭하면 폭탄 설치 (0.4초 이내)
    float lastTapTime = -10f;

    // 폭탄 설치가 발생한 프레임에는 같은 입력이 이동 명령으로도 해석되지 않도록 1프레임 차단
    bool suppressMovementThisFrame;

    Collider2D selfCol;
    SpriteRenderer cachedSr;
    float lastAppliedCellSize = -1f;

    void Awake()
    {
        selfCol = GetComponent<Collider2D>();
        selfCol.isTrigger = true;
        cachedSr = GetComponentInChildren<SpriteRenderer>();
    }

    // 미니게임에서 초기화 호출 — 시작 셀 지정 + 화면 위치/스케일 즉시 반영
    public void Init(BombManMiniGame ownerGame, BombManSpawner ownerSpawner, int startCol, int startRow)
    {
        owner = ownerGame;
        spawner = ownerSpawner;
        currentCol = startCol;
        currentRow = startRow;
        nextCol = startCol;
        nextRow = startRow;
        isMoving = false;
        isPaused = false;

        SnapToCurrentCell();
        ApplyScale();
    }

    public int CurrentCol => currentCol;
    public int CurrentRow => currentRow;

    void Update()
    {
        if (spawner == null || owner == null) return;

        // 셀 크기 변경(회전/리사이즈) 시 스케일/위치 보정
        float cellSize = spawner.CurrentCellSize;
        if (!Mathf.Approximately(cellSize, lastAppliedCellSize))
        {
            ApplyScale();
            if (!isMoving) SnapToCurrentCell();
        }

        // 일시정지(자체 isPaused 또는 글로벌 timeScale=0)면 입력/이동 모두 무시
        if (isPaused || Time.timeScale == 0f) return;

        HandleTapInput();
        HandleMovement();
    }

    // 터치/마우스 입력 — 화면 어디든 두 번 탭하면 캐릭터 현재 셀에 폭탄 설치
    // (위치 무관: 더 이상 캐릭터 셀 위에서만 탭을 받지 않음)
    void HandleTapInput()
    {
        bool tapStarted = false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            tapStarted = true;
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            tapStarted = true;

        if (!tapStarted) return;

        float now = Time.time;
        if (now - lastTapTime <= doubleTapInterval)
        {
            // 두 번째 탭 — 폭탄 설치 요청 (위치 무관, 항상 캐릭터 현재 셀에 설치)
            lastTapTime = -10f; // 리셋
            if (owner != null) owner.RequestPlaceBomb(currentCol, currentRow);

            // 같은 프레임에 HandleMovement가 이 탭을 이동 명령으로도 해석하지 않도록 차단
            suppressMovementThisFrame = true;
        }
        else
        {
            lastTapTime = now;
        }
    }

    // 격자 위 셀 단위 이동 — 셀 중심에 도착해야 다음 방향 판정
    void HandleMovement()
    {
        if (Camera.main == null || spawner == null) return;

        // 이동 중이면 다음 셀로 보간
        if (isMoving)
        {
            Vector3 target = spawner.ComputeCellPosCurrent(nextCol, nextRow);
            Vector3 p = transform.position;
            p = Vector3.MoveTowards(p, target, moveSpeed * Time.deltaTime);
            transform.position = p;

            // 목표 셀 중심 도착
            if ((p - target).sqrMagnitude < 0.0001f)
            {
                transform.position = target;
                currentCol = nextCol;
                currentRow = nextRow;
                isMoving = false;
            }
            return;
        }

        // 폭탄 설치가 발생한 프레임은 이동 명령 무시 (같은 탭이 이동으로도 해석되는 것 방지)
        if (suppressMovementThisFrame)
        {
            suppressMovementThisFrame = false;
            return;
        }

        // 정지 상태 — 누르고 있는 동안만 새 이동 명령 수락
        Vector2? screenPos = null;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            screenPos = Mouse.current.position.ReadValue();

        if (!screenPos.HasValue) return;

        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.Value.x, screenPos.Value.y, 0f));
        if (!spawner.WorldToCell(world, out int targetCol, out int targetRow)) return;

        int dCol = targetCol - currentCol;
        int dRow = targetRow - currentRow;
        if (dCol == 0 && dRow == 0) return;

        // 더 큰 축 먼저 이동, 그 축이 막혀 있으면 다른 축 시도
        int stepCol = 0, stepRow = 0;
        if (Mathf.Abs(dCol) >= Mathf.Abs(dRow))
        {
            stepCol = dCol > 0 ? 1 : -1;
            if (!TryStartStep(stepCol, 0))
            {
                stepCol = 0;
                stepRow = dRow > 0 ? 1 : (dRow < 0 ? -1 : 0);
                if (stepRow != 0) TryStartStep(0, stepRow);
            }
        }
        else
        {
            stepRow = dRow > 0 ? 1 : -1;
            if (!TryStartStep(0, stepRow))
            {
                stepRow = 0;
                stepCol = dCol > 0 ? 1 : (dCol < 0 ? -1 : 0);
                if (stepCol != 0) TryStartStep(stepCol, 0);
            }
        }
    }

    // (dCol, dRow) 방향 인접 셀이 walkable이면 이동 시작
    bool TryStartStep(int dCol, int dRow)
    {
        if (dCol == 0 && dRow == 0) return false;
        int nc = currentCol + dCol;
        int nr = currentRow + dRow;
        if (owner == null) return false;
        if (!owner.IsWalkable(nc, nr)) return false;

        nextCol = nc;
        nextRow = nr;
        isMoving = true;
        return true;
    }

    // 자기 위치를 현재 논리 셀의 월드 좌표로 즉시 이동
    void SnapToCurrentCell()
    {
        if (spawner == null) return;
        transform.position = spawner.ComputeCellPosCurrent(currentCol, currentRow);
    }

    // 셀 폭에 맞춰 캐릭터 스케일 적용
    void ApplyScale()
    {
        if (cachedSr == null || cachedSr.sprite == null || spawner == null) return;
        float cellSize = spawner.CurrentCellSize;
        if (cellSize <= 0f) return;

        float spriteWidth = cachedSr.sprite.bounds.size.x;
        if (spriteWidth <= 0f) return;

        float scale = (cellSize * widthRatioOfCell) / spriteWidth;
        transform.localScale = Vector3.one * scale;
        lastAppliedCellSize = cellSize;
    }

    // 미니게임에서 자폭 시 호출 — 0.5초 정지
    public void OnCaughtByExplosion()
    {
        StartCoroutine(PauseCoroutine(caughtPauseDuration));
    }

    IEnumerator PauseCoroutine(float duration)
    {
        isPaused = true;
        isMoving = false; // 이동 중이었어도 그 자리에 멈춤
        yield return new WaitForSeconds(duration);
        isPaused = false;
    }
}
