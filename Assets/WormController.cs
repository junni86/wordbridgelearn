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

    [Header("정답 글자 누적 부스트")]
    [Tooltip("정답 글자 1개당 머리 속도 증가율 (0.15 = +15%/글자, 단어마다 리셋)")]
    public float speedBoostPerLetter = 0.0f;
    // 현재 단어에서 먹은 정답 글자 수 — SetupWord에서 0으로 리셋, BoostSpeed에서 +1
    int lettersEaten;

    // 머리가 실제로 지나간 경로 — 매 프레임 기록, 몸통은 이 경로 위에서 거리만큼 뒤를 따라감
    readonly List<Vector3> headPath = new();

    [Header("디버그")]
    [Tooltip("켜면 매 프레임 머리/몸통 이동량을 콘솔에 로그")]
    public bool debugLogSpeeds = true;

    // 직전 프레임 위치 기록 — 이번 프레임 이동량(=속도×dt) 계산용
    Vector3 debugPrevHeadPos;
    readonly List<Vector3> debugPrevBodyPositions = new();
    bool debugHasPrev = false;

    // 부스트 단계별 평균 이동량 누적 — 부스트 전/후 비교용
    int debugPrevLettersEaten = -1;     // 직전 프레임 lettersEaten — 변화 감지용
    float debugHeadDeltaSum = 0f;       // 현재 부스트 단계 누적 머리 이동량
    float debugBody0DeltaSum = 0f;      // 현재 부스트 단계 누적 body0 이동량
    int debugSampleCount = 0;           // 현재 부스트 단계 샘플 프레임 수

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

    // 디버그용 — 씬 Hierarchy 전체 경로 (어느 부모 밑 자식인지 식별)
    string GetFullPath()
    {
        string path = name;
        Transform t = transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }

    void Awake()
    {
        // 디버그 강제 활성화 — Inspector/프리팹의 직렬화 값과 무관하게 켬
        // (튜닝 끝나면 이 줄 제거하고 Inspector 토글 그대로 사용)
        // debugLogSpeeds = true;

        // 디버그 — WormController가 실제로 로드되었는지 확인 + 직렬화 필드 값 출력
        // 인스턴스마다 값이 다른지 비교해 어느 프리팹/오브젝트가 운용 중인지 식별
        // Debug.Log($"[WormSpeed] WormController.Awake (entityID={GetEntityId()}, name={name}) " +
        //           $"speed={speed} touchSpeed={touchSpeed} speedBoostPerLetter={speedBoostPerLetter} " +
        //           $"segmentSpacing={segmentSpacing} debugLogSpeeds={debugLogSpeeds} " +
        //           $"path={GetFullPath()}");

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

        // 새 단어 시작 — 부스트 카운터 리셋 (단어마다 원래 속도로 복귀)
        lettersEaten = 0;

        // 새 단어 시작 시 머리를 화면 중앙(월드 원점)으로 리셋
        transform.position = Vector3.zero;

        // 머리 경로 기록도 초기화 — 이전 단어 경로 따라가지 않도록
        headPath.Clear();

        // 디버그 속도 비교용 직전 위치 캐시 초기화 (새 단어 시작 시 위치 점프로 인한 거짓 양성 방지)
        debugHasPrev = false;
        debugPrevBodyPositions.Clear();

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

    // 정답 글자 1개 먹을 때 호출 — 부스트 카운터 +1 (단어마다 누적, SetupWord에서 리셋)
    public void BoostSpeed()
    {
        lettersEaten++;
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
        // 일시정지(자체 isPaused 또는 글로벌 timeScale=0)면 입력/이동 무시 — PausePanel 위 클릭 차단
        if (isPaused || Time.timeScale == 0f) return;

        HandleTouchInput();
        HandleWallBounce();

        // 정답 글자 누적 부스트 배율 — 0글자=1.0, 1글자=1.15, 2글자=1.30 ...
        float boostMul = 1f + lettersEaten * speedBoostPerLetter;

        // 머리 속도: 터치 중 vs 비터치 (둘 다 동일 배율 적용)
        float headSpeed = (isTouchActive ? touchSpeed : speed) * boostMul;

        // 머리 경로 기록 + 경로 따라 몸통 배치 (코너 컷팅 방지)
        RecordHeadPath();
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

    // 머리의 현재 위치를 경로 기록에 추가하고, 꼬리 끝보다 더 멀리 남은 오래된 점은 잘라냄
    // — 매 프레임 호출, 머리가 거의 안 움직이면 추가 안 함(노이즈/메모리 절약)
    void RecordHeadPath()
    {
        Vector3 head = transform.position;

        if (headPath.Count == 0)
        {
            headPath.Add(head);
            return;
        }

        Vector3 last = headPath[^1];
        // 1e-4 미만 이동은 기록 안 함 (정지/거의 정지 상태)
        if ((head - last).sqrMagnitude < 1e-8f) return;

        headPath.Add(head);

        // 필요한 경로 길이(꼬리 끝 + 여유 1칸) 넘는 오래된 점 제거 — 메모리/탐색 비용 절약
        float needed = (bodySegments.Count + 1) * segmentSpacing;
        float total = 0f;
        int keepFromIdx = 0;
        for (int i = headPath.Count - 1; i > 0; i--)
        {
            total += Vector3.Distance(headPath[i], headPath[i - 1]);
            if (total >= needed) { keepFromIdx = i - 1; break; }
        }
        if (keepFromIdx > 0)
            headPath.RemoveRange(0, keepFromIdx);
    }

    // 몸통이 머리가 실제로 지나간 경로(headPath)를 따라 거리만큼 뒤에 배치됨
    void UpdateBodyFollow()
    {
        if (bodySegments.Count == 0) return;

        Vector3 head = transform.position;

        // 경로 기록 없으면 모든 몸통을 머리 위치에 모음 (단어 시작 직후 자연스러운 등장)
        if (headPath.Count == 0)
        {
            for (int i = 0; i < bodySegments.Count; i++)
                if (bodySegments[i] != null)
                    bodySegments[i].transform.position = head;
            return;
        }

        // 머리에서 시작해 경로를 뒤로 거슬러 올라가며 (i+1)*segmentSpacing 거리에 각 세그먼트 배치
        Vector3 walker = head;
        int pathIdx = headPath.Count - 1;
        float totalDistance = 0f;

        for (int seg = 0; seg < bodySegments.Count; seg++)
        {
            float target = (seg + 1) * segmentSpacing;
            bool placed = false;

            while (pathIdx >= 0)
            {
                Vector3 next = headPath[pathIdx];
                float stepDist = Vector3.Distance(walker, next);

                if (totalDistance + stepDist >= target)
                {
                    // 목표 지점은 walker와 next 사이 — 보간으로 정확한 위치 산출
                    float overshoot = target - totalDistance;
                    float t = stepDist > 0f ? overshoot / stepDist : 0f;
                    Vector3 placedPos = Vector3.Lerp(walker, next, t);

                    if (bodySegments[seg] != null)
                        bodySegments[seg].transform.position = placedPos;

                    // 다음 세그먼트는 이 위치에서 이어서 탐색 (pathIdx는 유지)
                    walker = placedPos;
                    totalDistance = target;
                    placed = true;
                    break;
                }

                // 이 경로 구간 전부 소진하고 더 뒤로 — walker를 next로 이동
                totalDistance += stepDist;
                walker = next;
                pathIdx--;
            }

            if (!placed)
            {
                // 경로가 부족함(아직 머리가 충분히 움직이지 않음) — 가장 오래된 점에 둠
                if (bodySegments[seg] != null)
                    bodySegments[seg].transform.position = walker;
            }
        }

        // ─── 디버그 로깅 ───
        // 직전 프레임 대비 머리/몸통 각 세그먼트의 이동량 계산
        // (각 값 = 이번 프레임 이동 거리 = 실제 속도 × Time.deltaTime)
        // + 부스트 단계 전환 시 직전 단계의 평균 이동량 요약 출력 → 먹기 전/후 비교 용이
        if (debugLogSpeeds)
        {
            float boostMul = 1f + lettersEaten * speedBoostPerLetter;
            float expectedHeadSpeed = (isTouchActive ? touchSpeed : speed) * boostMul;

            // 부스트 단계가 바뀌었는지 확인 — 바뀌었으면 직전 단계 요약 + 전환 마커 출력
            if (debugPrevLettersEaten != lettersEaten)
            {
                if (debugSampleCount > 0)
                {
                    float prevBoostMul = 1f + debugPrevLettersEaten * speedBoostPerLetter;
                    float avgHead = debugHeadDeltaSum / debugSampleCount;
                    float avgBody0 = debugBody0DeltaSum / debugSampleCount;
                    Debug.Log($"[WormSpeed] ===== 부스트 단계 요약: letters={debugPrevLettersEaten} boost={prevBoostMul:F3} | " +
                              $"샘플={debugSampleCount}프레임 평균 headΔ={avgHead:F4} body0Δ={avgBody0:F4} 차이={(avgBody0 - avgHead):+0.0000;-0.0000} =====");
                }

                Debug.Log($"[WormSpeed] ===== 전환: letters {debugPrevLettersEaten} → {lettersEaten} (boost {(1f + debugPrevLettersEaten * speedBoostPerLetter):F3} → {boostMul:F3}) =====");

                // 단계 누적 리셋 (전환 직후부터 새 단계 통계 시작)
                debugPrevLettersEaten = lettersEaten;
                debugHeadDeltaSum = 0f;
                debugBody0DeltaSum = 0f;
                debugSampleCount = 0;
            }

            if (debugHasPrev)
            {
                float headDelta = Vector3.Distance(head, debugPrevHeadPos);

                var sb = new System.Text.StringBuilder();
                sb.Append($"[WormSpeed] frame={Time.frameCount} dt={Time.deltaTime:F4} ");
                sb.Append($"headΔ={headDelta:F4} (expSpd={expectedHeadSpeed:F3}, expΔ={expectedHeadSpeed * Time.deltaTime:F4}) ");
                sb.Append($"letters={lettersEaten} boost={boostMul:F3} touch={isTouchActive} | ");

                float body0Delta = 0f;
                for (int i = 0; i < bodySegments.Count && i < debugPrevBodyPositions.Count; i++)
                {
                    if (bodySegments[i] == null) continue;
                    float bodyDelta = Vector3.Distance(bodySegments[i].transform.position, debugPrevBodyPositions[i]);
                    if (i == 0) body0Delta = bodyDelta;
                    string flag = bodyDelta > headDelta + 1e-4f ? "!" : "";
                    sb.Append($"body{i}Δ={bodyDelta:F4}{flag} ");
                }
                Debug.Log(sb.ToString());

                // 현재 부스트 단계 통계 누적 — 머리가 거의 안 움직인 프레임(정지)은 제외해 평균 왜곡 방지
                if (headDelta > 1e-4f)
                {
                    debugHeadDeltaSum += headDelta;
                    debugBody0DeltaSum += body0Delta;
                    debugSampleCount++;
                }
            }

            // 이번 프레임 위치를 다음 프레임 비교용으로 저장
            debugPrevHeadPos = head;
            debugPrevBodyPositions.Clear();
            for (int i = 0; i < bodySegments.Count; i++)
                debugPrevBodyPositions.Add(bodySegments[i] != null ? bodySegments[i].transform.position : Vector3.zero);
            debugHasPrev = true;
        }
    }
}
