using System.Collections.Generic;
using UnityEngine;

// BombMan 미니게임 — 봄버맨 스타일
// - 9×11 격자 안에 캐릭터/벽돌/글자 배치
// - 캐릭터를 더블 탭하면 자리에 폭탄 설치 (2초 후 폭발)
// - 폭발은 자기 셀 + 상하좌우 1셀에 영향
//   · 벽돌 → 제거
//   · 글자 → 호스트에 입력 전달 (정답이면 글자 제거, 오답이면 기본 패널티)
//   · 캐릭터가 영향 셀에 있으면 자폭 → 점수 -2 + 진동 + 0.5초 정지
public class BombManMiniGame : MiniGameBase
{
    public static BombManMiniGame Instance { get; private set; }

    [Header("스포너 / 캐릭터")]
    public BombManSpawner spawner;
    public BombManCharacter character;

    [Header("프리팹")]
    public GameObject bombPrefab;        // 폭탄 (없으면 동적 폴백)
    public GameObject dropletPrefab;     // 정답 시 파티클 (선택)
    public GameObject wrongMarkPrefab;   // 오답 X 표시
    public GameObject waterBlastPrefab;  // 폭발 시 공통 폴백 — 방향별 슬롯이 비어있을 때 사용 (이것도 없으면 imageDrop 폴백)

    [Header("폭발 5개 위치별 이미지 (비우면 waterBlastPrefab → imageDrop 폴백)")]
    public GameObject blastCenterPrefab; // 가운데 (폭탄 자리)
    public GameObject blastUpPrefab;     // 위
    public GameObject blastDownPrefab;   // 아래
    public GameObject blastLeftPrefab;   // 왼쪽
    public GameObject blastRightPrefab;  // 오른쪽

    [Header("이펙트 타이밍")]
    [Tooltip("폭발 시 물줄기 시각이 화면에 머무는 시간 (초)")]
    public float blastVisibleDuration = 0.2f;

    // 그리드 단일 진실원천 — 0=empty, 1=brick, 2=letter, 3=bomb (캐릭터는 별도)
    int[,] cellType;
    BombManBrick[,] bricks;
    BombManLetter[,] letters;
    BombManBomb[,] bombs;

    int Cols => spawner != null ? spawner.Cols : 0;
    int Rows => spawner != null ? spawner.Rows : 0;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 새 단어 시작 — 그리드 다시 빌드 + 캐릭터 시작 위치 재설정
    public override void BeginWord(string answer, List<string> koreanCharPool, bool isKorean)
    {
        if (spawner == null) return;

        // 기존 폭탄 정리
        CleanupBombs();

        var result = spawner.Build(answer, koreanCharPool, isKorean);
        cellType = result.cellType;
        bricks = result.bricks;
        letters = result.letters;
        bombs = new BombManBomb[Cols, Rows];

        // 화면 크기 변경 시 spawner.RelayoutAll이 최신 그리드를 참조하도록 동기화
        spawner.SyncGridRefs(cellType, bricks, letters);

        // 캐릭터 시작 위치 적용
        if (character != null)
            character.Init(this, spawner, result.charCol, result.charRow);
    }

    public override void Cleanup()
    {
        CleanupBombs();
        if (spawner != null) spawner.DestroyAll();
        cellType = null;
        bricks = null;
        letters = null;
        bombs = null;
    }

    void CleanupBombs()
    {
        if (bombs == null) return;
        for (int r = 0; r < bombs.GetLength(1); r++)
            for (int c = 0; c < bombs.GetLength(0); c++)
                if (bombs[c, r] != null) Destroy(bombs[c, r].gameObject);
        bombs = null;
    }

    // 정답 입력 — 폭발로 닿은 글자가 정답일 때 호스트가 호출
    public override void HandleCorrect(MonoBehaviour source, int filledIndex, string letter)
    {
        if (source == null) return;
        BombManLetter bl = source as BombManLetter;
        if (bl == null) bl = source.GetComponent<BombManLetter>();
        if (bl == null) return;

        if (dropletPrefab != null)
            Instantiate(dropletPrefab, bl.transform.position, Quaternion.identity);

        // 그리드에서 제거
        if (InRange(bl.col, bl.row))
        {
            cellType[bl.col, bl.row] = 0;
            letters[bl.col, bl.row] = null;
        }
        Destroy(bl.gameObject);
    }

    // 오답 입력 — 글자는 유지하고 X마크만 0.5초간 표시 (다른 미니게임과 동일 동작)
    public override void HandleWrong(MonoBehaviour source, string expected)
    {
        if (source == null) return;
        ShowWrongMark(source.transform);
    }

    void ShowWrongMark(Transform parent)
    {
        if (wrongMarkPrefab == null || parent == null) return;
        GameObject mark = Instantiate(wrongMarkPrefab, parent);
        Destroy(mark, 0.5f);
    }

    // ─── 캐릭터 / 폭탄 ─────────────────────────────────────────────

