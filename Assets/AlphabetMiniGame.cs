using System.Collections.Generic;
using UnityEngine;

// 알파벳 클릭 미니게임 (기존 게임)
// - AlphabetSpawner를 사용해 26개 버블 생성
// - 버블 클릭(AlphabetClick)이 NotifyLetterTyped로 호스트에 입력 전달
// - 호스트가 정답 판정 후 HandleCorrect/HandleWrong 호출
public class AlphabetMiniGame : MiniGameBase
{
    public static AlphabetMiniGame Instance { get; private set; }

    [Header("스포너")]
    public AlphabetSpawner spawner; // 자식 또는 동일 GameObject에 부착

    [Header("이펙트 프리팹")]
    public GameObject dropletPrefab;     // 정답 시 파티클
    public GameObject wrongMarkPrefab;   // 오답 X 표시

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 새 단어 시작 — 단어에 맞춰 26개 버블 재생성
    public override void BeginWord(string answer, List<string> koreanCharPool, bool isKorean)
    {
        if (spawner == null) return;

        if (isKorean)
            spawner.SpawnAllKorean(answer, koreanCharPool);
        else
            spawner.SpawnAll(answer);
    }

    // 미니게임 종료 시 정리 — 모든 버블 제거
    public override void Cleanup()
    {
        if (spawner != null) spawner.DestroyAll();
    }

    // 정답: 파티클 생성 + 클릭한 버블 제거
    public override void HandleCorrect(MonoBehaviour source, int filledIndex, string letter)
    {
        if (source == null) return;

        if (dropletPrefab != null)
            Instantiate(dropletPrefab, source.transform.position, Quaternion.identity);

        Destroy(source.gameObject);
    }

    // 오답: 버블 0.5초 정지 + X 마크 0.5초 표시
    public override void HandleWrong(MonoBehaviour source, string expected)
    {
        if (source == null) return;

        if (source.TryGetComponent(out RandomMove move))
            move.Pause(0.5f);

        ShowWrongMark(source.transform);
    }

    void ShowWrongMark(Transform parent)
    {
        if (wrongMarkPrefab == null || parent == null) return;
        GameObject mark = Instantiate(wrongMarkPrefab, parent);
        Destroy(mark, 0.5f);
    }

    // AlphabetClick에서 호출 — 입력을 호스트로 전달
    public void ReportClick(string letter, MonoBehaviour source)
    {
        NotifyLetterTyped(letter, source);
    }

    // 힌트 — 지금 입력해야 하는 다음 글자(nextLetter)와 일치하는 첫 버블 1개만 잠시 키움
    // AlphabetSpawner는 버블을 자식으로 부착하지 않으므로 씬 전체에서 검색
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
