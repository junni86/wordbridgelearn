using UnityEngine;
using UnityEngine.UI;

// 첫 실행 시 개인정보/이용약관 동의 팝업을 표시하는 매니저
// - PlayerPrefs("ConsentAccepted") == 1 이면 팝업 표시 안 함 (이미 동의함)
// - 동의 버튼 클릭 시 PlayerPrefs에 저장하고 팝업 숨김 → 이후 실행에서는 다시 안 뜸
// - 거부 버튼은 선택 사항 (연결돼 있으면 앱 종료)
public class ConsentPopupManager : MonoBehaviour
{
    [Header("동의 팝업 UI 연결")]
    [SerializeField] GameObject consentPanel; // 동의 팝업 전체 패널 (본문 텍스트/버튼 포함)
    [SerializeField] Button agreeButton;      // "동의함" 버튼
    [SerializeField] Button declineButton;    // "거부" 버튼 (선택 — 없으면 무시)

    // PlayerPrefs 저장 키 — 한 번 1로 저장되면 다시 팝업이 뜨지 않음
    public const string CONSENT_PREFS_KEY = "ConsentAccepted";

    void Start()
    {
        // 이미 동의한 사용자면 패널을 꺼버리고 끝 — 버튼 리스너도 등록 불필요
        if (PlayerPrefs.GetInt(CONSENT_PREFS_KEY, 0) == 1)
        {
            if (consentPanel != null) consentPanel.SetActive(false);
            return;
        }

        // 첫 실행 — 패널 표시 + 버튼 리스너 등록
        if (consentPanel != null) consentPanel.SetActive(true);

        if (agreeButton != null)
            agreeButton.onClick.AddListener(OnClickAgree);

        if (declineButton != null)
            declineButton.onClick.AddListener(OnClickDecline);
    }

    // 동의 버튼 클릭 — PlayerPrefs에 1 저장 후 즉시 저장(Save) 호출로 영구 반영
    // 다음 실행부터는 Start()에서 키가 1이므로 팝업이 뜨지 않음
    void OnClickAgree()
    {
        PlayerPrefs.SetInt(CONSENT_PREFS_KEY, 1);
        PlayerPrefs.Save();

        if (consentPanel != null) consentPanel.SetActive(false);
    }

    // 거부 버튼 클릭 — 동의 없이는 앱 사용 불가 정책 적용 (앱 종료)
    // 에디터에서는 플레이 중지, 빌드에서는 Application.Quit
    void OnClickDecline()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
