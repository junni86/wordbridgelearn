using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

// 지렁이 미니게임용 버블 스포너
// - 정답 단어 글자(중복 포함) + 랜덤 10개 = 총 N+10개 생성
// - 버블 크기는 기존 게임(0.5)의 절반(0.25)
public class WormGameSpawner : MonoBehaviour
{
    public static WormGameSpawner Instance { get; private set; }

    public GameObject prefab;          // 알파벳 버블 프리팹 (기존 게임과 공용 가능)
    public SpriteRenderer background;  // 생성 범위 기준 배경

    [SerializeField] float bubbleScale = 0.25f;        // 기존 게임의 절반 크기
    [SerializeField] float fontSize = 6f;              // 글자 크기 (기존과 동일)
    [SerializeField] int randomBubbleCount = 10;       // 정답 외 추가 랜덤 개수
    [SerializeField] TMP_FontAsset koreanBubbleFont;   // 한글 모드 전용 폰트

    Sprite[] bubbleSprites;                    // 버블 색상 스프라이트 5종
    readonly List<GameObject> spawnedObjects = new();

    void Awake()
    {
        Instance = this;
        LoadBubbleSprites();
    }

    // Resources 폴더에서 버블 색상 스프라이트 로드 (기존 AlphabetSpawner와 동일)
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

    // 영어 단어 — 정답 글자 + 랜덤 10개 버블 생성
    public void SpawnAll(string word)
    {
        DestroyAll();

        List<string> pool = BuildEnglishPool(word.ToUpper());
        List<Sprite> colorPool = BuildColorPool(pool.Count);

        for (int i = 0; i < pool.Count; i++)
            SpawnOne(pool[i], colorPool[i]);
    }

    // 한글 단어 — 정답 글자 + 랜덤 10개 버블 생성
    public void SpawnAllKorean(string koreanWord, List<string> charPool)
    {
        DestroyAll();

        List<string> pool = BuildKoreanPool(koreanWord, charPool);
        List<Sprite> colorPool = BuildColorPool(pool.Count);

        for (int i = 0; i < pool.Count; i++)
            SpawnOne(pool[i], colorPool[i], koreanBubbleFont);
    }

    // 정답 글자(중복 포함) + 단어에 없는 랜덤 글자 randomBubbleCount개로 풀 구성
    List<string> BuildEnglishPool(string word)
    {
        List<string> pool = new();

        // 1. 정답 단어 글자 먼저 추가 (중복 포함 - 예: BOOK → B,O,O,K)
        foreach (char c in word) pool.Add(c.ToString());

        // 2. 단어에 포함된 고유 글자 집합
        HashSet<char> wordChars = new(word);

        // 3. 단어에 없는 글자 후보 수집
        List<string> others = new();
        for (char c = 'A'; c <= 'Z'; c++)
            if (!wordChars.Contains(c))
                others.Add(c.ToString());

        // 후보 셔플
        for (int i = others.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (others[i], others[j]) = (others[j], others[i]);
        }

        // 랜덤 글자 randomBubbleCount개 추가
        int needed = Mathf.Min(randomBubbleCount, others.Count);
        for (int i = 0; i < needed; i++) pool.Add(others[i]);

        // 전체 셔플 (정답 글자 위치도 섞기)
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    // 한글 정답 글자 + 단어에 없는 랜덤 글자 randomBubbleCount개로 풀 구성
    List<string> BuildKoreanPool(string koreanWord, List<string> allChars)
    {
        List<string> pool = new();
        HashSet<string> wordChars = new();

        // 1. 정답 한글 글자 먼저 추가
        foreach (char c in koreanWord)
        {
            // 한글 음절 블록만 허용 — 공백·자모·특수문자 제외
            if (c < '가' || c > '힣') continue;
            string cs = c.ToString();
            pool.Add(cs);
            wordChars.Add(cs);
        }

        // 2. 단어에 없는 한글 후보 수집
        List<string> others = new();
        if (allChars != null)
        {
            foreach (string ch in allChars)
                if (!wordChars.Contains(ch))
                    others.Add(ch);
        }

        // 후보 셔플
        for (int i = others.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (others[i], others[j]) = (others[j], others[i]);
        }

        // 랜덤 글자 randomBubbleCount개 추가
        int needed = Mathf.Min(randomBubbleCount, others.Count);
        for (int i = 0; i < needed; i++) pool.Add(others[i]);

        // 전체 셔플
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    // 색상 풀 — 5색을 균등 분배 후 셔플 (count개)
    List<Sprite> BuildColorPool(int count)
    {
        List<Sprite> pool = new();
        if (bubbleSprites == null || bubbleSprites.Length == 0) return pool;

        // 각 색상을 균등하게 사용
        for (int i = 0; i < count; i++)
            pool.Add(bubbleSprites[i % bubbleSprites.Length]);

        // 셔플
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    // 버블 한 개 생성 (AlphabetSpawner.SpawnOne과 동일 패턴, 크기만 절반)
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
        obj.transform.localScale = Vector3.one * bubbleScale;
        // 버블 이미지·글자 묶어 다른 버블과 독립 렌더링
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
        Material mat = tmp.fontMaterial;
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.4f);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.1f, 0.3f, 0.9f));
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.4f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.05f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.05f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);

        // AlphabetClick 데이터 초기화 — 컴포넌트는 비활성화 (지렁이 모드는 충돌로 판정)
        if (obj.TryGetComponent(out AlphabetClick click))
        {
            click.Init(letter);
            click.enabled = false; // OnMouseDown 비활성화 (터치는 지렁이 조종에 사용)
        }

        spawnedObjects.Add(obj);
    }
}
