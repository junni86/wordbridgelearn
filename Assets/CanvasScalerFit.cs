using UnityEngine;
using UnityEngine.UI;

// 화면 비율에 따라 Canvas Scaler의 Match 값을 자동으로 조절하는 스크립트
// 폴드폰처럼 가로가 넓은 화면에서는 GameArea 가로 폭을 제한해 콘텐츠가 잘리지 않게 함
[RequireComponent(typeof(CanvasScaler))]
public class CanvasScalerFit : MonoBehaviour
{
    [SerializeField] float targetWidth = 1080f;         // 기준 가로 해상도
    [SerializeField] float targetHeight = 1920f;        // 기준 세로 해상도
    [SerializeField] float maxAspectRatio = 0.65f;      // 이 비율 초과 시 GameArea 가로 제한
    [SerializeField] RectTransform gameArea;            // 콘텐츠 영역 컨테이너

    CanvasScaler scaler;
    int prevWidth;          // 이전 프레임 화면 가로
    int prevHeight;         // 이전 프레임 화면 세로
    bool firstFrame = true; // 첫 프레임 여부 (LateUpdate에서 초기 적용용)

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
    }

    void LateUpdate()
    {
        // 첫 프레임이거나 화면 크기가 변경됐을 때만 재계산 (폴드폰 펼치기/접기 대응)
        if (firstFrame || Screen.width != prevWidth || Screen.height != prevHeight)
        {
            firstFrame = false;
            ApplyFit();
        }
    }

    void ApplyFit()
    {
        prevWidth = Screen.width;
        prevHeight = Screen.height;

        float targetAspect = targetWidth / targetHeight;
        float screenAspect = (float)Screen.width / Screen.height;

        // 화면이 기준보다 세로로 길면 가로 기준, 아니면 세로 기준으로 스케일
        if (screenAspect < targetAspect)
        {
            scaler.matchWidthOrHeight = 0f; // 가로 기준
            Debug.Log($"[CanvasScalerFit] 가로 기준 (Match=0) | screenAspect={screenAspect:F3}");
        }
        else
        {
            scaler.matchWidthOrHeight = 1f; // 세로 기준
            Debug.Log($"[CanvasScalerFit] 세로 기준 (Match=1) | screenAspect={screenAspect:F3}");
        }

        if (gameArea == null) return;

        // 화면 비율이 maxAspectRatio 초과 시 GameArea 가로 폭 제한 (양옆 여백 생성)
        if (screenAspect > maxAspectRatio)
        {
            float limitedWidth = targetHeight * maxAspectRatio; // 제한된 가로 폭 (Canvas 단위)
            gameArea.anchorMin = new Vector2(0.5f, 0f);
            gameArea.anchorMax = new Vector2(0.5f, 1f);
            gameArea.sizeDelta = new Vector2(limitedWidth, 0f);
            gameArea.anchoredPosition = Vector2.zero;
            Debug.Log($"[CanvasScalerFit] 가로 제한 적용 | limitedWidth={limitedWidth:F0}");
        }
        else
        {
            // 일반 화면은 GameArea가 Canvas 전체를 채움
            gameArea.anchorMin = Vector2.zero;
            gameArea.anchorMax = Vector2.one;
            gameArea.sizeDelta = Vector2.zero;
            gameArea.anchoredPosition = Vector2.zero;
            Debug.Log($"[CanvasScalerFit] 가로 제한 없음 | screenAspect={screenAspect:F3}");
        }

        // RectTransform 변경 후 레이아웃 강제 재계산 (터치 영역 불일치 방지)
        Canvas.ForceUpdateCanvases();
    }
}
