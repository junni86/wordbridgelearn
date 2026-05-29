using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// 게임 씬의 단일 호스트 매니저
// - 단어 큐 / 점수 / 스테이지 / 타이머 / 힌트 / Pause UI 등 공용 룰 관리
// - 활성 미니게임(MiniGameBase) 인스턴스를 보유하고 5스테이지 단위로 교체
// - 미니게임은 입력 + 시각 처리만 담당하고 정답 검증은 이 호스트가 수행
public class GameHostManager : MonoBehaviour
{
    public static GameHostManager Instance { get; private set; }

    [System.Serializable]
    public class WordPair
    {
        public string koreanWord;
        public string englishWord;
    }

    [Header("힌트 UI")]
    public TMP_Text hintMainText;
    public TMP_Text hintSubText;
    public GameObject hintTapArea;

    [Header("결과/점수/스테이지")]
    public TMP_Text resultText;
    public TMP_Text scoreText;
    public TMP_Text stageText;

    [Header("타이머 & Pause")]
    public TMP_Text timerText;
    public GameObject pausePanel;
    public Button quitButton;
    public string mainMenuSceneName = "MainScene";

    [Header("힌트 시스템 (정답 글자 3초간 키우기)")]
    public Button hintButton;        // 힌트 사용 버튼
    public TMP_Text hintCountText;   // 남은 힌트 횟수 표시 (예: "5")
    // 광고 보고 +2 충전 버튼은 MainScene의 RewardButton(MainMenuManager.OnClickReward)에서 처리

    [Header("미니게임 프리팹 (Inspector 연결)")]
    public MiniGameBase alphabetMiniGamePrefab; // 알파벳 게임 프리팹
    public MiniGameBase wormMiniGamePrefab;     // 지렁이 게임 프리팹
    public MiniGameBase passWallMiniGamePrefab; // PassWall 게임 프리팹
    public MiniGameBase lineWordMiniGamePrefab; // LineWord 게임 프리팹
    public MiniGameBase bombManMiniGamePrefab;  // BombMan 게임 프리팹

    [Header("미니게임 호스트 컨테이너")]
    public Transform miniGameHost; // 활성 미니게임이 자식으로 들어갈 빈 GameObject

    [Header("정답 완성 효과")]
    public GameObject completeImage; // 단어 완성 시 0.5초간 표시할 이미지 (Inspector 연결, 기본 비활성)

    [Header("디버그")]
    [Tooltip("첫판 시작 미니게임을 강제 지정 (Default = 정상 흐름). 5단어 정답 후엔 RandomizeMiniGame이 정상 동작.")]
    [SerializeField] DebugStartMiniGame debugStartMiniGame = DebugStartMiniGame.Default;

    // 디버그용 — Inspector 드롭다운으로 첫판 시작 미니게임 강제 지정
    enum DebugStartMiniGame
    {
        Default = -1,
        Alphabet = 0,
        Worm = 1,
        PassWall = 2,
        LineWord = 3,
        BombMan = 4,
    }

    // 현재 활성 미니게임 인스턴스
    MiniGameBase currentMiniGame;
    int currentMiniGameId = -1; // 마지막으로 인스턴스화한 미니게임 ID

    // 게임 상태
    int score;
    int stage = 1;
    int roundsInStage = 0;
    int currentIndex; // 현재 단어에서 채운 글자 수 (호스트가 단일 진실원천)
    bool isWordCompleting; // 단어 완성 직후 0.5초 잠금 — complete 이미지 표시 중 입력 차단
    const int ROUNDS_PER_STAGE = 10;

    [SerializeField] string debugWord = ""; // 디버그용 고정 단어
    [SerializeField] float hintSubGap = 10f;

    // 힌트 3연탭 자동 풀이
    int hintTapCount = 0;
    float lastHintTapTime = -1f;
    const float TRIPLE_TAP_INTERVAL = 0.4f;

    readonly List<WordPair> queue = new();
    readonly List<string> koreanCharPool = new();
    WordPair current;
    static readonly WaitForSeconds waitNextWord = new(0.5f);

