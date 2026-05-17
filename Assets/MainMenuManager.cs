using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// 메인 메뉴 화면의 모든 버튼 동작을 관리하는 스크립트
// Inspector에서 각 버튼을 연결하면 코드로 자동 리스너 등록
public class MainMenuManager : MonoBehaviour
{
    [Header("버튼 연결")]
    [SerializeField] Button startButton;        // 시작하기 버튼
    [SerializeField] Button settingsButton;     // 설정 버튼
    [SerializeField] Button rewardButton;       // 보상 버튼
    [SerializeField] Button quitButton;         // 게임 종료 버튼
    [SerializeField] Button noAdsButton;        // 광고 안보기 버튼
    [SerializeField] Button serverRankButton;   // 서버 최고 랭킹 버튼
    [SerializeField] Button myScoreButton;      // 나의 최고 점수 버튼

    [Header("랭킹 매니저 연결")]
    [SerializeField] RankingManager rankingManager; // RankingManager 오브젝트 연결

    [Header("점수 텍스트 연결")]
    [SerializeField] TextMeshProUGUI serverHighScoreText;   // 서버 최고 점수 표시
    [SerializeField] TextMeshProUGUI myHighScoreText;       // 나의 최고 점수 표시
    [SerializeField] TextMeshProUGUI versionText;           // 앱 버전 표시
    [SerializeField] TextMeshProUGUI rewardText;            // 현재 보유 힌트(아이템) 수 표시

    // 광고 시청으로 충전 가능한 힌트 보유 상한 — 이 값 이상이면 광고 차단
    const int HINT_MAX_CAP = 10;

    void Start()
    {
        // 각 버튼에 클릭 리스너 등록 (null 체크로 연결 안 된 버튼 무시)
        if (startButton != null) startButton.onClick.AddListener(OnClickStart);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnClickSettings);
        if (rewardButton != null) rewardButton.onClick.AddListener(OnClickReward);
        if (quitButton != null) quitButton.onClick.AddListener(OnClickQuit);
        if (noAdsButton != null) noAdsButton.onClick.AddListener(OnClickNoAds);
        if (serverRankButton != null) serverRankButton.onClick.AddListener(OnClickServerRank);
        if (myScoreButton != null) myScoreButton.onClick.AddListener(OnClickMyScore);

        // PlayerPrefs에 저장된 나의 최고 점수 불러와서 표시
        int myBest = PlayerPrefs.GetInt("BestScore", 0);
        if (myHighScoreText != null) myHighScoreText.text = myBest.ToString("N0");
        if (serverHighScoreText != null) serverHighScoreText.text = "0";

        // 앱 버전 표시 (Project Settings → Player → Version)
        if (versionText != null) versionText.text = $"v{Application.version}";

