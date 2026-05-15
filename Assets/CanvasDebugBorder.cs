using UnityEngine;
using UnityEngine.UI;

// Canvas 영역을 붉은 테두리로 시각화하는 디버그용 스크립트
// 실제 Canvas 경계를 확인할 때 사용하며 배포 전 제거 필요
public class CanvasDebugBorder : MonoBehaviour
{
    [SerializeField] float borderWidth = 8f;            // 테두리 두께 (픽셀)
    [SerializeField] Color borderColor = Color.red;     // 테두리 색상

    void Start()
    {
        // 상하좌우 4개의 테두리 생성
        CreateStrip("Border_Top",    new Vector2(0, 1), new Vector2(1, 1), true);
        CreateStrip("Border_Bottom", new Vector2(0, 0), new Vector2(1, 0), true);
        CreateStrip("Border_Left",   new Vector2(0, 0), new Vector2(0, 1), false);
        CreateStrip("Border_Right",  new Vector2(1, 0), new Vector2(1, 1), false);
    }

    // 테두리 한 면을 생성하는 메서드
    // horizontal: true=가로(상하), false=세로(좌우)
    void CreateStrip(string stripName, Vector2 anchorMin, Vector2 anchorMax, bool horizontal)
    {
        GameObject go = new GameObject(stripName);
        go.transform.SetParent(transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();

        if (horizontal)
        {
            // 가로 방향 (상단/하단) 테두리
            rt.anchorMin = new Vector2(0, anchorMin.y);
            rt.anchorMax = new Vector2(1, anchorMax.y);
            rt.sizeDelta = new Vector2(0, borderWidth);
            rt.anchoredPosition = new Vector2(0, anchorMin.y == 1 ? -borderWidth / 2 : borderWidth / 2);
        }
        else
        {
            // 세로 방향 (좌측/우측) 테두리
            rt.anchorMin = new Vector2(anchorMin.x, 0);
            rt.anchorMax = new Vector2(anchorMax.x, 1);
            rt.sizeDelta = new Vector2(borderWidth, 0);
            rt.anchoredPosition = new Vector2(anchorMin.x == 0 ? borderWidth / 2 : -borderWidth / 2, 0);
        }

        Image img = go.AddComponent<Image>();
        img.color = borderColor;
    }
}
