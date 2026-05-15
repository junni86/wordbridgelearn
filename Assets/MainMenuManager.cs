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
        if (AdsManager.Instance == null)
        {
            // 에디터/광고 매니저 없음 — 즉시 보상 지급 (디버그)
            GrantHintReward();
            return;
        }
        AdsManager.Instance.ShowRewardedAd(GrantHintReward);
    }

    // 힌트 카운트 +HINT_AD_REWARD 충전 — 게임 씬에서도 같은 키를 사용
    static void GrantHintReward()
    {
        int current = PlayerPrefs.HasKey(GameHostManager.HINT_PREFS_KEY)
            ? PlayerPrefs.GetInt(GameHostManager.HINT_PREFS_KEY)
            : GameHostManager.HINT_INITIAL;
        PlayerPrefs.SetInt(GameHostManager.HINT_PREFS_KEY, current + GameHostManager.HINT_AD_REWARD);
        PlayerPrefs.Save();
        Debug.Log($"[Reward] 힌트 +{GameHostManager.HINT_AD_REWARD} 충전 — 현재 {current + GameHostManager.HINT_AD_REWARD}");
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