        // 보상(힌트) 수 표시 — PlayerPrefs.HintCount 값을 그대로 숫자만 출력
        UpdateRewardText();
    }

    // 게임 씬에서 돌아와 MainScene이 다시 활성화될 때도 최신값으로 갱신
    void OnEnable()
    {
        UpdateRewardText();
    }

    // 현재 보유 힌트 수를 rewardText에 숫자로 표시
    void UpdateRewardText()
    {
        if (rewardText == null) return;
        int current = PlayerPrefs.HasKey(GameHostManager.HINT_PREFS_KEY)
            ? PlayerPrefs.GetInt(GameHostManager.HINT_PREFS_KEY)
            : GameHostManager.HINT_INITIAL;
        rewardText.text = current.ToString();
    }

    // 시작하기 버튼 → 게임 씬으로 이동
    // MiniGameSelector를 새 게임으로 초기화 후 단일 게임 씬 로드
    void OnClickStart()
    {
        MiniGameSelector.InitNewGame();
        SceneManager.LoadScene("SampleScene");
    }

    // 설정 버튼 → 설정 패널 열기
    void OnClickSettings()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OpenPanel();
    }

    // 보상 버튼 → 보상 광고 시청 → 힌트 카운트 +2 (PlayerPrefs에 누적 저장)
    void OnClickReward()
    {
        Debug.Log("[Reward] OnClickReward 진입");

        // -1) 보유 힌트가 상한 이상이면 광고 차단 — 무한 충전 방지
        int currentHint = PlayerPrefs.HasKey(GameHostManager.HINT_PREFS_KEY)
            ? PlayerPrefs.GetInt(GameHostManager.HINT_PREFS_KEY)
            : GameHostManager.HINT_INITIAL;
        if (currentHint >= HINT_MAX_CAP)
        {
            Debug.Log($"[Reward] 보유 {currentHint} ≥ 한도 {HINT_MAX_CAP} → 광고 차단");
            if (ToastManager.Instance != null)
                ToastManager.Instance.Show($"You already have the maximum of {HINT_MAX_CAP} items.\nUse them before watching another ad.");
            return;
        }

        // 0) 인터넷 연결 확인 — 광고는 네트워크가 필요하므로 미연결 시 즉시 안내 후 종료
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.Log("[Reward] No Internet Connection — Notice regarding unavailable ads");
            if (ToastManager.Instance != null)
                ToastManager.Instance.Show("Internet connection is required.\nPlease connect to the network to watch the advertisement.");
            return;
        }

        // 1) AdsManager 자체가 없는 경우 — 즉시 보상(디버그 폴백) 경로
        if (AdsManager.Instance == null)
        {
            Debug.LogWarning("[Reward] AdsManager.Instance == null → 광고 없이 즉시 +2 지급 (에디터 폴백)");
            GrantHintReward();
            return;
        }

        // 2) AdsManager는 있지만 보상 광고가 준비됐는지 확인
        Debug.Log($"[Reward] AdsManager.Instance 존재. IsRewardedReady={AdsManager.Instance.IsRewardedReady}");

        // 보상 광고가 아직 로드 안 됐으면 사용자에게 안내 (네트워크 약함/로드 지연 등)
        if (!AdsManager.Instance.IsRewardedReady)
        {
            Debug.Log("[Reward] IsRewardedReady=false → 광고 준비 중 안내");
            if (ToastManager.Instance != null)
                ToastManager.Instance.Show("The advertisement is being prepared. \nPlease try again later.");
            return;
        }

        AdsManager.Instance.ShowRewardedAd(
            onReward: () =>
            {
                Debug.Log("[Reward] onReward 콜백 발생 — 광고 시청 완료로 인한 +2");
                GrantHintReward();
            },
            onClosed: () =>
            {
                Debug.Log("[Reward] onClosed 콜백 발생 (시청 여부 무관, 보상 지급 안 함)");
            });
    }

    // 힌트 카운트 +HINT_AD_REWARD 충전 — 게임 씬에서도 같은 키를 사용
    // 인스턴스 메서드로 둬서 충전 직후 rewardText UI도 함께 갱신
    void GrantHintReward()
    {
        int current = PlayerPrefs.HasKey(GameHostManager.HINT_PREFS_KEY)
            ? PlayerPrefs.GetInt(GameHostManager.HINT_PREFS_KEY)
            : GameHostManager.HINT_INITIAL;
        PlayerPrefs.SetInt(GameHostManager.HINT_PREFS_KEY, current + GameHostManager.HINT_AD_REWARD);
        PlayerPrefs.Save();
        Debug.Log($"[Reward] GrantHintReward 실행 → 힌트 +{GameHostManager.HINT_AD_REWARD} 충전 (이전:{current} → 현재:{current + GameHostManager.HINT_AD_REWARD}) — 호출 스택 위에서 어디서 불렀는지 확인할 것");
        UpdateRewardText();
    }

    // 게임 종료 버튼 (에디터/빌드 환경 분기 처리)
    void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 광고 안보기 버튼 → Remove Ads 구매
    void OnClickNoAds()
    {
        if (IAPManager.Instance != null)
            IAPManager.Instance.BuyRemoveAds();
        else
            Debug.LogWarning("[MainMenuManager] IAPManager가 없어 구매 불가 (에디터 전용 메시지)");
    }

    // 서버 최고 점수 텍스트 갱신 (외부에서 호출 가능)
    public void SetServerHighScore(int score)
    {
        if (serverHighScoreText != null)
            serverHighScoreText.text = score.ToString("N0");
    }

    // 점수 UI 새로고침 (외부에서 호출 가능)
    public void RefreshScoreDisplay()
    {
        int myBest = PlayerPrefs.GetInt("BestScore", 0);
        if (myHighScoreText != null)
            myHighScoreText.text = myBest.ToString("N0");
    }

    // Top Score 버튼 → 랭킹 패널 열기
    void OnClickServerRank()
    {
        if (rankingManager != null)
            rankingManager.OpenTopScore();
    }

    // My Best Score 버튼 → ID 입력 패널 열기
    void OnClickMyScore()
    {
        if (rankingManager != null)
            rankingManager.OpenMyScore();
    }
}
