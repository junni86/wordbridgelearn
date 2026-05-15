using System.Collections;
using UnityEngine;

// BombMan 미니게임의 폭탄 1개
// - 설치 후 fuseDuration(기본 2초) 카운트다운
// - 폭발 시점에 자기 셀(중앙) + 상하좌우 4셀에 영향
//   · 벽돌 → BombManMiniGame.OnBrickExploded 호출
//   · 글자 → BombManMiniGame.OnLetterExploded 호출
//   · 캐릭터가 영향 셀에 있으면 BombManMiniGame.OnCharacterCaught 호출
// - 시각 효과: fuse 동안 살짝 깜빡임 → 폭발 순간 화면에 짧은 폭발 스프라이트(자식 sortOrder 위로) 후 0.2초 뒤 자기 제거
public class BombManBomb : MonoBehaviour
{
    [Tooltip("설치 후 폭발까지 걸리는 시간 (초)")]
    public float fuseDuration = 2f;

    [Tooltip("폭발 시각 효과가 화면에 머무는 시간 (초)")]
    public float explosionVisibleDuration = 0.2f;

    [System.NonSerialized] public int col = -1;
    [System.NonSerialized] public int row = -1;

    BombManMiniGame owner;

    public void Init(int assignedCol, int assignedRow, BombManMiniGame ownerGame)
    {
        col = assignedCol;
        row = assignedRow;
        owner = ownerGame;
        StartCoroutine(FuseRoutine());
    }

    IEnumerator FuseRoutine()
    {
        // fuse 동안 살짝 깜빡임 — sprite scale을 0.1초 주기로 약간 변화
        Vector3 baseScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < fuseDuration)
        {
            float t = Mathf.PingPong(elapsed * 4f, 1f);
            transform.localScale = baseScale * Mathf.Lerp(0.9f, 1.1f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = baseScale;

        // 폭발
        Explode();

        // 짧은 시간 뒤 자기 제거 (시각 효과 잔상 시간)
        yield return new WaitForSeconds(explosionVisibleDuration);

        if (owner != null) owner.OnBombFinished(this);
        Destroy(gameObject);
    }

    void Explode()
    {
        if (owner == null) return;

        // 영향 받는 셀들: 자기 셀 + 상하좌우 4셀
        (int, int)[] cells = new (int, int)[]
        {
            (col, row),
            (col, row - 1), // 위
            (col, row + 1), // 아래
            (col - 1, row), // 왼쪽
            (col + 1, row), // 오른쪽
        };

        // 폭발 시각 효과 — 자기 폭탄 자리에 흰색 원으로 잠깐 표시 (스프라이트 컬러 변경)
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(1f, 1f, 0.4f, 0.9f); // 노랑/주황 폭발 느낌
            transform.localScale *= 1.3f;
        }

        // 상하좌우 4방향에 물줄기 이미지 표시 (BombManMiniGame이 0.3초 후 자동 제거)
        owner.SpawnBlasts(col, row);

        foreach (var (c, r) in cells)
            owner.OnExplosionHit(c, r);
    }
}