    const float WORD_TIME_LIMIT = 60f;
    const float WORD_TIME_LIMIT_BOMB_MAN = 120f; // BombMan은 격자 탐색이 필요해 시간 연장
    Coroutine timerCoroutine;
    float timerRemaining;

    // ─── 힌트 시스템 ─────────────────────────────────────────────────
    // public const — MainMenuManager(RewardButton)에서도 같은 키/보상량을 공유
    public const int HINT_INITIAL = 10;          // 첫 실행 시 충전량
    public const int HINT_AD_REWARD = 2;          // 광고 1회 시청 보상량 (MainScene 보상 버튼)
    public const float HINT_DURATION = 3f;        // 힌트 강조 지속 시간 (초)
    public const string HINT_PREFS_KEY = "HintCount";
    int hintRemaining;

    // 현재 미니게임에 따른 단어 제한시간 (BombMan만 120초, 나머지 60초)
    float GetCurrentWordTimeLimit()
    {
        return MiniGameSelector.CurrentMiniGame == MiniGameSelector.GAME_BOMB_MAN
            ? WORD_TIME_LIMIT_BOMB_MAN
            : WORD_TIME_LIMIT;
    }

    void Awake()
    {
        Application.targetFrameRate = 60;
        Instance = this;

        // 시작 시 timeScale 강제 1로 (이전 실행에서 0으로 남은 경우 대비)
        Time.timeScale = 1f;

        LoadKoreanCharPool();

        // 다른 게임에서 진입했거나 새 게임 시작 — MiniGameSelector에서 상태 이어받기
        score = MiniGameSelector.CurrentScore;
        stage = Mathf.Max(1, MiniGameSelector.CurrentStage);

        // 디버그: Inspector에서 첫판 시작 미니게임을 강제 지정한 경우 적용
        if (debugStartMiniGame != DebugStartMiniGame.Default)
            MiniGameSelector.SetMiniGame((int)debugStartMiniGame);

        LoadWordsForStage(stage);
        UpdateScoreDisplay();
        UpdateStageDisplay();
    }

