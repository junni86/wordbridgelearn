using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

public class AlphabetSpawner : MonoBehaviour
{
    public static AlphabetSpawner Instance { get; private set; }

    public GameObject prefab;          // 알파벳 버블 프리팹
    public SpriteRenderer background;  // 생성 범위 기준 배경

    // 버블 가로가 wallGap(좌·우 벽 사이 간격) 대비 차지할 비율 (0~1)
    // 0.17 = wallGap의 17%. PassWallCharacter.widthRatio와 동일한 의미
    [SerializeField] float bubbleScale = 0.5f;
    [SerializeField] float fontSize = 6f;              // 글자 크기
    [SerializeField] TMP_FontAsset koreanBubbleFont;   // 한글 모드 전용 폰트 (Inspector에서 NotoSansKR-Bold SDF 연결)

    Sprite[] bubbleSprites;                    // 버블 색상 스프라이트 5종
    List<GameObject> spawnedObjects = new();   // 현재 화면에 있는 버블 목록

    void Awake()
    {
        Instance = this;
        LoadBubbleSprites();
    }

    // Resources 폴더에서 버블 색상 스프라이트 로드
    void LoadBubbleSprites()
    {
        bubbleSprites = new Sprite[]
        {
            Resources.Load<Sprite>("imageBubbleBlue"),
            Resources.Load<Sprite>("imageBubbleGreen"),
            Resources.Load<Sprite>("imageBubbleyellow"),
            Resources.Load<Sprite>("imageBubbleRed"),
            Resources.Load<Sprite>("imageBubblePurple")
        };
    }

    // 기존 버블 전체 제거
    public void DestroyAll()
    {
        foreach (var obj in spawnedObjects)
            if (obj != null) Destroy(obj);
        spawnedObjects.Clear();
    }

    // 영어 단어 글자를 먼저 포함한 26개 버블 생성
    public void SpawnAll(string word)
    {
        DestroyAll();

        List<char> pool = BuildLetterPool(word.ToUpper());
        List<Sprite> colorPool = BuildColorPool();

        for (int i = 0; i < pool.Count; i++)
            SpawnOne(pool[i].ToString(), colorPool[i]);
    }

    // 한글 모드: 한글 글자 풀로 26개 버블 생성
    public void SpawnAllKorean(string koreanWord, List<string> charPool)
    {
        DestroyAll();

        List<string> pool = BuildKoreanPool(koreanWord, charPool);
        List<Sprite> colorPool = BuildColorPool();

        for (int i = 0; i < pool.Count; i++)
            SpawnOne(pool[i], colorPool[i], koreanBubbleFont);
    }

