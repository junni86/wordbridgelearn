using UnityEngine;

public class CameraFit : MonoBehaviour
{
    public float targetWidth = 1080f;
    public float targetHeight = 1920f;
    public float pixelsPerUnit = 100f;

    void Start()
    {
        float targetAspect = targetWidth / targetHeight;
        float screenAspect = (float)Screen.width / Screen.height;

        Camera cam = GetComponent<Camera>();

        if (screenAspect < targetAspect)
        {
            float difference = targetAspect / screenAspect;
            cam.orthographicSize = (targetHeight / pixelsPerUnit / 2f) * difference;
        }
        else
        {
            cam.orthographicSize = targetHeight / pixelsPerUnit / 2f;
        }
    }
}