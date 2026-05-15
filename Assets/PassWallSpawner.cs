using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

// PassWall 미니게임 — 위에서 아래로 떨어지는 글자 행 스포너
// - 한 행 = 15개 슬롯 (정답 글자 우선 채우고 + 구멍 N개 + 나머지 랜덤)
// - 일정 간격(rowInterval)마다 새 행 반복 생성
// - 풍선은 PassWallBalloon이 등속 하강
public class PassWallSpawner : MonoBehaviour
{
    public GameObject prefab;          // 풍선 프리팹 (기존 알파벳 버블 프리팹 재사용)
    public SpriteRenderer background;  // 가로 범위 기준 배경 (없으면 카메라 기준)

    [Header("행/슬롯 설정")]
    [SerializeField] int slotsPerRow = 13;             // 한 행의 풍선 칸 수
    [SerializeField] int gapCount = 2;                 // 행마다 비울 구멍 개수
    // 풍선 폭이 한 슬롯의 몇 %를 차지할지 (0~1) — 화면 크기 무관하게 비율로 지정
    // 0.8 = 슬롯 폭의 80%. 1.0이면 슬롯에 빈틈없이 가득
    [SerializeField, Range(0.1f, 1f)] float bubbleWidthRatio = 0.8f;
    // 풍선 사이 여백을 "풍선 폭의 몇 %"로 지정 — 풍선이 작아지면 여백도 비례해서 작아짐
    // 기본 0.25 = 현재 시각적 결과 유지 (bubbleWidthRatio=0.8 기준 슬롯 폭과 동일한 간격)
    [SerializeField, Range(0f, 1f)] float bubbleGapRatio = 0.25f;
    [SerializeField] float fontSize = 6f;

    // 떠 있는 풍선이 실시간으로 화면 비율을 반영하도록 외부에 노출
    public int SlotsPerRow => slotsPerRow;
    public float BubbleWidthRatio => bubbleWidthRatio;
    public float BubbleGapRatio => bubbleGapRatio;

    // 슬롯 인덱스 i (0-based)에 해당하는 X 좌표 — 행 중앙 정렬, 새 레이아웃 수식
    // balloon_width = (fullW / N) * bubbleWidthRatio
    // gap_width     = balloon_width * bubbleGapRatio
    // spacing       = balloon_width + gap_width
    // 행 전체 = N*balloon + (N-1)*gap, 화면 중앙에 정렬
    public float ComputeSlotX(int slotIndex, float fullW)
    {
        if (slotsPerRow <= 0 || fullW <= 0f) return 0f;
        float balloonWidth = (fullW / slotsPerRow) * bubbleWidthRatio;
        float gapWidth = balloonWidth * bubbleGapRatio;
        float spacing = balloonWidth + gapWidth;
        float rowTotal = slotsPerRow * balloonWidth + (slotsPerRow - 1) * gapWidth;
        float startX = -rowTotal * 0.5f + balloonWidth * 0.5f;
        return startX + spacing * slotIndex;
    }
    [SerializeField] TMP_FontAsset koreanBubbleFont;   // 한글 모드 전용 폰트

    [Header("하강/스폰 타이밍")]
    [SerializeField] float fallSpeed = 1.2f;           // 풍선 하강 속도
    [SerializeField] float rowInterval = 3.5f;         // 새 행 생성 간격 (초)
    [SerializeField] float spawnOffsetY = 0.3f;        // topWall 위 얼마나 위에서 생성할지

    Sprite[] bubbleSprites;
    readonly List<GameObject> spawnedObjects = new();
    Coroutine spawnLoop;

    // 현재 단어 정보 (행 구성용)
    string currentAnswer = string.Empty;
    List<string> currentCharPool;
    bool currentIsKorean;
    bool isActive;

    void Awake()
    {
        LoadBubbleSprites();
    }

    void OnDisable()
    {
        StopSpawnLoop();
    }

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

