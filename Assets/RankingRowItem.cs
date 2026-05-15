using UnityEngine;
using TMPro;

// 랭킹 행 프리팹에 추가하는 컴포넌트
// RankingRowPrefab 오브젝트에 Add Component → RankingRowItem 으로 연결
public class RankingRowItem : MonoBehaviour
{
    [Header("랭킹 행 텍스트 연결")]
    public TMP_Text rankText;    // 순위
    public TMP_Text idText;      // 플레이어 ID
    public TMP_Text countryText; // 국가 코드
    public TMP_Text scoreText;   // 점수
    public TMP_Text stageText;   // 스테이지

    // 데이터를 받아 각 텍스트에 표시
    public void SetData(int rank, string id, string country, int score, int stage)
    {
        if (rankText != null) rankText.text = rank.ToString();
        if (idText != null) idText.text = id;
        if (countryText != null) countryText.text = country;
        if (scoreText != null) scoreText.text = score.ToString("N0");
        if (stageText != null) stageText.text = $"{stage}";
    }
}
