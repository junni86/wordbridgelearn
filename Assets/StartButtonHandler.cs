using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 시작 버튼에 직접 붙이는 스크립트
// Inspector 연결 없이 코드로 클릭 이벤트를 등록
[RequireComponent(typeof(Button))]
public class StartButtonHandler : MonoBehaviour
{
    void Start()
    {
        // 버튼 클릭 시 SampleScene(게임 씬)으로 이동
        GetComponent<Button>().onClick.AddListener(() =>
            SceneManager.LoadScene("SampleScene"));
    }
}
