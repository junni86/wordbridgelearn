using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// 터치/클릭 위치와 UI 감지 결과를 로그로 출력하는 디버그용 스크립트
// 버튼 터치가 안 될 때 원인 파악에 사용 (배포 전 제거 필요)
public class TouchDebugger : MonoBehaviour
{
    [SerializeField] RectTransform targetButton; // 터치 영역을 확인할 대상 버튼

    void Update()
    {
        Vector2 inputPos = Vector2.zero;
        bool triggered = false;

#if UNITY_EDITOR
        // 에디터에서는 마우스 클릭으로 테스트
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            inputPos = Mouse.current.position.ReadValue();
            triggered = true;
        }
#else
        // 실제 기기에서는 터치 입력 감지
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            inputPos = Touchscreen.current.primaryTouch.position.ReadValue();
            triggered = true;
        }
#endif

        if (!triggered) return;

        // Debug.Log($"[Touch] 입력 위치 (Screen): {inputPos}");

        // 입력 위치가 대상 버튼의 RectTransform 영역 안에 있는지 확인
        if (targetButton != null)
        {
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(targetButton, inputPos, null);
            // Debug.Log($"[Result] 버튼 안에 입력됨: {inside}");
        }

        // EventSystem RaycastAll로 실제 감지된 UI 요소 목록 출력
        // 클릭을 가로채는 오브젝트가 있는지 확인할 때 사용
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = inputPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count == 0)
        {
            Debug.Log("[Raycast] 감지된 UI 없음");
        }
        else
        {
            for (int i = 0; i < results.Count; i++)
                Debug.Log($"[Raycast #{i}] {results[i].gameObject.name} (depth: {results[i].depth})");
        }
    }
}