    // 캐릭터가 (c,r)로 이동 가능한지 — 그리드 안 + 빈 셀
    public bool IsWalkable(int c, int r)
    {
        if (!InRange(c, r)) return false;
        return cellType[c, r] == 0;
    }

    // 캐릭터에서 더블 탭 — 자기 셀에 폭탄 설치 요청
    public void RequestPlaceBomb(int c, int r)
    {
        if (!InRange(c, r)) return;
        if (cellType[c, r] != 0) return;          // 이미 뭔가 있으면 설치 X
        if (bombs[c, r] != null) return;          // 이미 폭탄

        Vector3 pos = spawner.ComputeCellPosCurrent(c, r);
        BombManBomb bomb = CreateBombObject(pos);
        bomb.col = c;
        bomb.row = r;
        bombs[c, r] = bomb;
        cellType[c, r] = 3;                       // 그리드도 폭탄으로 마킹 (캐릭터는 빠져나갈 수 있게 walkable 판정엔 영향)
        // 단, 캐릭터가 폭탄 위에서 빠져나간 뒤 그 셀로 다시 들어오면 안 됨 — 위의 IsWalkable이 type==0만 통과시키므로 OK
        bomb.Init(c, r, this);
    }

    // 폭탄 GameObject 생성 — bombPrefab이 있으면 사용, 없으면 폴백 (단색 원)
    BombManBomb CreateBombObject(Vector3 pos)
    {
        GameObject obj;
        if (bombPrefab != null)
        {
            obj = Instantiate(bombPrefab);
        }
        else
        {
            obj = new GameObject("Bomb");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircleSprite();
            sr.color = Color.black;
            sr.sortingOrder = 5;
        }
        obj.transform.SetParent(spawner.transform, false);
        obj.transform.position = pos;

        // 셀 크기에 맞춰 스케일
        float cellSize = spawner.CurrentCellSize;
        var srMain = obj.GetComponentInChildren<SpriteRenderer>();
        float spriteW = (srMain != null && srMain.sprite != null) ? srMain.sprite.bounds.size.x : 1f;
        if (spriteW <= 0f) spriteW = 1f;
        obj.transform.localScale = Vector3.one * (cellSize * 0.7f / spriteW);

        var bomb = obj.GetComponent<BombManBomb>();
        if (bomb == null) bomb = obj.AddComponent<BombManBomb>();
        return bomb;
    }

    // 폭탄에서 폭발 시 호출 — 중앙 셀 + 상하좌우 4방향(총 5칸)에 물줄기 이미지를 0.3초 표시
    // 각 위치마다 방향 전용 prefab 사용 (비어있으면 공통 waterBlastPrefab → imageDrop 폴백)
    public void SpawnBlasts(int centerCol, int centerRow)
    {
        if (spawner == null) return;

        // (col, row, 방향별 prefab) — 5개 위치 각각 다른 이미지 가능
        (int c, int r, GameObject pf)[] cells = new (int, int, GameObject)[]
        {
            (centerCol,     centerRow,     blastCenterPrefab), // 가운데
            (centerCol,     centerRow - 1, blastUpPrefab),     // 위
            (centerCol,     centerRow + 1, blastDownPrefab),   // 아래
            (centerCol - 1, centerRow,     blastLeftPrefab),   // 왼쪽
            (centerCol + 1, centerRow,     blastRightPrefab),  // 오른쪽
        };

        float cellSize = spawner.CurrentCellSize;
        foreach (var item in cells)
        {
            // 그리드 밖이면 표시 생략
            if (item.c < 0 || item.r < 0 || item.c >= Cols || item.r >= Rows) continue;

            Vector3 pos = spawner.ComputeCellPosCurrent(item.c, item.r);
            GameObject blast = CreateBlastObject(pos, item.pf);
            if (blast == null) continue;

            // 셀 크기에 맞춰 스케일
            var sr = blast.GetComponentInChildren<SpriteRenderer>();
            float spriteW = (sr != null && sr.sprite != null) ? sr.sprite.bounds.size.x : 1f;
            if (spriteW <= 0f) spriteW = 1f;
            blast.transform.localScale = Vector3.one * (cellSize / spriteW);

            Destroy(blast, blastVisibleDuration);
        }
    }

    // 물줄기 GameObject 생성 — 방향 전용 prefab → 공통 waterBlastPrefab → imageDrop 순으로 폴백
    GameObject CreateBlastObject(Vector3 pos, GameObject directionalPrefab)
    {
        GameObject obj;
        if (directionalPrefab != null)
        {
            obj = Instantiate(directionalPrefab);
        }
        else if (waterBlastPrefab != null)
        {
            obj = Instantiate(waterBlastPrefab);
        }
        else
        {
            // 폴백: Resources/imageDrop sprite로 동적 생성
            Sprite dropSprite = Resources.Load<Sprite>("imageDrop");
            if (dropSprite == null) return null;
            obj = new GameObject("WaterBlast");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = dropSprite;
            sr.color = new Color(0.4f, 0.7f, 1f, 0.9f); // 옅은 파랑 — 물줄기 느낌
            sr.sortingOrder = 6; // 폭탄(5)보다 위, 캐릭터(10)보다 아래
        }
        obj.transform.SetParent(spawner.transform, false);
        obj.transform.position = pos;
        return obj;
    }

