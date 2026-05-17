using UnityEngine;
using UnityEngine.EventSystems;

public class AlphabetClick : MonoBehaviour
{
    public string letter;

    public void Init(string assignedLetter)
    {
        letter = assignedLetter;
    }

    void OnMouseDown()
    {
        // 일시정지 중이면 무시 — PausePanel(ContinueButton 등) 위 클릭이 뒤 물방울로 새는 것 차단
        // PauseManager.Pause()와 GameHostManager.ShowPauseOnTimeout()이 timeScale을 0으로 만드므로 이걸 신호로 사용
        if (Time.timeScale == 0f) return;

        // UI(버튼/패널 등) 위 클릭이면 무시 — Raycast Target이 켜진 UI 위 클릭 차단 (보조 가드)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // 알파벳 미니게임 활성 시 클릭 입력을 미니게임 → 호스트로 전달
        if (AlphabetMiniGame.Instance == null) return;
        AlphabetMiniGame.Instance.ReportClick(letter, this);
    }
}