    // pausePanel 활성 시 어떤 입력(터치/마우스 클릭)이든 Quit으로 처리하는 폴백
    // - GameOverButton의 Button 설정이나 Raycast 가림 등으로 정식 클릭이 안 잡혀도 종료 가능하게 함
    void Update()
    {
        if (pausePanel == null || !pausePanel.activeSelf) return;

        bool tapped = false;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) tapped = true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) tapped = true;

        if (tapped)
            OnQuitClicked();
    }

    void Start()
    {
        // 힌트 영역 클릭 핸들러 (3연탭 = 자동 풀이)
        if (hintTapArea != null)
        {
            var trigger = hintTapArea.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => OnHintTapped());
            trigger.triggers.Add(entry);
        }

        // hintSubText 앵커/피벗 + ContentSizeFitter (글자 길이 변화 대응)
        if (hintSubText != null)
        {
            var rt = hintSubText.rectTransform;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.pivot = new Vector2(rt.pivot.x, 1f);

            var csf = hintSubText.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = hintSubText.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        if (pausePanel != null) pausePanel.SetActive(false);
        // 완성 이미지는 시작 시 비활성 — 단어 완성 시에만 잠깐 표시
        if (completeImage != null) completeImage.SetActive(false);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        // 힌트 시스템 초기화 — PlayerPrefs에서 잔여 횟수 로드 (첫 실행이면 10개 충전)
        hintRemaining = PlayerPrefs.HasKey(HINT_PREFS_KEY)
            ? PlayerPrefs.GetInt(HINT_PREFS_KEY)
            : HINT_INITIAL;
        if (hintButton != null) hintButton.onClick.AddListener(OnHintClicked);
        UpdateHintUI();

        PickNext();
    }

    // ─── 힌트 버튼 ───────────────────────────────────────────────────

    // 힌트 사용 — 지금 입력해야 하는 다음 한 글자에 해당하는 풍선 1개만 3초간 1.5배로 키움
    void OnHintClicked()
    {
        if (current == null || currentMiniGame == null) return;
        if (hintRemaining <= 0) return;
        // 일시정지/단어 완성 직후엔 무시
        if (isWordCompleting) return;
        if (pausePanel != null && pausePanel.activeSelf) return;

        bool isKorean = SettingsManager.CurrentLanguage == "KR";
        string answer = isKorean ? current.koreanWord : current.englishWord;
        if (currentIndex >= answer.Length) return;

        // 다음 입력해야 하는 한 글자 — 영문은 호스트가 항상 대문자로 검증하므로 일관되게 대문자
        string nextLetter = answer[currentIndex].ToString();
        if (!isKorean) nextLetter = nextLetter.ToUpper();

        currentMiniGame.ShowHint(nextLetter, HINT_DURATION);

        hintRemaining--;
        PlayerPrefs.SetInt(HINT_PREFS_KEY, hintRemaining);
        UpdateHintUI();
    }

    // 외부(MainScene RewardButton 등)에서 PlayerPrefs.HintCount를 충전한 뒤
    // 게임 씬으로 돌아왔을 때 호출하면 잔여량을 다시 읽어 UI를 갱신함
    public void ReloadHintCount()
    {
        hintRemaining = PlayerPrefs.HasKey(HINT_PREFS_KEY)
            ? PlayerPrefs.GetInt(HINT_PREFS_KEY)
            : HINT_INITIAL;
        UpdateHintUI();
    }

    void UpdateHintUI()
    {
        // if (hintCountText != null) hintCountText.text = hintRemaining.ToString();
        if (hintCountText != null) hintCountText.text = $"x {hintRemaining.ToString()}";
        if (hintButton != null) hintButton.interactable = hintRemaining > 0;
    }

    // ─── CSV 로드 ────────────────────────────────────────────────────────────

    void LoadWordsForStage(int targetStage)
    {
        queue.Clear();

        string fileName;
        if (targetStage >= 8)
        {
            int randomFile = Random.Range(1, 8);
            fileName = $"words{randomFile}";
        }
        else
        {
            fileName = $"words{targetStage}";
        }

        TextAsset csv = Resources.Load<TextAsset>(fileName);
        if (csv == null)
        {
            Debug.LogError($"[GameHostManager] {fileName}.csv 파일을 찾을 수 없습니다.");
            return;
        }

        string[] lines = csv.text.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            string[] parts = trimmed.Split(',');
            if (parts.Length < 2) continue;

            string eng = parts[1].Trim();
            if (eng.Length == 0) continue;
            // 10단계 미만: 8자 초과 단어 제외
            if (targetStage < 10 && eng.Length > 8) continue;

            string kor = new string(System.Array.FindAll(
                parts[0].Trim().ToCharArray(), c => c >= '가' && c <= '힣'));
            if (kor.Length == 0) continue;

            queue.Add(new WordPair { koreanWord = kor, englishWord = eng.ToUpper() });
        }

        // 단어 순서 셔플
        for (int i = queue.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }
    }

    void LoadKoreanCharPool()
    {
        koreanCharPool.Clear();
        HashSet<string> charSet = new();

        for (int i = 1; i <= 7; i++)
        {
            TextAsset csv = Resources.Load<TextAsset>($"words{i}");
            if (csv == null) continue;

            foreach (string line in csv.text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                string[] parts = trimmed.Split(',');
                if (parts.Length < 1) continue;

                foreach (char c in parts[0].Trim())
                {
                    if (c < '가' || c > '힣') continue;
                    string cs = c.ToString();
                    if (charSet.Add(cs))
                        koreanCharPool.Add(cs);
                }
            }
        }
    }

    // ─── 단어/스테이지 흐름 ──────────────────────────────────────────────────

    void PickNext()
    {
        if (queue.Count == 0) LoadWordsForStage(stage);

        current = queue[0];
        queue.RemoveAt(0);
        currentIndex = 0;

        if (!string.IsNullOrEmpty(debugWord))
            current = new WordPair { koreanWord = "디버그", englishWord = debugWord.ToUpper() };

        // 미니게임이 바뀌어야 하면 교체
        EnsureCurrentMiniGame();

        // 미니게임에 새 단어 시작 알림
        bool isKorean = SettingsManager.CurrentLanguage == "KR";
        string answer = isKorean ? current.koreanWord : current.englishWord;
        if (currentMiniGame != null)
            currentMiniGame.BeginWord(answer, koreanCharPool, isKorean);

        UpdateDisplay();
        if (resultText != null) resultText.text = "";

        StartWordTimer();
    }

    // 현재 스테이지에 맞는 미니게임이 활성화되도록 보장 (필요 시 교체)
    void EnsureCurrentMiniGame()
    {
        int wantedId = MiniGameSelector.CurrentMiniGame;
        if (currentMiniGame != null && wantedId == currentMiniGameId) return;

        // 기존 미니게임 정리/제거
        if (currentMiniGame != null)
        {
            currentMiniGame.Cleanup();
            Destroy(currentMiniGame.gameObject);
            currentMiniGame = null;
        }

        // 새 미니게임 인스턴스화
        MiniGameBase prefab = wantedId switch
        {
            MiniGameSelector.GAME_WORM => wormMiniGamePrefab,
            MiniGameSelector.GAME_PASS_WALL => passWallMiniGamePrefab,
            MiniGameSelector.GAME_LINE_WORD => lineWordMiniGamePrefab,
            MiniGameSelector.GAME_BOMB_MAN => bombManMiniGamePrefab,
            _ => alphabetMiniGamePrefab,
        };

        if (prefab == null)
        {
            Debug.LogError($"[GameHostManager] 미니게임 프리팹이 연결되지 않음 (id={wantedId})");
            return;
        }

        // 먼저 부모 없이 인스턴스화 — 슬롯에 프리팹이 잘못 연결돼도 경고 없이 씬에 생성
        currentMiniGame = Instantiate(prefab);

        // miniGameHost가 씬 오브젝트일 때만 자식으로 부착 (프리팹 에셋이면 SetParent 생략)
        Transform parent = miniGameHost != null ? miniGameHost : transform;
        if (parent != null && parent.gameObject.scene.IsValid())
            currentMiniGame.transform.SetParent(parent, false);

        currentMiniGameId = wantedId;
    }

    // ─── 입력 처리 ───────────────────────────────────────────────────────────

    // 미니게임에서 입력이 들어오면 호출 — 정답 검증 후 미니게임에 결과 위임
    public void OnLetterTyped(string letter, MonoBehaviour source)
    {
        if (current == null) return;
        // 단어 완성 직후 0.5초 잠금 — complete 이미지 표시 중 추가 입력 무시
        if (isWordCompleting) return;

        bool isKorean = SettingsManager.CurrentLanguage == "KR";
        string answer = isKorean ? current.koreanWord : current.englishWord;
        if (currentIndex >= answer.Length) return;

        string expected = answer[currentIndex].ToString();
        string input = isKorean ? letter : (letter ?? string.Empty).ToUpper();
        if (!isKorean) expected = expected.ToUpper();

        if (input == expected)
        {
            int filledIndex = currentIndex;
            currentIndex++;

            // 미니게임에 정답 시각 효과 위임
            if (currentMiniGame != null)
                currentMiniGame.HandleCorrect(source, filledIndex, expected);

            UpdateDisplay();

            if (currentIndex >= answer.Length)
                OnWordCompleted();
        }
        else
        {
            if (resultText != null)
                resultText.text = $"틀렸습니다. '{expected}' 를 누르세요";

            // 진동 + 점수 차감
            SettingsManager.TryVibrate();
            score = Mathf.Max(0, score - 2);
            UpdateScoreDisplay();

            // 미니게임에 오답 시각 효과 위임
            if (currentMiniGame != null)
                currentMiniGame.HandleWrong(source, expected);
        }
    }

    void OnWordCompleted()
    {
        StopWordTimer();

        // 입력 잠금 + complete 이미지 표시 (NextWordDelay에서 해제)
        isWordCompleting = true;
        if (completeImage != null) completeImage.SetActive(true);

        score += 10;
        UpdateScoreDisplay();
        // 최고 점수 갱신 시, 그 시점의 스테이지도 함께 저장 (BestStage는 "최고점수 달성 당시의 스테이지")
        int prevBestScoreA = PlayerPrefs.GetInt("BestScore", 0);
        int prevBestStageA = PlayerPrefs.GetInt("BestStage", 1);
        if (score > prevBestScoreA)
        {
            PlayerPrefs.SetInt("BestScore", score);
            PlayerPrefs.SetInt("BestStage", stage);
            PlayerPrefs.Save(); // 디스크에 즉시 반영 (앱 강제 종료 대비)
            Debug.Log($"[BestUpdate/OnWordCompleted] score:{prevBestScoreA}->{score}, stage:{prevBestStageA}->{stage} (현재 stage={stage})");
        }
        else
        {
            Debug.Log($"[BestUpdate/OnWordCompleted] 갱신없음 — score:{score}<=Best:{prevBestScoreA}, 현재 stage={stage}, BestStage={prevBestStageA}");
        }

        roundsInStage++;

        // 누적 정답 단어 수 증가 — 5단어마다 미니게임 랜덤 교체
        MiniGameSelector.CurrentTotalWords++;
        if (MiniGameSelector.CurrentTotalWords % 5 == 0)
            MiniGameSelector.RandomizeMiniGame();

        // 점수 동기화 (다른 미니게임 진입 시에도 점수 이어짐)
        MiniGameSelector.CurrentScore = score;

        if (roundsInStage >= ROUNDS_PER_STAGE)
        {
            stage++;
            roundsInStage = 0;

            // BestStage는 BestScore 갱신 시점에 함께 저장되므로 여기서는 단독 갱신하지 않음
            // (stage만 단독으로 갱신하면 "최고점수 당시 스테이지"와 어긋나는 버그 발생)

            MiniGameSelector.CurrentStage = stage;

            UpdateStageDisplay();

            if (resultText != null)
                resultText.text = $"★ STAGE {stage} 돌입! 난이도가 올랐습니다!";

            LoadWordsForStage(stage);

            // 전면 광고 표시 — 광고 닫힌 후 다음 단어 시작 (씬 전환 없음, 프리팹만 교체)
            if (AdsManager.Instance != null)
                AdsManager.Instance.ShowInterstitial(() => StartCoroutine(NextWordDelay()));
            else
                StartCoroutine(NextWordDelay());
        }
        else
        {
            if (resultText != null)
                resultText.text = $"정답! {current.koreanWord} = {current.englishWord}  ({roundsInStage}/{ROUNDS_PER_STAGE})";

            StartCoroutine(NextWordDelay());
        }
    }

    IEnumerator NextWordDelay()
    {
        yield return waitNextWord;
        // 0.5초 경과 — complete 이미지 숨김 + 입력 잠금 해제
        if (completeImage != null) completeImage.SetActive(false);
        isWordCompleting = false;
        PickNext();
    }

    // ─── 힌트 클릭 (3연탭으로 자동 완성) ──────────────────────────────────────

    void OnHintTapped()
    {
        float now = Time.unscaledTime;
        if (now - lastHintTapTime > TRIPLE_TAP_INTERVAL)
            hintTapCount = 0;

        lastHintTapTime = now;
        hintTapCount++;

        if (false)
        // if (hintTapCount >= 1)
        {
            hintTapCount = 0;
            string aw = SettingsManager.CurrentLanguage == "KR" ? current.koreanWord : current.englishWord;
            while (current != null && currentIndex < aw.Length)
                AutoSolveNext();
        }
    }

    [ContextMenu("Auto Solve Next Letter")]
    void AutoSolveNext()
    {
        if (current == null) return;
        bool isKorean = SettingsManager.CurrentLanguage == "KR";
        string answer = isKorean ? current.koreanWord : current.englishWord;
        if (currentIndex >= answer.Length) return;
        string next = isKorean ? answer[currentIndex].ToString() : answer[currentIndex].ToString().ToUpper();
        OnLetterTyped(next, null);
    }

    // ─── 외부 호출 인터페이스 ────────────────────────────────────────────────

    // 언어 변경 시 현재 단어 재시작 (SettingsManager에서 호출)
    public void RefreshBubbles()
    {
        if (current == null || currentMiniGame == null) return;

        bool isKorean = SettingsManager.CurrentLanguage == "KR";
        string answer = isKorean ? current.koreanWord : current.englishWord;

        currentIndex = 0;
        currentMiniGame.BeginWord(answer, koreanCharPool, isKorean);
        UpdateDisplay();
    }

    // 점수 저장 (씬 이동 전 호출)
    public void SaveScore()
    {
        // 최고 점수 갱신 시, 그 시점의 스테이지도 함께 저장 (BestStage는 "최고점수 달성 당시의 스테이지")
        int prevBestScoreB = PlayerPrefs.GetInt("BestScore", 0);
        int prevBestStageB = PlayerPrefs.GetInt("BestStage", 1);
        if (score > prevBestScoreB)
        {
            PlayerPrefs.SetInt("BestScore", score);
            PlayerPrefs.SetInt("BestStage", stage);
            PlayerPrefs.Save(); // 디스크에 즉시 반영 (앱 강제 종료 대비)
            Debug.Log($"[BestUpdate/SaveScore] score:{prevBestScoreB}->{score}, stage:{prevBestStageB}->{stage} (현재 stage={stage})");
        }
        else
        {
            Debug.Log($"[BestUpdate/SaveScore] 갱신없음 — score:{score}<=Best:{prevBestScoreB}, 현재 stage={stage}, BestStage={prevBestStageB}");
        }
    }

    // ─── UI 갱신 ─────────────────────────────────────────────────────────────

    void UpdateScoreDisplay()
    {
        if (scoreText != null) scoreText.text = $"{score:N0}";
    }

    void UpdateStageDisplay()
    {
        if (stageText != null) stageText.text = $"STAGE {stage}";
    }

    void UpdateDisplay()
    {
        if (current == null) return;

        bool isKorean = SettingsManager.CurrentLanguage == "KR";
        string answer = isKorean ? current.koreanWord : current.englishWord;
        string hintWord = isKorean ? current.englishWord : current.koreanWord;

        if (hintMainText != null)
            hintMainText.text = hintWord;

        if (hintSubText != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < answer.Length; i++)
            {
                sb.Append(i < currentIndex ? answer[i] : '_');
                if (i < answer.Length - 1) sb.Append(' ');
            }
            hintSubText.text = sb.ToString();
        }

        StartCoroutine(RepositionHintSub());
    }

    IEnumerator RepositionHintSub()
    {
        yield return null; // ContentSizeFitter 갱신 대기
        if (hintMainText == null || hintSubText == null) yield break;

        var mainRt = hintMainText.rectTransform;
        var subRt = hintSubText.rectTransform;
        float newY = mainRt.anchoredPosition.y - mainRt.rect.height - hintSubGap;
        subRt.anchoredPosition = new Vector2(subRt.anchoredPosition.x, newY);
    }

    // ─── 타이머 ──────────────────────────────────────────────────────────────

    void StartWordTimer()
    {
        StopWordTimer();
        timerCoroutine = StartCoroutine(WordTimer());
    }

    void StopWordTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
        if (timerText != null) timerText.text = "";
    }

    IEnumerator WordTimer()
    {
        timerRemaining = GetCurrentWordTimeLimit();
        while (timerRemaining > 0f)
        {
            if (timerText != null)
                timerText.text = Mathf.CeilToInt(timerRemaining).ToString();
            yield return null;
            timerRemaining -= Time.deltaTime;
        }
        if (timerText != null) timerText.text = "0";
        ShowPauseOnTimeout();
    }

    void ShowPauseOnTimeout()
    {
        SaveScore();
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    void OnQuitClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