    // 폭탄에서 폭발 시 셀 1개에 호출 (자기 셀 + 상하좌우 4개 = 총 5번 호출)
    public void OnExplosionHit(int c, int r)
    {
        if (!InRange(c, r)) return;
        int type = cellType[c, r];

        // 캐릭터가 이 셀 위에 있으면 자폭 처리 — 셀 타입에 관계없이 영향
        if (character != null && character.CurrentCol == c && character.CurrentRow == r)
        {
            HandleSelfCaught();
        }

        if (type == 1)
        {
            // 벽돌 제거
            var brick = bricks[c, r];
            if (brick != null) Destroy(brick.gameObject);
            bricks[c, r] = null;
            cellType[c, r] = 0;
        }
        else if (type == 2)
        {
            // 글자 — 호스트로 입력 전달. 호스트가 정답이면 HandleCorrect → 글자 제거,
            // 오답이면 HandleWrong → X마크 + 점수 차감 (글자는 유지)
            var bl = letters[c, r];
            if (bl != null && !bl.consumed)
            {
                bl.consumed = true; // 같은 폭발 중 중복 입력 방지
                NotifyLetterTyped(bl.letter, bl);
            }
        }
        // type==3(폭탄) 또는 type==0(empty)은 추가 처리 없음
    }

    // 폭탄이 자기 LifeCycle을 끝낼 때 호출 — bombs 그리드/셀 타입 정리
    public void OnBombFinished(BombManBomb bomb)
    {
        if (bomb == null) return;
        int c = bomb.col, r = bomb.row;
        if (!InRange(c, r)) return;
        bombs[c, r] = null;
        // 폭발 후 글자가 정답이면 이미 제거됐고, 오답이면 글자가 유지됨
        // → cellType은 그 처리로 이미 0(정답) 또는 2(오답) 상태일 것
        // 폭탄 셀 자체는 비워줌
        if (cellType[c, r] == 3) cellType[c, r] = 0;

        // 폭발 직후 같은 폭발 영역의 글자 consumed 플래그 리셋은 불필요 —
        // 글자가 살아남았다면 그 글자 위에 새 폭탄이 또 터질 때 다시 입력으로 들어가야 함
        // 단, 다른 폭탄이 같은 글자를 친 경우엔 consumed가 풀려야 하므로 모든 글자 리셋
        ResetLetterConsumed();
    }

    void ResetLetterConsumed()
    {
        if (letters == null) return;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (letters[c, r] != null) letters[c, r].consumed = false;
    }

    // 자기 폭탄에 휘말림 — 오답과 같은 패널티
    void HandleSelfCaught()
    {
        // 점수 차감 + 진동은 호스트의 표준 오답 경로를 빌려옴
        // 빈 글자(string.Empty)로 NotifyLetterTyped를 호출하면 expected와 불일치 → wrong 처리
        // 단, HandleWrong이 source에 의존하므로 character를 source로 전달 (X마크는 character에 잠깐 표시)
        if (character != null) character.OnCaughtByExplosion();

        // 점수 차감/진동만 발생시키기 위해 임시 비공개 경로 사용 — Settings/Score 직접 호출이 더 깔끔
        SettingsManager.TryVibrate();

        // 호스트 점수 차감 — 직접 score를 만질 수는 없으므로 wrong 입력을 흉내냄
        // (currentIndex 글자 expected와 다른 글자를 보내면 호스트가 점수 -2 + resultText 갱신)
        if (GameHostManager.Instance != null)
            GameHostManager.Instance.OnLetterTyped("\0", character); // null char → 절대 정답 X
    }

    bool InRange(int c, int r) => cellType != null && c >= 0 && r >= 0 && c < Cols && r < Rows;

    // 힌트 — 그리드 위 글자 셀 중 다음 입력 글자(nextLetter)와 일치하는 첫 셀 1개만 잠시 키움
    public override void ShowHint(string nextLetter, float duration)
    {
        if (string.IsNullOrEmpty(nextLetter) || letters == null) return;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                var bl = letters[c, r];
                if (bl == null) continue;
                if (bl.letter == nextLetter)
                {
                    StartCoroutine(AnimateHintHighlight(new List<Transform> { bl.transform }, duration));
                    return;
                }
            }
        }
    }

    // 폴백 원 스프라이트 생성 (8×8 흰색 원형 텍스처) — bombPrefab 없을 때만 사용
    Sprite cachedCircle;
    Sprite MakeCircleSprite()
    {
        if (cachedCircle != null) return cachedCircle;
        int size = 32;
        Texture2D tex = new(size, size);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : new Color(0, 0, 0, 0));
            }
        tex.Apply();
        cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedCircle;
    }
}