    // 새 단어 시작 — 기존 풍선 모두 제거 후 스폰 루프 (재)시작
    public void BeginWord(string answer, List<string> charPool, bool isKorean)
    {
        currentAnswer = isKorean ? (answer ?? string.Empty) : (answer ?? string.Empty).ToUpper();
        currentCharPool = charPool;
        currentIsKorean = isKorean;

        DestroyAll();
        StopSpawnLoop();
        isActive = true;
        spawnLoop = StartCoroutine(SpawnLoop());
    }

    // 미니게임 종료 시 정리
    public void DestroyAll()
    {
        foreach (var obj in spawnedObjects)
            if (obj != null) Destroy(obj);
        spawnedObjects.Clear();
    }

    public void StopSpawnLoop()
    {
        isActive = false;
        if (spawnLoop != null)
        {
            StopCoroutine(spawnLoop);
            spawnLoop = null;
        }
    }

    // 일정 간격마다 새 행 생성 — BeginWord에서 시작, StopSpawnLoop에서 종료
    IEnumerator SpawnLoop()
    {
        // 첫 행은 즉시, 이후 rowInterval마다
        while (isActive)
        {
            SpawnRow();
            yield return new WaitForSeconds(rowInterval);
        }
    }

    // 행 하나 — slotsPerRow 칸을 가로로 균등 배치, gapCount만큼 풍선 생성 생략
    void SpawnRow()
    {
        // 가로 범위 계산
        float halfW;
        if (background != null) halfW = background.bounds.extents.x;
        else if (Camera.main != null) halfW = Camera.main.orthographicSize * Camera.main.aspect;
        else halfW = 5f;

        // 글자 풀 구성 (정답 우선 + 구멍 자리 + 나머지 랜덤)
        List<string> rowPool = BuildRowPool();

        // topWall 위에서 살짝 위쪽에 생성 — 화면 안으로 자연스럽게 등장
        float spawnY = ScreenWallFitter.TopBoundaryY + spawnOffsetY;

        // 슬롯 X 좌표 — ComputeSlotX가 행 중앙 정렬 + bubbleGapRatio 반영
        float fullW = halfW * 2f;
        float balloonWidth = (fullW / slotsPerRow) * bubbleWidthRatio;

        // 행 단위로 색상 풀 셔플 (각 행마다 색 균등 분배)
        List<Sprite> colorPool = BuildColorPool(slotsPerRow);

        for (int i = 0; i < slotsPerRow; i++)
        {
            string letter = rowPool[i];
            if (string.IsNullOrEmpty(letter)) continue; // 구멍 — 풍선 생성 생략

            float x = ComputeSlotX(i, fullW);
            SpawnOne(letter, new Vector3(x, spawnY, 0f), colorPool[i], balloonWidth, i);
        }
    }

