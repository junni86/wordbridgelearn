using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 지렁이 미니게임
// - WormGameSpawner: 정답 글자 + 랜덤 10개 (절반 크기) 버블 생성
// - WormController: 머리 자동 이동, 터치 추적, 몸통 따라오기
// - 머리가 버블에 부딪히면 NotifyLetterTyped로 호스트에 입력 전달
public class WormMiniGame : MiniGameBase
{
    public static WormMiniGame Instance { get; private set; }

    [Header("스포너 / 컨트롤러")]
    public WormGameSpawner spawner; // 자식 또는 동일 GameObject에 부착
    public WormController worm;     // 자식으로 배치된 지렁이 머리 GameObject

    [Header("이펙트 프리팹")]
    public GameObject dropletPrefab;     // 정답 시 파티클
    public GameObject wrongMarkPrefab;   // 오답 X 표시

    // 오답 쿨다운 중인 버블 — X 마크 표시되는 0.5초 동안 중복 충돌 무시 (점수 다중 차감 방지)
    readonly HashSet<int> wrongCooldownIds = new();
    const float WRONG_COOLDOWN = 0.5f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 새 단어 시작 — 버블 + 지렁이 몸통 모두 재생성
    public override void BeginWord(string answer, List<string> koreanCharPool, bool isKorean)
    {
        if (spawner != null)
        {
            if (isKorean) spawner.SpawnAllKorean(answer, koreanCharPool);
            else          spawner.SpawnAll(answer);
        }

        if (worm != null) worm.SetupWord(answer);
    }

    public override void Cleanup()
    {
        if (spawner != null) spawner.DestroyAll();
        // 지렁이 자체는 프리팹 단위로 Destroy 시 자식과 함께 사라짐
    }

    // 정답: 파티클 + 버블 제거 + 지렁이 몸통에 글자 채움 + 먹은 버블 스프라이트로 몸통 교체
    public override void HandleCorrect(MonoBehaviour source, int filledIndex, string letter)
    {
        // 버블의 스프라이트를 먼저 추출 (Destroy 전에 캡처)
        Sprite bubbleSprite = null;
        if (source != null)
        {
            var sr = source.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) bubbleSprite = sr.sprite;
        }

        if (worm != null) worm.FillLetterAt(filledIndex, letter, bubbleSprite);

        if (source != null)
        {
            if (dropletPrefab != null)
                Instantiate(dropletPrefab, source.transform.position, Quaternion.identity);
            Destroy(source.gameObject);
        }
    }

    // 오답: 버블 0.5초 정지 + X 마크. 지렁이는 멈추지 않고 계속 이동
    public override void HandleWrong(MonoBehaviour source, string expected)
    {
        if (source != null)
        {
            if (source.TryGetComponent(out RandomMove move))
                move.Pause(0.5f);
            ShowWrongMark(source.transform);

            // 쿨다운 등록 — 같은 버블 재충돌 시 ReportCollision에서 무시됨
            int id = source.gameObject.GetInstanceID();
            wrongCooldownIds.Add(id);
            StartCoroutine(ClearWrongCooldown(id));
        }
    }

    IEnumerator ClearWrongCooldown(int id)
    {
        yield return new WaitForSeconds(WRONG_COOLDOWN);
        wrongCooldownIds.Remove(id);
    }

    void ShowWrongMark(Transform parent)
    {
        if (wrongMarkPrefab == null || parent == null) return;
        GameObject mark = Instantiate(wrongMarkPrefab, parent);
        Destroy(mark, WRONG_COOLDOWN);
    }

    // WormController에서 호출 — 입력을 호스트로 전달
    // 단, 오답 쿨다운 중인 버블이면 무시 (X 마크 표시되는 동안 점수 다중 차감 방지)
    public void ReportCollision(string letter, MonoBehaviour source)
    {
        if (source != null && wrongCooldownIds.Contains(source.gameObject.GetInstanceID()))
            return;
        NotifyLetterTyped(letter, source);
    }

    // 힌트 — 떠다니는 버블 중 다음 입력 글자(nextLetter)와 일치하는 첫 버블 1개만 잠시 키움
    // WormGameSpawner는 버블을 자식으로 부착하지 않으므로 씬 전체에서 검색
    public override void ShowHint(string nextLetter, float duration)
    {
        if (string.IsNullOrEmpty(nextLetter)) return;

        foreach (var click in FindObjectsByType<AlphabetClick>(FindObjectsSortMode.None))
        {
            if (click == null) continue;
            if (click.letter == nextLetter)
            {
                StartCoroutine(AnimateHintHighlight(new List<Transform> { click.transform }, duration));
                return;
            }
        }
    }
}
