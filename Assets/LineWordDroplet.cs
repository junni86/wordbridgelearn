using System.Collections;
using UnityEngine;

// LineWord 미니게임의 격자 셀 1개 — 단순 데이터 + 시각 효과 컴포넌트
// - 그리드 좌표(col,row), 글자, 정답 여부를 저장
// - HandleCorrect 호출 시 sortingOrder를 올리고 살짝 커진 상태로 유지 (앞으로 나오는 느낌)
// - 이미 정답 처리된 셀은 consumed=true 로 잠겨 같은 입력이 중복 발생하지 않도록 함
public class LineWordDroplet : MonoBehaviour
{
    [System.NonSerialized] public string letter = string.Empty; // 셀에 표시된 글자
    [System.NonSerialized] public int col = -1;                 // 그리드 열 (0-based, 좌→우)
    [System.NonSerialized] public int row = -1;                 // 그리드 행 (0-based, 위→아래)
    [System.NonSerialized] public bool isAnswerCell = false;    // 정답 라인에 포함된 셀인지 (디버그/색 강조용)
    [System.NonSerialized] public bool consumed = false;        // 이미 한 번 입력으로 소비된 셀인지

    [Tooltip("정답 시 확대 배율 (현재 크기 × 이 값)")]
    [Range(1.0f, 2.0f)] public float correctScaleMul = 1.5f;

    [Tooltip("확대 보간 시간 (초)")]
    public float correctScaleDuration = 0.12f;

    Vector3 baseScale;
    SpriteRenderer cachedSr;
    Coroutine scaleCoroutine;

    // Spawner가 셀 생성 직후 호출 — 데이터 + 기준 스케일 캐시
    public void Init(string assignedLetter, int assignedCol, int assignedRow, bool answerCell)
    {
        letter = assignedLetter ?? string.Empty;
        col = assignedCol;
        row = assignedRow;
        isAnswerCell = answerCell;
        baseScale = transform.localScale;
        cachedSr = GetComponentInChildren<SpriteRenderer>();
    }

    // Spawner가 화면 비율 변경에 따라 스케일을 재적용할 때 호출 — 기준 스케일 갱신
    public void UpdateBaseScale(Vector3 newBase)
    {
        baseScale = newBase;
        // 이미 정답으로 확대된 상태라면 새 기준에 맞춰 확대 비율 유지
        if (consumed)
            transform.localScale = baseScale * correctScaleMul;
        else
            transform.localScale = baseScale;
    }

    // 정답 입력 시 호출 — 앞으로 나오는(sortingOrder↑) 효과 + 살짝 확대 유지
    public void PlayCorrectEffect()
    {
        consumed = true;

        // 같은 셀이 형제 셀들 위에 그려지도록 sortingOrder 상승
        if (cachedSr != null)
            cachedSr.sortingOrder += 10;

        // 자식 TMP_Text 등 다른 렌더러도 함께 올려줌 (글자가 가려지지 않도록)
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == cachedSr) continue;
            r.sortingOrder += 10;
        }

        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleTo(baseScale * correctScaleMul, correctScaleDuration));
    }

    IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            transform.localScale = Vector3.LerpUnclamped(start, target, k);
            yield return null;
        }
        transform.localScale = target;
    }
}
