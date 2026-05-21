using UnityEngine;

// iOS 노치/다이나믹 아일랜드/홈 인디케이터 및 Android 펀치홀/제스처 바를 피해
// UI 가 안전 영역(Screen.safeArea) 안에서만 표시되도록 RectTransform 의 anchor 를 자동 조정한다.
//
// 사용법:
//   1. Canvas 아래에 빈 GameObject 를 만든다 (예: "SafeArea")
//   2. 이 컴포넌트를 부착한다
//   3. 기존 콘텐츠(GameArea 등)를 이 오브젝트의 자식으로 둔다
//
// 회전/폴드 등 화면 변경에도 자동 대응하며, 안전 영역이 전체 화면과 같은 기기에서는
// anchor 가 (0,0)~(1,1) 로 유지되어 사실상 아무 동작도 하지 않으므로 안전하다.
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rt;          // 이 컴포넌트가 붙은 RectTransform
    Rect lastSafeArea;         // 마지막으로 적용한 안전 영역 (변경 감지용)
    Vector2Int lastScreenSize; // 마지막 화면 크기 (회전/폴드 감지용)

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        // 첫 프레임에서 즉시 적용되도록 강제 — 초기값을 의도적으로 어긋나게 설정
        lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        lastScreenSize = new Vector2Int(-1, -1);
        ApplySafeArea();
    }

    void Update()
    {
        // 안전 영역 또는 화면 크기가 변경됐을 때만 재계산 (성능 절약)
        // 화면 회전/폴드폰 펼치기/접기/스플릿뷰 진입 등의 상황에 대응
        if (Screen.safeArea != lastSafeArea ||
            Screen.width != lastScreenSize.x ||
            Screen.height != lastScreenSize.y)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        // 현재 화면 크기가 0 이면 (앱 백그라운드 등) 스킵 — 0 나누기 방지
        if (Screen.width <= 0 || Screen.height <= 0) return;

        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        // Screen.safeArea 는 픽셀 단위 사각형(좌하단 원점)
        // anchor 는 0~1 정규화 좌표이므로 화면 크기로 나눠서 변환
        Vector2 anchorMin = lastSafeArea.position;
        Vector2 anchorMax = lastSafeArea.position + lastSafeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // 부동소수점 오차로 0~1 범위를 살짝 벗어나는 경우 방지
        anchorMin.x = Mathf.Clamp01(anchorMin.x);
        anchorMin.y = Mathf.Clamp01(anchorMin.y);
        anchorMax.x = Mathf.Clamp01(anchorMax.x);
        anchorMax.y = Mathf.Clamp01(anchorMax.y);

        // RectTransform 을 안전 영역에 정확히 맞춤 (offset 은 0 으로 초기화하여 패딩 없음)
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Debug.Log($"[SafeAreaFitter] applied | safeArea={lastSafeArea} | screen={Screen.width}x{Screen.height}");
    }
}
