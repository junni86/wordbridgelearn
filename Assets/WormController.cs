using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 지렁이 머리 + 몸통 관리
// - 자동 이동 (RandomMove와 동일하게 사방 벽에서 반사)
// - 화면 터치 시 머리가 그 방향으로 이동 방향 전환
// - 정답 글자 입력 시 머리 다음 칸부터 단어 순서대로 글자 채움
// - 오답 시 일정 시간 정지
[RequireComponent(typeof(Rigidbody2D))]
public class WormController : MonoBehaviour
{
    [Header("머리 이동 속도")]
    [Tooltip("터치 안 할 때 (관성/기본 이동 속도)")]
    public float speed = 1.5f;
    [Tooltip("터치 중일 때 (사용자가 적극적으로 조종할 때)")]
    public float touchSpeed = 4.0f;

    [Header("몸통 이동 속도 (머리와 독립)")]
    [Tooltip("터치 안 할 때 몸통이 머리를 따라가는 속도")]
    public float bodySpeed = 1.5f;
    [Tooltip("터치 중일 때 몸통이 머리를 따라가는 속도")]
    public float bodyTouchSpeed = 7.0f;

    // 현재 프레임에 터치 입력이 활성인지 (매 Update에서 HandleTouchInput이 갱신)
    bool isTouchActive;

    [Header("스프라이트")]
    public Sprite headSprite;        // 머리 이미지
    public Sprite emptyBodySprite;   // 빈 몸통 이미지 (정답 채우기 전)

    [Header("몸통")]
    public GameObject segmentPrefab;        // 몸통 한 칸 프리팹 (WormSegment 컴포넌트 포함)
    public float segmentSpacing = 0.4f;     // 몸통 간 거리 (월드 단위)

    [Header("머리 시각")]
    public SpriteRenderer headSpriteRenderer; // 머리 GameObject의 SpriteRenderer

    Rigidbody2D rb;
    Vector2 moveDir;
    bool isPaused;

    // 현재 단어 (몸통 길이 결정용)
    string targetWord = string.Empty;

    // 몸통 세그먼트 리스트 (index 0 = 머리 바로 뒤, 마지막 = 꼬리)
    readonly List<WormSegment> bodySegments = new();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 마찰력 제거 - 벽에 붙는 현상 방지
        rb.sharedMaterial = new PhysicsMaterial2D { friction = 0f, bounciness = 0f };

