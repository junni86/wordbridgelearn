using UnityEngine;

// BombMan 미니게임의 격자 셀 컴포넌트 2종을 한 파일에 묶음
// - BombManBrick : 벽돌 한 개. 폭발에 닿으면 그리드에서 제거됨.
// - BombManLetter : 글자 물방울 한 개. 폭발에 닿으면 호스트로 입력 전달.

// 벽돌 1칸 — 그리드 좌표와 시각 표현만 보유
public class BombManBrick : MonoBehaviour
{
    [System.NonSerialized] public int col = -1;
    [System.NonSerialized] public int row = -1;

    public void Init(int assignedCol, int assignedRow)
    {
        col = assignedCol;
        row = assignedRow;
    }
}

// 글자 물방울 1칸 — 셀에 표시된 글자를 보유. LineWordDroplet의 단순 버전
public class BombManLetter : MonoBehaviour
{
    [System.NonSerialized] public string letter = string.Empty;
    [System.NonSerialized] public int col = -1;
    [System.NonSerialized] public int row = -1;
    [System.NonSerialized] public bool consumed = false; // 같은 폭발에서 중복 입력 방지용

    public void Init(string assignedLetter, int assignedCol, int assignedRow)
    {
        letter = assignedLetter ?? string.Empty;
        col = assignedCol;
        row = assignedRow;
    }
}
