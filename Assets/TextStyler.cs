using UnityEngine;
using TMPro;

// TMP 텍스트 스타일(색상, 아웃라인, 글로우)을 코드로 적용하는 스크립트
// 텍스트 내용은 GameManager가 관리하므로 여기서는 스타일만 설정
public class TextStyler : MonoBehaviour
{
    public enum Style { Score, Hint, HintEnglish, Result, MyScore, ServerRank, Letter, Timer }

    [SerializeField] Style style = Style.Score;

    [Header("폰트 설정 (비워두면 기존 폰트 유지)")]
    [SerializeField] TMP_FontAsset customFont;   // 적용할 폰트 (선택사항)

    void Start()
    {
        ApplyStyle();
    }

    void ApplyStyle()
    {
        TMP_Text tmp = GetComponent<TMP_Text>();

        // 폰트가 지정된 경우에만 교체
        if (customFont != null)
            tmp.font = customFont;

        tmp.alignment = TextAlignmentOptions.Center;

        // fontMaterial: 인스턴스 머티리얼 생성 (다른 텍스트에 영향 없음)
        Material mat = tmp.fontMaterial;

        switch (style)
        {
            case Style.Score:
                // 금색 텍스트 + 진한 갈색 아웃라인 + 그림자
                tmp.fontSize = 72;
                tmp.color = new Color(1f, 0.85f, 0.1f); // 밝은 금색

                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.25f, 0.1f, 0f)); // 진한 갈색 아웃라인

                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.15f, 0.05f, 0f, 0.8f));
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.05f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.06f);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);
                break;

            case Style.Hint:
                // 진한 갈색 텍스트 + 검정 아웃라인
                tmp.fontSize = 48;
                tmp.color = new Color(0.36f, 0.18f, 0f); // #5C2E00 진한 갈색

                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.4f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.white); // 흰색 아웃라인
                break;

            case Style.HintEnglish:
                // 영어 빈칸 텍스트 — 진한 갈색 + 흰색 아웃라인
                tmp.fontSize = 48;
                tmp.color = new Color(0.36f, 0.18f, 0f);

                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.4f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
                break;

            case Style.Result:
                // 초록 텍스트 + 흰색 아웃라인
                tmp.fontSize = 48;
                tmp.color = new Color(0.2f, 1f, 0.4f);

                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
                break;

            case Style.MyScore:
            case Style.ServerRank:
                // 흰색 텍스트 + 남색 아웃라인 + 검정 그림자 + 굵게
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;

                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.08f, 0.12f, 0.45f));

                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.08f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.08f);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
                break;

            case Style.Letter:
                // 버블 위 알파벳 글자 스타일 - 흰색 + 파란 외곽선 + 그림자
                tmp.fontSize = 120;
                tmp.color = Color.white;

                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.1f, 0.4f, 1f));
                mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f); // 글자 두께

                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.5f));
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.05f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.05f);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
                break;
            case Style.Timer:
                // 금색 텍스트 + 진한 갈색 아웃라인 + 그림자
                tmp.fontSize = 200;
                tmp.color = new Color(1f, 0.85f, 0.1f); // 밝은 금색

                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.25f, 0.1f, 0f)); // 진한 갈색 아웃라인

                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.15f, 0.05f, 0f, 0.8f));
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.05f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.06f);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);
                break;
        }
    }
}