        // 머리 이미지 설정
        if (headSpriteRenderer != null && headSprite != null)
            headSpriteRenderer.sprite = headSprite;
    }

    void Start()
    {
        // 초기엔 정지 상태 — 첫 터치/클릭 입력이 들어와야 이동 시작
        moveDir = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }

    // 미니게임 교체 등으로 본 컨트롤러가 파괴될 때 — 씬 루트에 남은 몸통도 함께 정리
    void OnDestroy()
    {
        foreach (var seg in bodySegments)
            if (seg != null) Destroy(seg.gameObject);
        bodySegments.Clear();
    }

    // 단어 설정 — 단어 길이만큼 빈 몸통 세그먼트 생성
    // 정답 검증은 호스트가 담당하므로 여기서는 길이 정보만 저장
    public void SetupWord(string word)
    {
        targetWord = word ?? string.Empty;

        // 새 단어 시작 시 머리를 화면 중앙(월드 원점)으로 리셋
        transform.position = Vector3.zero;

        // 기존 몸통 제거
        foreach (var seg in bodySegments)
            if (seg != null) Destroy(seg.gameObject);
        bodySegments.Clear();

        // 단어 길이만큼 몸통 생성 — 모두 머리와 같은 위치에 겹쳐서 생성
        // 머리가 움직이기 시작하면 UpdateBodyFollow가 거리 기반으로 끌어당겨 자연스러운 등장 효과
        for (int i = 0; i < targetWord.Length; i++)
        {
            if (segmentPrefab == null) break;

            Vector3 spawnPos = transform.position;
            GameObject obj = Instantiate(segmentPrefab, spawnPos, Quaternion.identity);

            if (obj.TryGetComponent(out WormSegment seg))
            {
                seg.SetSprite(emptyBodySprite);
                seg.SetLetter(string.Empty);
                bodySegments.Add(seg);
            }
        }
    }

    // 호스트가 정답 판정 후 호출 — 지정 인덱스 몸통에 글자 표시 + 선택적으로 스프라이트 교체
    // sprite 가 null이 아니면 몸통 배경을 먹은 버블 이미지로 변경
    public void FillLetterAt(int index, string letter, Sprite sprite = null)
    {
        if (index < 0 || index >= bodySegments.Count) return;
        if (bodySegments[index] == null) return;
        bodySegments[index].SetLetter(letter);
        if (sprite != null) bodySegments[index].SetSprite(sprite);

        // 가장 최근에 채워진 몸통이 위에 오도록 sortingOrder 갱신 (index+1 → 안 채워진 몸통(0)보다 큼)
        bodySegments[index].SetSortingOrder(index + 1);
    }

    // 머리에 버블이 부딪히면 WormMiniGame을 통해 호스트로 글자 전달
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isPaused) return;
        // 버블 식별 — AlphabetClick 컴포넌트 보유 여부로 판정
        if (!collision.collider.TryGetComponent(out AlphabetClick click)) return;
        if (WormMiniGame.Instance == null) return;

        WormMiniGame.Instance.ReportCollision(click.letter, click);
    }

    // 오답 시 일정 시간 정지
    public void Pause(float duration)
    {
        StartCoroutine(PauseCoroutine(duration));
    }

    IEnumerator PauseCoroutine(float duration)
    {
        isPaused = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(duration);

        isPaused = false;
        if (rb != null) rb.linearVelocity = moveDir * speed;
    }

    void Update()
    {
        // 일시정지 중에는 입력/이동 모두 무시
        if (isPaused) return;

        HandleTouchInput();
        HandleWallBounce();

        // 머리 속도: 터치 중 vs 비터치
        float headSpeed = isTouchActive ? touchSpeed : speed;

        // 몸통 갱신 (몸통 자체 속도 사용)
        UpdateBodyFollow();

        rb.linearVelocity = moveDir * headSpeed;
    }

    // 화면 터치/클릭 시 머리가 그 방향으로 이동 방향 전환 (새 Input System API)
    void HandleTouchInput()
    {
        // 매 프레임 초기화 — 입력이 감지되면 아래에서 true로 갱신됨
        isTouchActive = false;

        Vector2? screenPos = null;

        // 모바일 터치 우선
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        // 에디터/PC 마우스
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            screenPos = Mouse.current.position.ReadValue();
        }

        if (!screenPos.HasValue) return;
        if (Camera.main == null) return;

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.Value.x, screenPos.Value.y, 0f));
        worldPos.z = 0f;

        Vector2 dir = (Vector2)(worldPos - transform.position);
        if (dir.sqrMagnitude < 0.001f) return;

        moveDir = dir.normalized;
        // 입력 감지 + 방향 갱신 성공 — 터치 활성으로 표시
        isTouchActive = true;
    }

    // RandomMove와 동일한 사방 벽 반사 처리
    void HandleWallBounce()
    {
        Vector3 pos = transform.position;
        float hw = ScreenWallFitter.HalfW;
        float hh = ScreenWallFitter.HalfH;

        if (hw == 0f || hh == 0f) return;

        float topBound = ScreenWallFitter.TopBoundaryY > 0 ? ScreenWallFitter.TopBoundaryY : hh;

        if (pos.x > hw) { pos.x = hw; moveDir.x = -Mathf.Abs(moveDir.x); }
        else if (pos.x < -hw) { pos.x = -hw; moveDir.x = Mathf.Abs(moveDir.x); }

        if (pos.y > topBound) { pos.y = topBound; moveDir.y = -Mathf.Abs(moveDir.y); }
        else if (pos.y < -hh) { pos.y = -hh; moveDir.y = Mathf.Abs(moveDir.y); }

        transform.position = pos;
    }

    // 몸통이 머리(또는 앞 세그먼트)를 직접 추격 — 몸통 전용 속도 사용 (머리와 독립)
    // bodySpeed / bodyTouchSpeed × Time.deltaTime 만큼만 이동
    void UpdateBodyFollow()
    {
        if (bodySegments.Count == 0) return;

        // 몸통 자체 속도 (터치 중 / 비터치 분기)
        float bodyCurrentSpeed = isTouchActive ? bodyTouchSpeed : bodySpeed;
        float maxStep = bodyCurrentSpeed * Time.deltaTime;
        Vector3 frontPos = transform.position;

        for (int i = 0; i < bodySegments.Count; i++)
        {
            if (bodySegments[i] == null) continue;

            Vector3 curPos = bodySegments[i].transform.position;
            Vector3 toFront = frontPos - curPos;
            float dist = toFront.magnitude;

            // 앞 세그먼트로부터 segmentSpacing 거리 떨어진 위치를 목표로
            Vector3 targetPos = dist > 0.0001f
                ? frontPos - toFront.normalized * segmentSpacing
                : curPos;

            // 머리와 동일한 속도(maxStep)로 목표 향해 이동 — 점프 없이 부드럽게
            bodySegments[i].transform.position =
                Vector3.MoveTowards(curPos, targetPos, maxStep);

            // 다음 세그먼트의 기준은 이번 세그먼트의 새 위치
            frontPos = bodySegments[i].transform.position;
        }
    }
}
