using System.Collections.Generic;
using UnityEngine;

// PassWall 미니게임
// - PassWallSpawner: 15칸 행을 위에서 일정 간격으로 떨어뜨림
// - PassWallCharacter: 좌우만 이동, 터치 추적
// - 캐릭터가 풍선 trigger에 닿으면 ReportCollision → 호스트가 정답 판정
// - 정답이면 풍선 제거, 오답이면 캐릭터 짧게 정지 (풍선은 그대로 통과)
public class PassWallMiniGame : MiniGameBase
{
    public static PassWallMiniGame Instance { get; private set; }

    [Header("스포너 / 캐릭터")]
    public PassWallSpawner spawner;     // 자식 또는 동일 GameObject에 부착
    public PassWallCharacter character; // 화면 하단 캐릭터

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

    // 새 단어 시작 — 스포너가 새 단어 기반 행 반복 생성 시작
    public override void BeginWord(string answer, List<string> koreanCharPool, bool isKorean)
    {
        if (spawner != null)
            spawner.BeginWord(answer, koreanCharPool, isKorean);
    }

    public override void Cleanup()
    {
        if (spawner != null)
        {
            spawner.StopSpawnLoop();
            spawner.DestroyAll();
        }
    }

    // 정답: 파티클 + 풍선 제거
    public override void HandleCorrect(MonoBehaviour source, int filledIndex, string letter)
    {
        if (source == null) return;

        if (dropletPrefab != null)
            Instantiate(dropletPrefab, source.transform.position, Quaternion.identity);

        Destroy(source.gameObject);
    }

    // 오답: 캐릭터 짧게 정지 + X 마크. 풍선은 그대로 통과(별도 처리 없음)
    public override void HandleWrong(MonoBehaviour source, string expected)
    {
        if (character != null) character.PauseMovement();
        if (source != null) ShowWrongMark(source.transform);
    }

    void ShowWrongMark(Transform parent)
    {
        if (wrongMarkPrefab == null || parent == null) return;
        GameObject mark = Instantiate(wrongMarkPrefab, parent);
        Destroy(mark, 0.5f);
    }

    // PassWallCharacter에서 호출 — 입력을 호스트로 전달
    public void ReportCollision(string letter, MonoBehaviour source)
    {
        NotifyLetterTyped(letter, source);
    }

    // 힌트 — 떨어지는 풍선 중 다음 입력 글자(nextLetter)와 일치하는 모든 풍선을 동시에 키움
    // PassWall만 다중 강조 (다른 미니게임은 1개만 강조)
    // PassWallSpawner는 풍선을 자식으로 부착하지 않으므로 씬 전체에서 검색
    public override void ShowHint(string nextLetter, float duration)
    {
        if (string.IsNullOrEmpty(nextLetter)) return;

        List<Transform> targets = new();
        foreach (var balloon in FindObjectsByType<PassWallBalloon>(FindObjectsSortMode.None))
        {
            if (balloon == null || balloon.consumed) continue;
            if (balloon.letter != nextLetter) continue;
            targets.Add(balloon.transform);
        }
        if (targets.Count > 0)
            StartCoroutine(AnimateHintHighlight(targets, duration));
    }
}