    // 행 글자 구성: 정답 글자(중복 포함) → 빈 슬롯(구멍) → 나머지는 단어에 없는 랜덤 글자, 전체 셔플
    List<string> BuildRowPool()
    {
        List<string> pool = new();

        // 1. 정답 글자 (중복 포함) — 매 행 정답이 한 번씩은 등장하도록
        if (!string.IsNullOrEmpty(currentAnswer))
        {
            foreach (char c in currentAnswer)
            {
                if (currentIsKorean)
                {
                    if (c < '가' || c > '힣') continue;
                    pool.Add(c.ToString());
                }
                else
                {
                    pool.Add(c.ToString());
                }
            }
        }

        // 2. 구멍 N개 (빈 문자열 = 풍선 생성 안 함)
        for (int i = 0; i < gapCount; i++) pool.Add(string.Empty);

        // 정답 글자(중복 제거)는 나머지 채움에서 제외
        HashSet<string> wordChars = new();
        foreach (string s in pool)
            if (!string.IsNullOrEmpty(s)) wordChars.Add(s);

        // 3. 나머지 슬롯은 정답에 없는 글자로 채움
        List<string> others = new();
        if (currentIsKorean)
        {
            if (currentCharPool != null)
                foreach (string ch in currentCharPool)
                    if (!wordChars.Contains(ch)) others.Add(ch);
        }
        else
        {
            for (char c = 'A'; c <= 'Z'; c++)
            {
                string cs = c.ToString();
                if (!wordChars.Contains(cs)) others.Add(cs);
            }
        }

        // 후보 셔플
        for (int i = others.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (others[i], others[j]) = (others[j], others[i]);
        }

        // 부족분 채우기 — 후보가 모자라면 같은 글자가 반복될 수 있음 (그래도 정답 글자는 우선 등장)
        int needed = slotsPerRow - pool.Count;
        for (int i = 0; i < needed; i++)
        {
            if (others.Count == 0)
            {
                // 후보 고갈 — 영문이면 A, 한글이면 풀 첫 글자로 폴백
                pool.Add(currentIsKorean ? (currentCharPool != null && currentCharPool.Count > 0 ? currentCharPool[0] : "가") : "A");
            }
            else
            {
                pool.Add(others[i % others.Count]);
            }
        }

        // 전체 셔플 — 정답 글자 위치도 매번 랜덤
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    // 색상 풀 — 5색 균등 분배 후 셔플
    List<Sprite> BuildColorPool(int count)
    {
        List<Sprite> pool = new();
        if (bubbleSprites == null || bubbleSprites.Length == 0) return pool;

        for (int i = 0; i < count; i++)
            pool.Add(bubbleSprites[i % bubbleSprites.Length]);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }

    // 풍선 한 개 생성 (지렁이 게임 SpawnOne을 기반, PassWallBalloon 컴포넌트 부착)
    // balloonWidth: 풍선이 차지할 월드 폭 / slotIndex: 행 내 슬롯 인덱스 (실시간 X 재계산용)
    void SpawnOne(string letter, Vector3 pos, Sprite bubbleSprite, float balloonWidth, int slotIndex)
    {
        GameObject obj = Instantiate(prefab);
        obj.transform.position = pos;
        // 버블 이미지+글자를 한 단위로 묶어 다른 풍선과 독립 렌더링
        obj.AddComponent<SortingGroup>();

        // 버블 배경 스프라이트
        SpriteRenderer sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = bubbleSprite;
            sr.sortingOrder = 0;
        }

        // 스프라이트 원본 폭(scale=1 기준)으로부터 목표 월드 폭(balloonWidth)에 맞는 scale 계산
        float spriteWidth = (sr != null && sr.sprite != null) ? sr.sprite.bounds.size.x : 1f;
        if (spriteWidth <= 0f) spriteWidth = 1f;
        float scale = balloonWidth / spriteWidth;
        obj.transform.localScale = Vector3.one * scale;

        // 글자 TMP 텍스트
        GameObject textObj = new("LetterText");
        textObj.transform.SetParent(obj.transform, false);

        TMP_Text tmp = textObj.AddComponent<TextMeshPro>();
        if (currentIsKorean && koreanBubbleFont != null) tmp.font = koreanBubbleFont;
        tmp.text = letter;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(11f, 11f);
        tmp.rectTransform.anchoredPosition = new Vector2(0f, 0.6f);
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.extraPadding = true;
        tmp.GetComponent<Renderer>().sortingOrder = 1;

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

        // AlphabetClick(있다면)은 클릭 입력에 사용되지 않도록 비활성화 + 데이터 초기화
        if (obj.TryGetComponent(out AlphabetClick click))
        {
            click.Init(letter);
            click.enabled = false;
        }

        // 등속 하강용 PassWallBalloon 부착 — RandomMove와 같이 있으면 충돌 — RandomMove가 있다면 제거
        if (obj.TryGetComponent(out RandomMove rm)) Destroy(rm);

        PassWallBalloon balloon = obj.GetComponent<PassWallBalloon>();
        if (balloon == null) balloon = obj.AddComponent<PassWallBalloon>();
        balloon.Init(letter, fallSpeed, this, slotIndex);

        spawnedObjects.Add(obj);
    }
}
