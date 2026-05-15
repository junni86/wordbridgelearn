using UnityEngine;

public class ScreenWallFitter : MonoBehaviour
{
    public Transform topWall;
    public Transform bottomWall;
    public Transform leftWall;
    public Transform rightWall;

    public SpriteRenderer background;
    public float thickness = 0.5f;
    public float topOffset = 0f; // 상단 벽을 아래로 내릴 거리 (양수일수록 아래로)

    public static float HalfW { get; private set; }
    public static float HalfH { get; private set; }
    // topWall 하단 경계 Y (버블 생성 상한으로 사용)
    public static float TopBoundaryY { get; private set; }

    void Start() => FitWalls();
    void Update() => FitWalls();

    void FitWalls()
    {
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

        HalfW = halfW;
        HalfH = halfH;

        TopBoundaryY = halfH - topOffset - thickness / 2f;
        topWall.position = new Vector3(0, halfH - topOffset + thickness / 2f, 0);
        bottomWall.position = new Vector3(0, -halfH - thickness / 2f, 0);
        leftWall.position = new Vector3(-halfW - thickness / 2f, 0, 0);
        rightWall.position = new Vector3(halfW + thickness / 2f, 0, 0);

        float fullW = halfW * 2f;
        float fullH = halfH * 2f;

        topWall.localScale = new Vector3(fullW + thickness * 2f, thickness, 1);
        bottomWall.localScale = new Vector3(fullW + thickness * 2f, thickness, 1);
        leftWall.localScale = new Vector3(thickness, fullH + thickness * 2f, 1);
        rightWall.localScale = new Vector3(thickness, fullH + thickness * 2f, 1);
    }
}