    // 한글 정답 글자 + 랜덤 한글 글자로 26개 풀 구성
    List<string> BuildKoreanPool(string koreanWord, List<string> allChars)
    {
        // 1. 정답 한글 글자 먼저 추가
        List<string> pool = new List<string>();
        HashSet<string> wordChars = new HashSet<string>();
        foreach (char c in koreanWord)
        {
            // 한글 음절 블록만 허용 — 공백·자모·특수문자 제외
            if (c < '가' || c > '힣') continue;
            string cs = c.ToString();
            pool.Add(cs);
            wordChars.Add(cs);
        }

        // 2. 정답에 없는 글자들로 나머지 채우기
        List<string> others = new List<string>();
        foreach (string ch in allChars)
            if (!wordChars.Contains(ch))
                others.Add(ch);

        // 나머지 글자 셔플
        for (int i = others.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (others[i], others[j]) = (others[j], others[i]);
        }

        // 26개 채우기
        int needed = 26 - pool.Count;
        for (int i = 0; i < Mathf.Min(needed, others.Count); i++)
            pool.Add(others[i]);

        // 전체 셔플
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    // 단어 글자(중복 포함) + 나머지 랜덤 글자로 26개 풀 구성
    List<char> BuildLetterPool(string word)
    {
        // 1. 정답 단어 글자 먼저 추가 (중복 포함 - 예: APPLE → A,P,P,L,E)
        List<char> pool = new List<char>(word.ToCharArray());

        // 2. 단어에 포함된 고유 글자 집합
        HashSet<char> wordChars = new HashSet<char>(word);

        // 3. 단어에 없는 글자들로 나머지 채우기
        List<char> others = new List<char>();
        for (char c = 'A'; c <= 'Z'; c++)
            if (!wordChars.Contains(c))
                others.Add(c);

        // 나머지 글자 셔플
        for (int i = others.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (others[i], others[j]) = (others[j], others[i]);
        }

        // 26개가 될 때까지 채우기
        int needed = 26 - pool.Count;
        for (int i = 0; i < Mathf.Min(needed, others.Count); i++)
            pool.Add(others[i]);

        // 전체 셔플
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    // Blue×6, Green×6, Yellow×5, Red×5, Purple×4 = 26개 색상 풀
    List<Sprite> BuildColorPool()
    {
        int[] counts = { 6, 6, 5, 5, 4 };
        List<Sprite> pool = new List<Sprite>();

        for (int i = 0; i < counts.Length; i++)
            for (int j = 0; j < counts[i]; j++)
                pool.Add(bubbleSprites[i]);

        // 색상 셔플
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    void SpawnOne(string letter, Sprite bubbleSprite, TMP_FontAsset font = null)
    {
        // 생성 범위 계산
        float halfW, halfH;
        if (background != null)
        {
            Bounds b = background.bounds;
            halfW = b.extents.x;
            halfH = b.extents.y;
        }
        else
        {
            Camera cam = Camera.main;
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
        }

        // topWall 아래에만 생성되도록 Y 상한 제한
        float spawnMaxY = ScreenWallFitter.TopBoundaryY - bubbleScale * 0.5f;

        GameObject obj = Instantiate(prefab);
        obj.transform.position = new Vector3(
            Random.Range(-halfW, halfW),
            Random.Range(-halfH, spawnMaxY),
            0f
        );

        // 버블 가로 = wallGap × bubbleScale 이 되도록 스프라이트 폭 기준으로 localScale 환산
        // (DropletBurst / PassWallCharacter 와 동일한 방식 — wallGap 기준 비례 스케일)
        float fullW = ScreenWallFitter.HalfW * 2f;
        if (fullW <= 0f && Camera.main != null)
            fullW = Camera.main.orthographicSize * Camera.main.aspect * 2f;
        float spriteW = bubbleSprite != null ? bubbleSprite.bounds.size.x : 0f;
        float worldScale = (fullW > 0f && spriteW > 0f)
            ? (fullW * bubbleScale) / spriteW    // wallGap 비율 기반 스케일
            : bubbleScale;                       // fallback: 기존 동작
        obj.transform.localScale = Vector3.one * worldScale;
        // 버블 이미지와 글자를 하나의 유닛으로 묶어 다른 버블과 독립 렌더링
        obj.AddComponent<SortingGroup>();

        // 버블 배경 이미지 설정
        SpriteRenderer sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = bubbleSprite;
            sr.sortingOrder = 0;
        }

        // 글자 TMP 텍스트 생성
        GameObject textObj = new("LetterText");
        textObj.transform.SetParent(obj.transform, false);

        TMP_Text tmp = textObj.AddComponent<TextMeshPro>();
        // fontMaterial 접근 전에 폰트 지정 — 순서 바꾸면 잘못된 머티리얼 참조됨
        if (font != null) tmp.font = font;
        tmp.text = letter;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(11f, 11f);
        tmp.rectTransform.anchoredPosition = new Vector2(0f, 0.6f);
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.extraPadding = true;
        tmp.GetComponent<Renderer>().sortingOrder = 1;

        // 글자 스타일 적용
        tmp.color = Color.white;
        // tmp.fontStyle = FontStyles.Bold;
        Material mat = tmp.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.4f);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.1f, 0.3f, 0.9f));
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.4f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.05f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.05f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);

        // AlphabetClick 초기화
        if (obj.TryGetComponent(out AlphabetClick click))
            click.Init(letter);

        spawnedObjects.Add(obj);
    }
}
