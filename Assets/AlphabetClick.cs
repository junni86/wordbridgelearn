using UnityEngine;

public class AlphabetClick : MonoBehaviour
{
    public string letter;

    public void Init(string assignedLetter)
    {
        letter = assignedLetter;
    }

    void OnMouseDown()
    {
        // 알파벳 미니게임 활성 시 클릭 입력을 미니게임 → 호스트로 전달
        if (AlphabetMiniGame.Instance == null) return;
        AlphabetMiniGame.Instance.ReportClick(letter, this);
    }
}
