using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 일시정지 버튼 및 오버레이 관리 스크립트
// Canvas 안 빈 오브젝트에 붙이고 Inspector에서 각 요소 연결
public class PauseManager : MonoBehaviour
{
    [Header("일시정지 버튼")]
    [SerializeField] Button pauseButton;      // 화면 상단 일시정지 버튼
    [SerializeField] Sprite pauseSprite;      // 버튼 아이콘 이미지

    [Header("일시정지 오버레이")]
    [SerializeField] GameObject pausePanel;    // 반투명 오버레이 패널
    [SerializeField] Button continueButton;    // 계속하기 버튼
    [SerializeField] Sprite continueSprite;    // 계속하기 버튼 이미지
    [SerializeField] Button quitButton;        // 그만하기 버튼
    [SerializeField] Sprite quitSprite;        // 그만하기 버튼 이미지

    void Start()
    {
        // 버튼 이미지 설정
        if (pauseSprite != null)
            pauseButton.GetComponent<Image>().sprite = pauseSprite;
        if (continueSprite != null)
            continueButton.GetComponent<Image>().sprite = continueSprite;
        if (quitSprite != null)
            quitButton.GetComponent<Image>().sprite = quitSprite;

        pauseButton.onClick.AddListener(Pause);
        continueButton.onClick.AddListener(Resume);
        quitButton.onClick.AddListener(Quit);

        // 시작 시 오버레이 숨김
        pausePanel.SetActive(false);
    }

    void Pause()
    {
        Time.timeScale = 0f;        // 게임 정지
        pausePanel.SetActive(true); // 오버레이 표시
    }

    void Resume()
    {
        Time.timeScale = 1f;         // 게임 재개
        pausePanel.SetActive(false); // 오버레이 숨김
    }

    void Quit()
    {
        // 현재 점수 저장 후 메인 메뉴로 이동 (호스트가 정상 동작 중일 때만)
        if (GameHostManager.Instance != null)
            GameHostManager.Instance.SaveScore();

        Time.timeScale = 1f; // 씬 이동 전 timeScale 반드시 초기화
        SceneManager.LoadScene("MainScene");
    }
}
