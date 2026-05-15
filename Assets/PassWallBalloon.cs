using UnityEngine;

// PassWall 미니게임용 떨어지는 물풍선 1개
// - 위에서 아래로 등속 이동 (Rigidbody2D.linearVelocity 사용)
// - BottomWall 또는 화면 하단 Y 도달 시 자동 소멸
// - 캐릭터(Trigger)와 충돌 시 정답/오답 판정은 PassWallCharacter에서 수행
[RequireComponent(typeof(Rigidbody2D))]
public class PassWallBalloon : MonoBehaviour
{
    [System.NonSerialized] public string letter = string.Empty; // 풍선이 가진 글자
    [System.NonSerialized] public float fallSpeed = 1.2f;       // 하강 속도 (Spawner가 주입)

    // 캐릭터가 이미 한 번 처리한 풍선인지 — 같은 풍선에 다중 판정 방지
    [System.NonSerialized] public bool consumed = false;

    Rigidbody2D rb;

    // 실시간 스케일/위치 재계산용 — 스포너 참조와 캐시
    PassWallSpawner spawner;
    SpriteRenderer cachedSr;
    float spriteWidth = 1f;
    int slotIndex = -1;        // 행 내 슬롯 인덱스 (X 재계산용)
    float lastFullW = -1f;
    float lastRatio = -1f;
    float lastGapRatio = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        // 캐릭터가 풍선을 그냥 통과해야 하므로 Kinematic + Trigger 운용
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Collider들을 trigger로 강제 — 캐릭터가 통과 가능하도록
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.isTrigger = true;
    }

    void Start()
    {
        // 풍선은 단순 등속 하강 — 좌우 흔들림 없음
        rb.linearVelocity = new Vector2(0f, -fallSpeed);
    }

    void Update()
    {
        // 화면 하단 아래로 내려가면 풍선 소멸 (BottomWall 트리거가 누락된 경우 안전망)
        float hh = ScreenWallFitter.HalfH;
        if (hh > 0f && transform.position.y < -hh - 0.5f)
        {
            Destroy(gameObject);
            return;
        }

        // 화면 폭, bubbleWidthRatio, bubbleGapRatio 중 하나라도 바뀌면 스케일/X 재적용
        // (회전, 해상도 변경, 인스펙터에서 비율 조정 등 실시간 대응)
        if (spawner == null) return;
        float fullW = ScreenWallFitter.HalfW * 2f;
        float ratio = spawner.BubbleWidthRatio;
        float gapRatio = spawner.BubbleGapRatio;
        if (!Mathf.Approximately(fullW, lastFullW) ||
            !Mathf.Approximately(ratio, lastRatio) ||
            !Mathf.Approximately(gapRatio, lastGapRatio))
        {
            ApplyLayout(fullW, ratio, gapRatio);
        }
    }

    // 현재 화면 폭/비율로 풍선 스케일 + X 위치 재계산 (Spawner.ComputeSlotX와 일치)
    void ApplyLayout(float fullW, float ratio, float gapRatio)
    {
        if (fullW <= 0f || spawner.SlotsPerRow <= 0 || spriteWidth <= 0f) return;

        // 크기: balloon_width = (fullW / N) * bubbleWidthRatio
        float balloonWidth = (fullW / spawner.SlotsPerRow) * ratio;
        float scale = balloonWidth / spriteWidth;
        transform.localScale = Vector3.one * scale;

        // X 위치: 슬롯 인덱스가 유효하면 새 레이아웃으로 재정렬 (여백 변경 반영)
        if (slotIndex >= 0)
        {
            float x = spawner.ComputeSlotX(slotIndex, fullW);
            Vector3 p = transform.position;
            p.x = x;
            transform.position = p;
        }

        lastFullW = fullW;
        lastRatio = ratio;
        lastGapRatio = gapRatio;
    }

    // 풍선의 글자를 외부에서 설정 (Spawner가 호출)
    public void Init(string assignedLetter, float speed, PassWallSpawner ownerSpawner, int assignedSlotIndex)
    {
        letter = assignedLetter ?? string.Empty;
        fallSpeed = speed;
        spawner = ownerSpawner;
        slotIndex = assignedSlotIndex;

        // 스프라이트 원본 폭 캐시 — 매 프레임 GetComponent 호출 회피
        cachedSr = GetComponentInChildren<SpriteRenderer>();
        if (cachedSr != null && cachedSr.sprite != null)
            spriteWidth = cachedSr.sprite.bounds.size.x;
    }
}
