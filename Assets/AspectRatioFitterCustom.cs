using UnityEngine;

public class AspectRatioFitterCustom : MonoBehaviour
{
    public float targetWidth = 1080f;
    public float targetHeight = 1920f;

    void Start()
    {
        float targetRatio = targetWidth / targetHeight;
        float screenRatio = (float)Screen.width / Screen.height;

        if (screenRatio > targetRatio)
        {
            float scale = targetRatio / screenRatio;
            transform.localScale = new Vector3(scale, 1, 1);
        }
        else
        {
            float scale = screenRatio / targetRatio;
            transform.localScale = new Vector3(1, scale, 1);
        }
    }
}