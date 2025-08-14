using UnityEngine;
using System.Collections;

public class CameraShakeService : MonoBehaviour
{
    public static CameraShakeService Instance { get; private set; }

    [Header("Shake Settings")] 
    public AnimationCurve intensityOverTime = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private Transform cam;
    private Vector3 originalLocalPos;
    private Coroutine running;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        cam = Camera.main ? Camera.main.transform : null;
        if (cam) originalLocalPos = cam.localPosition;
    }

    public void Shake(float duration = 0.3f, float magnitude = 0.2f)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        if (!cam) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = intensityOverTime.Evaluate(t / duration);
            cam.localPosition = originalLocalPos + Random.insideUnitSphere * (magnitude * k);
            yield return null;
        }
        cam.localPosition = originalLocalPos;
        running = null;
    }
}
