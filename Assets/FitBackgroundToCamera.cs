using UnityEngine;

public class FitBackgroundToCamera : MonoBehaviour
{
    private int lastWidth;
    private int lastHeight;

    void Start()
    {
        ApplyFit();
        SaveScreenSize();
    }

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            ApplyFit();
            SaveScreenSize();
            Debug.Log("화면 크기 변경 감지 → 재설정");
        }
    }

    void SaveScreenSize()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
    }

    void ApplyFit()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Camera cam = Camera.main;

        float screenHeight = cam.orthographicSize * 2f;
        float screenWidth = screenHeight * cam.aspect;

        float spriteHeight = sr.sprite.bounds.size.y;
        float spriteWidth = sr.sprite.bounds.size.x;

        float scale = Mathf.Min(
            screenWidth / spriteWidth,
            screenHeight / spriteHeight
        );

        transform.localScale = new Vector3(scale, scale, 1f);
        transform.position = Vector3.zero;
    }
}