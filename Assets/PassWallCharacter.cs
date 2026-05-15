using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// PassWall 미니게임 캐릭터
// - 화면 하단 부근 Y 고정, X만 좌우로 이동
// - 터치/마우스 X 좌표를 추적 (Lerp)
// - 떨어지는 풍선(PassWallBalloon)과 Trigger 충돌 시 호스트로 글자 전달
// - 오답이면 짧게 정지(풍선은 그대로 통과)
[RequireComponent(typeof(Collider2D))]
public class PassWallCharacter : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("따라가는 속도 (Lerp 비율 — 클수록 빠르게 추격)")]
    public float followSpeed = 12f;

    [Tooltip("화면 하단에서 캐릭터 Y 위치 오프셋 (양수일수록 위로)")]
    public float bottomOffsetY = 1.0f;

    [Tooltip("오답 시 정지 시간 (초)")]
    public float wrongPauseDuration = 0.3f;

    [Header("크기 (화면 비율)")]
    [Tooltip("캐릭터 폭이 화면 가로의 몇 %를 차지할지 (0~1). 0.12 = 화면 폭의 12%")]
    [Range(0.02f, 0.5f)] public float widthRatio = 0.119f;

    bool isPaused;
    float targetX;

    // 마지막으로 스케일을 적용했을 때의 기준값 — 변경 감지용
    float lastAppliedFullW = -1f;
    float lastAppliedRatio = -1f;

    void Awake()
    {
        // 캐릭터 trigger collider — 풍선이 그대로 통과 가능하도록
        foreach (var col in GetComponents<Collider2D>())
            col.isTrigger = true;
    }

    void Start()
    {
        // 화면 가로 폭 대비 widthRatio가 되도록 자기 스케일 계산
        // 스프라이트 원본 폭(scale=1 기준)으로 환산 — 어떤 폰/태블릿에서도 동일 비율
        ApplyScreenRelativeScale();

        // 시작 시 화면 하단 부근으로 위치 고정
        float hh = ScreenWallFitter.HalfH;
        float y = -hh + bottomOffsetY;
        transform.position = new Vector3(0f, y, 0f);
        targetX = 0f;
    }

    // SpriteRenderer 원본 폭과 화면 폭(HalfW×2)으로 스케일 계산
    void ApplyScreenRelativeScale()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        float fullW = ScreenWallFitter.HalfW * 2f;
        if (fullW <= 0f && Camera.main != null)
            fullW = Camera.main.orthographicSize * Camera.main.aspect * 2f;
        if (fullW <= 0f) return;

        float spriteWidth = sr.sprite.bounds.size.x;
        if (spriteWidth <= 0f) return;

        float scale = (fullW * widthRatio) / spriteWidth;
        transform.localScale = Vector3.one * scale;

        // 캐시 갱신 — Update에서 변경 감지에 사용
        lastAppliedFullW = fullW;
        lastAppliedRatio = widthRatio;
    }

    void Update()
    {
        // 화면 폭 또는 widthRatio가 바뀌었으면 스케일 재적용
        // (회전, 해상도 변경, 인스펙터에서 widthRatio 조정 등에 실시간 대응)
        float currentFullW = ScreenWallFitter.HalfW * 2f;
        if (!Mathf.Approximately(currentFullW, lastAppliedFullW) ||
            !Mathf.Approximately(widthRatio, lastAppliedRatio))
        {
            ApplyScreenRelativeScale();
        }

        if (isPaused) return;

        // 터치/마우스 X 좌표 읽기 — 누르고 있는 동안만 추적
        Vector2? screenPos = null;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            screenPos = Mouse.current.position.ReadValue();

        if (screenPos.HasValue && Camera.main != null)
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(
                new Vector3(screenPos.Value.x, screenPos.Value.y, 0f));
            targetX = world.x;
        }

        // 좌우 경계 안에 클램프
        float hw = ScreenWallFitter.HalfW;
        if (hw > 0f) targetX = Mathf.Clamp(targetX, -hw, hw);

        // Y는 고정, X만 Lerp
        Vector3 pos = transform.position;
        float hh = ScreenWallFitter.HalfH;
        float y = hh > 0f ? -hh + bottomOffsetY : pos.y;
        pos.x = Mathf.Lerp(pos.x, targetX, followSpeed * Time.deltaTime);
        pos.y = y;
        transform.position = pos;
    }

    // 풍선과 충돌 — Trigger 사용 (캐릭터·풍선 양쪽 모두 trigger)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isPaused) return;
        // 풍선이 자신의 자식 collider일 수도 있어 부모 쪽도 검사
        PassWallBalloon balloon = other.GetComponent<PassWallBalloon>();
        if (balloon == null) balloon = other.GetComponentInParent<PassWallBalloon>();
        if (balloon == null || balloon.consumed) return;

        if (PassWallMiniGame.Instance == null) return;

        balloon.consumed = true; // 중복 판정 방지
        PassWallMiniGame.Instance.ReportCollision(balloon.letter, balloon);
    }

    // 오답 시 호스트에서 호출 — 짧게 정지
    public void PauseMovement()
    {
        StartCoroutine(PauseCoroutine(wrongPauseDuration));
    }

    IEnumerator PauseCoroutine(float duration)
    {
        isPaused = true;
        yield return new WaitForSeconds(duration);
        isPaused = false;
    }
}
