using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

// 지렁이 몸통 한 칸 — 빈 몸통 스프라이트(또는 머리 스프라이트) + 글자 TMP
// letterText 슬롯이 비어있으면 Awake에서 자동으로 3D TextMeshPro 자식을 생성한다.
// (알파벳 버블과 동일한 패턴 — UGUI 버전 실수로 Canvas 종속이 생기는 것을 차단)
public class WormSegment : MonoBehaviour
{
    [Header("시각 컴포넌트")]
    public SpriteRenderer spriteRenderer; // 몸통/머리 배경 스프라이트
    public TMP_Text letterText;           // 글자 표시 TMP (비어있으면 Awake에서 자동 생성)

    [Header("자동 생성 시 글자 옵션")]
    [Tooltip("letterText가 비어있을 때 자동 생성되는 TMP의 폰트 크기")]
    [SerializeField] float fontSize = 74f;
    [Tooltip("letterText가 비어있을 때 자동 생성되는 TMP의 글자 색")]
    [SerializeField] Color textColor = Color.white;
    [Tooltip("선택 — 비워두면 TMP 기본 폰트 사용")]
    [SerializeField] TMP_FontAsset fontAsset;

    void Awake()
    {
        // letterText가 비어있거나 UGUI(Canvas 종속) 버전이면 3D TextMeshPro 자식을 새로 생성
        if (letterText == null || letterText is TextMeshProUGUI)
            letterText = CreateLetterTextChild();

        // SortingGroup 추가 — sprite + 글자를 하나의 단위로 묶어 렌더링
        // 다른 몸통과 겹칠 때도 자기 sprite 와 같은 레이어에 글자가 표시됨 (알파벳 버블과 동일 패턴)
        if (GetComponent<SortingGroup>() == null)
            gameObject.AddComponent<SortingGroup>();
    }

    // AlphabetSpawner.SpawnOne 과 동일한 방식으로 3D TextMeshPro 컴포넌트를 자식에 동적 추가
    TMP_Text CreateLetterTextChild()
    {
        GameObject textObj = new GameObject("LetterText");
        textObj.transform.SetParent(transform, false);
        // 스프라이트(sortingOrder 0) 보다 살짝 앞에 놓아 가려지지 않게
        textObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.text = string.Empty;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;

        // 스프라이트 위에 글자가 그려지도록 sortingOrder 명시
        var rd = tmp.GetComponent<Renderer>();
        if (rd != null) rd.sortingOrder = 1;

        return tmp;
    }

    // 글자 표시 변경 (빈 칸이면 빈 문자열)
    public void SetLetter(string letter)
    {
        if (letterText != null)
            letterText.text = letter ?? string.Empty;
    }

    // 스프라이트 교체 (머리/빈 몸통 등)
    public void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null)
            spriteRenderer.sprite = sprite;
    }

    // SortingGroup의 정렬 우선순위 변경 — 값이 클수록 화면 위에 표시
    // (Awake에서 자동 추가된 SortingGroup이 sprite + 글자를 묶음 단위로 렌더링)
    public void SetSortingOrder(int order)
    {
        var sg = GetComponent<SortingGroup>();
        if (sg != null) sg.sortingOrder = order;
    }
}
