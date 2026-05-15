using System.Collections;
using UnityEngine;

public class RandomMove : MonoBehaviour
{
    // NonSerialized: 코드 기본값이 항상 적용됨 (Inspector 저장값 무시)
    [System.NonSerialized] public float speed = 1.3f;

    Rigidbody2D rb;
    Vector2 moveDir;
    bool isPaused; // 일시정지 상태 — true 동안 속도 갱신·반사 스킵

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError($"{gameObject.name}에 Rigidbody2D가 없습니다.");
            return;
        }

        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 마찰력 제거 - 벽에 붙어 미끄러지는 현상 방지
        rb.sharedMaterial = new PhysicsMaterial2D { friction = 0f, bounciness = 0f };
    }

    void Start()
    {
        if (rb == null) return;

        moveDir = Random.insideUnitCircle.normalized;
        rb.linearVelocity = moveDir * speed;
    }

    void Update()
    {
        Vector3 pos = transform.position;
        float hw = ScreenWallFitter.HalfW;
        float hh = ScreenWallFitter.HalfH;

        if (hw == 0f || hh == 0f) return;

        float topBound = ScreenWallFitter.TopBoundaryY > 0 ? ScreenWallFitter.TopBoundaryY : hh;

        if (pos.x > hw) { pos.x = hw; moveDir.x = -Mathf.Abs(moveDir.x); }
        else if (pos.x < -hw) { pos.x = -hw; moveDir.x = Mathf.Abs(moveDir.x); }

        // topWall 위로 넘어가지 않도록 상단 경계 제한
        if (pos.y > topBound) { pos.y = topBound; moveDir.y = -Mathf.Abs(moveDir.y); }
        else if (pos.y < -hh) { pos.y = -hh; moveDir.y = Mathf.Abs(moveDir.y); }

        transform.position = pos;
        // 일시정지 중에는 속도 갱신을 건너뛰어 정지 상태 유지
        if (!isPaused) rb.linearVelocity = moveDir * speed;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (rb == null || collision.contacts.Length == 0) return;
        // 일시정지 중에는 충돌 반사 무시 (정지 상태 유지)
        if (isPaused) return;

        Vector2 normal = collision.contacts[0].normal;
        moveDir = Vector2.Reflect(moveDir, normal).normalized;
        rb.linearVelocity = moveDir * speed;
    }

    // 오답 시 일정 시간 정지 후 다시 이동
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
        // 동일한 방향으로 다시 이동 시작
        if (rb != null) rb.linearVelocity = moveDir * speed;
    }
}
