using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 미니게임 공통 인터페이스 — 단일 씬에서 GameHostManager가 활성 미니게임을 교체하며 사용
// 각 미니게임은 입력 방식과 시각 표현만 책임지고, 단어 검증/점수/스테이지 등 룰은 호스트가 담당
public abstract class MiniGameBase : MonoBehaviour
{
    // 새 단어가 시작될 때 호스트가 호출
    // - answer: 현재 언어 모드의 정답 단어
    // - koreanCharPool: KR 모드일 때 랜덤 채움용 글자 풀 (EN 모드면 무시)
    // - isKorean: 현재 언어가 KR인지
    public abstract void BeginWord(string answer, List<string> koreanCharPool, bool isKorean);

    // 미니게임이 비활성화될 때(다른 미니게임으로 교체) 정리
    public abstract void Cleanup();

    // 정답 글자 입력 후 호스트가 호출 — 미니게임은 정답 시각 효과(파티클·몸통 채움 등) 처리
    // - source: 입력 발생시킨 오브젝트 (버블 등) — null 가능
    // - filledIndex: 0-base, 방금 채워진 글자 위치
    // - letter: 채워진 글자
    public abstract void HandleCorrect(MonoBehaviour source, int filledIndex, string letter);

    // 오답 입력 후 호스트가 호출 — 미니게임은 오답 시각 효과(X마크·정지 등) 처리
    // - source: 입력 발생시킨 오브젝트 — null 가능
    // - expected: 기대된 정답 글자
    public abstract void HandleWrong(MonoBehaviour source, string expected);

    // 미니게임 내부에서 입력이 들어오면 호스트로 위임 (자식 클래스가 호출)
    protected void NotifyLetterTyped(string letter, MonoBehaviour source)
    {
        if (GameHostManager.Instance != null)
            GameHostManager.Instance.OnLetterTyped(letter, source);
    }

    // 힌트 표시 — 지금 입력해야 하는 다음 한 글자(nextLetter)에 해당하는 물방울 1개를
    // duration 초 동안 1.5배로 키웠다 원복. 같은 글자 풍선이 화면에 여러 개 있어도 첫 매칭 1개만 강조.
    // 각 미니게임은 자기 자식 중 글자 컴포넌트를 찾아 첫 매칭의 transform 1개를
    // AnimateHintHighlight 헬퍼에 전달하면 공통 보간 처리됨
    public virtual void ShowHint(string nextLetter, float duration) { }

    // 공통 헬퍼 — 대상 transform들을 grow → hold → shrink 패턴으로 키웠다 원복
    // duration 양 끝의 0.2초는 자연스러운 보간 구간
    protected IEnumerator AnimateHintHighlight(List<Transform> targets, float duration, float scaleMul = 1.5f)
    {
        if (targets == null || targets.Count == 0) yield break;

        const float EASE = 0.2f; // grow/shrink 보간 구간 길이
        Dictionary<Transform, Vector3> baseScales = new();
        foreach (var t in targets)
            if (t != null) baseScales[t] = t.localScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float k;
            if (elapsed < EASE)
                k = elapsed / EASE;
            else if (elapsed > duration - EASE)
                k = (duration - elapsed) / EASE;
            else
                k = 1f;

            float mul = Mathf.Lerp(1f, scaleMul, Mathf.Clamp01(k));
            foreach (var pair in baseScales)
                if (pair.Key != null) pair.Key.localScale = pair.Value * mul;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 정확히 원복
        foreach (var pair in baseScales)
            if (pair.Key != null) pair.Key.localScale = pair.Value;
    }
}
