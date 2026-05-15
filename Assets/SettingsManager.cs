using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// 설정 패널 전체 관리 (BGM, SFX, 진동, 언어)
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("패널")]
    public GameObject settingsPanel;      // 설정 패널 오브젝트
    public GameObject mainMenuPanel;      // 설정 열 때 숨길 메인 메뉴 패널

    [Header("버튼")]
    public Button closeButton;            // X 닫기 버튼
    public Button mainMenuButton;         // 메인화면 이동 버튼
    public Button languageButton;         // 언어 전환 버튼
    public TMP_Text languageButtonText;   // 언어 버튼 텍스트 (선택 사항)
    public Image languageButtonImage;     // 언어 버튼 이미지
    public Sprite englishSprite;          // 영어 상태 이미지
    public Sprite koreanSprite;           // 한국어 상태 이미지

    [Header("BGM 버튼")]
    public Button bgmButton;              // BGM ON/OFF 전환 버튼
    public Image bgmButtonImage;          // BGM 버튼의 Image (스프라이트 교체 대상)
    public Sprite bgmOnSprite;            // BGM ON 상태 이미지
    public Sprite bgmOffSprite;           // BGM OFF 상태 이미지

    [Header("SFX 버튼")]
    public Button sfxButton;              // Sound FX ON/OFF 전환 버튼
    public Image sfxButtonImage;          // SFX 버튼의 Image (스프라이트 교체 대상)
    public Sprite sfxOnSprite;            // SFX ON 상태 이미지
    public Sprite sfxOffSprite;           // SFX OFF 상태 이미지

    [Header("진동 버튼")]
    public Button vibrationButton;        // 진동 ON/OFF 전환 버튼
    public Image vibrationButtonImage;    // 진동 버튼의 Image (스프라이트 교체 대상)
    public Sprite vibrationOnSprite;      // 진동 ON 상태 이미지
    public Sprite vibrationOffSprite;     // 진동 OFF 상태 이미지

    [Header("BGM AudioSource")]
    public AudioSource bgmAudioSource;   // BGM 재생 중인 AudioSource

    [Header("씬 이름")]
    public string mainMenuSceneName = "MainScene"; // 메인 메뉴 씬 이름 (Inspector에서 수정)

    // PlayerPrefs 키
    const string KEY_BGM       = "BGM";
    const string KEY_SFX       = "SFX";
    const string KEY_VIBRATION = "VIBRATION";
    const string KEY_LANG      = "LANG";

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 버튼 리스너 등록
        if (closeButton    != null) closeButton.onClick.AddListener(ClosePanel);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        if (languageButton != null) languageButton.onClick.AddListener(OnLanguageClicked);

        // ON/OFF 버튼 클릭 리스너 등록 (토글 대신 버튼 + 이미지 교체 방식)
        if (bgmButton       != null) bgmButton.onClick.AddListener(OnBGMClicked);
        if (sfxButton       != null) sfxButton.onClick.AddListener(OnSFXClicked);
        if (vibrationButton != null) vibrationButton.onClick.AddListener(OnVibrationClicked);

        // 저장된 설정 불러와서 UI에 반영
        LoadSettings();

        // 시작 시 패널 숨기기
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // 저장된 PlayerPrefs 값을 읽어 UI와 실제 동작에 반영
    void LoadSettings()
    {
        // 최초 설치 시 기본값 저장
        if (!PlayerPrefs.HasKey(KEY_LANG)) { PlayerPrefs.SetString(KEY_LANG, "EN"); PlayerPrefs.Save(); }

        bool bgm       = PlayerPrefs.GetInt(KEY_BGM,       1) == 1;
        bool sfx       = PlayerPrefs.GetInt(KEY_SFX,       1) == 1;
        bool vibration = PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
        string lang    = PlayerPrefs.GetString(KEY_LANG, "EN");

        // 저장된 ON/OFF 상태에 맞는 스프라이트로 각 버튼 이미지 갱신
        UpdateBGMButton(bgm);
        UpdateSFXButton(sfx);
        UpdateVibrationButton(vibration);

        // BGM 실제 적용
        ApplyBGM(bgm);

        // 언어 버튼 텍스트 반영
        UpdateLanguageButton(lang);
    }

    // ─── 패널 열기/닫기 ──────────────────────────────────────────────────────

    public void OpenPanel()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void ClosePanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    // ─── BGM ─────────────────────────────────────────────────────────────────

    // BGM 버튼 클릭 시 ON/OFF 상태 토글 후 이미지 교체 + 음소거 적용
    void OnBGMClicked()
    {
        // 현재 저장된 상태를 읽어 반전 (기본값 ON)
        bool current = PlayerPrefs.GetInt(KEY_BGM, 1) == 1;
        bool next = !current;

        PlayerPrefs.SetInt(KEY_BGM, next ? 1 : 0);
        PlayerPrefs.Save();

        // 새 상태에 맞는 스프라이트로 갱신 후 BGM AudioSource 음소거 반영
        UpdateBGMButton(next);
        ApplyBGM(next);
    }

    // BGM ON/OFF 상태에 따라 버튼 이미지 스프라이트 교체
    void UpdateBGMButton(bool isOn)
    {
        if (bgmButtonImage != null)
            bgmButtonImage.sprite = isOn ? bgmOnSprite : bgmOffSprite;
    }

    void ApplyBGM(bool isOn)
    {
        if (bgmAudioSource != null)
            bgmAudioSource.mute = !isOn;
    }

    // ─── Sound FX ────────────────────────────────────────────────────────────

    // SFX 버튼 클릭 시 ON/OFF 상태 토글 후 이미지 교체
    void OnSFXClicked()
    {
        // 현재 저장된 상태를 읽어 반전 (기본값 ON)
        bool current = PlayerPrefs.GetInt(KEY_SFX, 1) == 1;
        bool next = !current;

        PlayerPrefs.SetInt(KEY_SFX, next ? 1 : 0);
        PlayerPrefs.Save();

        // 새 상태에 맞는 스프라이트로 갱신
        UpdateSFXButton(next);
    }

    // SFX ON/OFF 상태에 따라 버튼 이미지 스프라이트 교체
    void UpdateSFXButton(bool isOn)
    {
        if (sfxButtonImage != null)
            sfxButtonImage.sprite = isOn ? sfxOnSprite : sfxOffSprite;
    }

    // SFX 재생 가능 여부 (외부에서 효과음 재생 전 확인)
    public static bool IsSFXEnabled => PlayerPrefs.GetInt("SFX", 1) == 1;

    // ─── 진동 ────────────────────────────────────────────────────────────────

    // 진동 버튼 클릭 시 ON/OFF 상태 토글 후 이미지 교체
    void OnVibrationClicked()
    {
        // 현재 저장된 상태를 읽어 반전 (기본값 ON)
        bool current = PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
        bool next = !current;

        PlayerPrefs.SetInt(KEY_VIBRATION, next ? 1 : 0);
        PlayerPrefs.Save();

        // 새 상태에 맞는 스프라이트로 갱신
        UpdateVibrationButton(next);
    }

    // 진동 ON/OFF 상태에 따라 버튼 이미지 스프라이트 교체
    void UpdateVibrationButton(bool isOn)
    {
        if (vibrationButtonImage != null)
            vibrationButtonImage.sprite = isOn ? vibrationOnSprite : vibrationOffSprite;
    }

    // 진동 설정 확인 후 진동 실행 (GameManager에서 Handheld.Vibrate() 대신 호출)
    public static void TryVibrate()
    {
        if (PlayerPrefs.GetInt("VIBRATION", 1) == 1)
            Handheld.Vibrate();
    }

    // ─── 언어 ────────────────────────────────────────────────────────────────

    void OnLanguageClicked()
    {
        string current = PlayerPrefs.GetString(KEY_LANG, "EN");
        string next    = current == "EN" ? "KR" : "EN";
        PlayerPrefs.SetString(KEY_LANG, next);
        PlayerPrefs.Save();
        UpdateLanguageButton(next);

        // 게임 중 언어 변경 시 현재 단어를 새 언어로 즉시 재시작
        GameHostManager.Instance?.RefreshBubbles();
    }

    void UpdateLanguageButton(string lang)
    {
        // 텍스트 갱신 (연결된 경우에만)
        if (languageButtonText != null)
            languageButtonText.text = lang == "EN" ? "English" : "한국어";

        // 이미지 스왑
        if (languageButtonImage != null)
            languageButtonImage.sprite = lang == "EN" ? englishSprite : koreanSprite;
    }

    // 현재 언어 반환 (외부 참조용)
    public static string CurrentLanguage => PlayerPrefs.GetString("LANG", "EN");

    // ─── 메인화면 이동 ───────────────────────────────────────────────────────

    void GoToMainMenu()
    {
        ClosePanel();
        Time.timeScale = 1f; // 혹시 일시정지 상태라면 정상화
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
