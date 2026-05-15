using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class TeacherPoseController : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public Image teacherImage;
    public TMP_Text dialogueText;

    [Header("Pose Sprites")]
    public Sprite[] poses;

    private int currentIndex = 0;

    private string[] dialogues =
    {
        "안녕하세요, 수업을 시작할까요?",
        "정답을 골라보세요!",
        "잘하고 있어요.",
        "조금 더 집중해볼까요?",
        "힌트를 드릴게요.",
        "좋아요!",
        "다시 생각해볼까요?",
        "훌륭해요!",
        "설명해줄게요.",
        "다음 문제로 가볼까요?"
    };

    void Start()
    {
        ShowPose(0);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("이미지 클릭됨");
        NextPose();
    }
    public void NextPose()
    {
        Debug.Log("버튼 클릭됨");

        currentIndex++;

        if (currentIndex >= poses.Length)
            currentIndex = 0;

        Debug.Log("현재 포즈 번호: " + currentIndex);

        ShowPose(currentIndex);
    }

    public void ShowPose(int index)
    {
        if (teacherImage == null)
        {
            Debug.LogError("Teacher Image 연결 안 됨");
            return;
        }

        if (poses == null || poses.Length == 0)
        {
            Debug.LogError("Poses 배열이 비어 있음");
            return;
        }

        if (index < 0 || index >= poses.Length)
        {
            Debug.LogError("잘못된 index: " + index);
            return;
        }

        Debug.Log("이미지 변경: " + poses[index].name);

        teacherImage.sprite = poses[index];

        if (dialogueText != null && index < dialogues.Length)
            dialogueText.text = dialogues[index];
    }
}