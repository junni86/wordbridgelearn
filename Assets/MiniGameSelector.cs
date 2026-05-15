using UnityEngine;

// 미니게임 선택 + 진행 상태 보관 (단일 씬 구조)
// - 시작은 항상 알파벳 게임
// - 정답 단어 5개마다 미니게임 랜덤 교체 (트리거는 GameHostManager가 호출)
// 씬 로드 책임은 없음 — GameHostManager가 같은 씬에서 프리팹만 교체
public static class MiniGameSelector
{
    // 미니게임 식별자 (추가 시 여기 등록)
    public const int GAME_ALPHABET  = 0;
    public const int GAME_WORM      = 1;
    public const int GAME_PASS_WALL = 2;
    public const int GAME_LINE_WORD = 3;
    public const int GAME_BOMB_MAN  = 4;

    // 미니게임 풀 — 추후 새 미니게임 추가 시 여기에 등록
    static readonly int[] MiniGamePool = { GAME_ALPHABET, GAME_WORM, GAME_PASS_WALL, GAME_LINE_WORD, GAME_BOMB_MAN };

    // 게임 진행 상태
    public static int CurrentScore      { get; set; } = 0;
    public static int CurrentStage      { get; set; } = 1;
    public static int CurrentTotalWords { get; set; } = 0; // 누적 정답 단어 수 (5단어마다 미니게임 교체 트리거)
    public static int CurrentMiniGame   { get; private set; } = GAME_ALPHABET;

    // 새 게임 시작 시 호출 — MainMenu의 PLAY 버튼에서
    public static void InitNewGame()
    {
        CurrentScore = 0;
        CurrentStage = 1;
        CurrentTotalWords = 0;
        CurrentMiniGame = GAME_ALPHABET; // 시작은 항상 알파벳 게임
    }

    // 미니게임을 풀에서 무작위로 선택 (5단어 정답마다 호출)
    // 현재 미니게임과 같은 결과는 회피 — 반드시 다른 게임으로 전환되도록 함
    public static void RandomizeMiniGame()
    {
        if (MiniGamePool == null || MiniGamePool.Length == 0) return;

        // 풀에 하나밖에 없으면 어쩔 수 없이 그것 (회피 불가능)
        if (MiniGamePool.Length == 1)
        {
            CurrentMiniGame = MiniGamePool[0];
            return;
        }

        int prev = CurrentMiniGame;
        int next;
        do
        {
            next = MiniGamePool[Random.Range(0, MiniGamePool.Length)];
        } while (next == prev);

        CurrentMiniGame = next;
    }

    // 외부에서 미니게임 ID를 직접 지정 (디버그용 — Inspector에서 첫판 강제 시작용)
    public static void SetMiniGame(int gameId)
    {
        CurrentMiniGame = gameId;
    }
}
