using UnityEngine;

// 물방울 터지는 효과 스크립트
// 이 컴포넌트가 붙은 오브젝트가 생성되면 작은 물방울들이 사방으로 퍼지며 사라짐
// GameManager의 dropletPrefab에 연결해서 사용
public class DropletBurst : MonoBehaviour
{
    [Header("물방울 설정")]
    [SerializeField] int dropletCount = 12;             // 생성할 물방울 개수
    [SerializeField] float speed = 4f;                // 물방울 퍼지는 속도
    [SerializeField] float duration = 0.45f;           // 효과 지속 시간 (초)
    [SerializeField] float startSize = 0.3f;          // 물방울 시작 크기

    [Header("스프라이트 설정")]
    [SerializeField] Sprite dropletSprite;              // 물방울 스프라이트 (없으면 흰 원)
    [SerializeField] Color dropletColor = new Color(0.4f, 0.8f, 1f, 1f); // 물방울 색상 (하늘색)

    // 벽 사이 간격 기반 배율: LeftWall ~ RightWall 안쪽 가로에 평균 15개의 물방울이 들어가도록 크기 조정
    // (속도에도 동일 배율을 곱해 확산 거리도 함께 비례)
    float bgScale = 1f;

    // 벽 사이에 들어갈 물방울 개수 — 기준값. 이 값을 줄이면 물방울이 커지고, 늘리면 작아짐
    const int DropletsAcrossWalls = 15;

    void Start()
    {
        // ScreenWallFitter.HalfW는 leftWall/rightWall 안쪽 경계 사이 거리의 절반
        // (HalfW * 2 = 좌·우 벽 안쪽 사이 가로 간격)
        float wallGap = ScreenWallFitter.HalfW * 2f;
        float spriteWidth = dropletSprite != null ? dropletSprite.bounds.size.x : 0f;

        if (wallGap > 0f && spriteWidth > 0f && startSize > 0f)
        {
            // 평균 물방울 월드 가로 = wallGap / N 이 되도록 startSize에 곱할 배율을 계산
            // 결과적으로 localScale = startSize * bgScale, 월드 너비 = spriteWidth * localScale
            bgScale = wallGap / (DropletsAcrossWalls * spriteWidth * startSize);
        }
        else
        {
            Debug.LogWarning($"[DropletBurst] wallGap({wallGap}) 또는 spriteWidth({spriteWidth}) 비정상 → 기본 스케일(1) 사용");
        }

        // 360도를 dropletCount로 나눠 균등한 각도로 물방울 생성 (각도에 랜덤 편차 추가)
        for (int i = 0; i < dropletCount; i++)
        {
            float angle = i * (360f / dropletCount);
            angle += Random.Range(-15f, 15f); // 랜덤 각도 편차 (-15~+15도)
            SpawnDroplet(angle, i);
        }

        // 효과가 끝나면 부모 오브젝트 제거
        Destroy(gameObject, duration);
    }

    // 특정 각도 방향으로 물방울 하나 생성
    void SpawnDroplet(float angle, int index)
    {
        GameObject drop = new GameObject($"Droplet_{index}");
        // 위치와 랜덤 Z축 회전을 한 번에 설정
        drop.transform.SetPositionAndRotation(
            transform.position,
            Quaternion.Euler(0, 0, Random.Range(0, 360))
        );

        // 스프라이트 렌더러 추가 (초기 알파 0.9로 약간 투명하게 시작)
        SpriteRenderer sr = drop.AddComponent<SpriteRenderer>();
        sr.sprite = dropletSprite;
        sr.color = new Color(dropletColor.r, dropletColor.g, dropletColor.b, 0.9f);
        sr.sortingOrder = 20; // 다른 오브젝트 위에 렌더링

        // 랜덤 크기 적용 (startSize의 0.7~1.3배) — 배경 스케일을 곱해 화면 비율에 맞춤
        float randomSize = startSize * bgScale * Random.Range(0.7f, 1.3f);
        drop.transform.localScale = Vector3.one * randomSize;

        // 각도를 방향 벡터로 변환
        float rad = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // 물방울마다 속도 랜덤화 (기본 속도의 0.7~1.3배) — 배경 스케일을 곱해 퍼지는 범위도 화면 비율에 비례
        float randomSpeed = speed * bgScale * Random.Range(0.7f, 1.3f);

        // 개별 물방울 이동/페이드 컴포넌트 초기화 (randomSize를 시작 크기로 전달)
        DropletParticle particle = drop.AddComponent<DropletParticle>();
        particle.Init(direction, randomSpeed, duration, randomSize);
    }
}

// 개별 물방울의 이동, 축소, 페이드 아웃을 처리하는 내부 컴포넌트
public class DropletParticle : MonoBehaviour
{
    Vector2 direction;  // 이동 방향
    float speed;        // 이동 속도
    float duration;     // 지속 시간
    float startSize;    // 시작 크기
    float elapsed;      // 경과 시간
    SpriteRenderer sr;

    // 외부에서 초기값 주입
    public void Init(Vector2 dir, float spd, float dur, float size)
    {
        direction = dir;
        speed = spd;
        duration = dur;
        startSize = size;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration; // 0 → 1 진행도

        // 바깥 방향으로 이동
        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        // 크기 축소 (startSize → 0)
        transform.localScale = Vector3.one * Mathf.Lerp(startSize, 0f, t);

        // 투명도 감소 (1 → 0)
        Color c = sr.color;
        c.a = Mathf.Lerp(1f, 0f, t);
        sr.color = c;

        // 지속 시간이 끝나면 제거
        if (elapsed >= duration)
            Destroy(gameObject);
    }
}
