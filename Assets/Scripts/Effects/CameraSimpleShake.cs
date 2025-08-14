using UnityEngine;

public class CameraSimpleShake : MonoBehaviour
{
    public static CameraSimpleShake Instance;
    public float trauma; // 0..1
    public float decay = 1.5f; // per second
    public float amplitude = 0.2f; // positional amplitude
    public float rotAmplitude = 1.5f; // rotational amplitude degrees

    Vector3 basePos;
    Quaternion baseRot;

    void Awake()
    {
        Instance = this;
        var cam = GetComponent<Camera>();
        if (!cam) cam = Camera.main;
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
    }

    void LateUpdate()
    {
        trauma = Mathf.Clamp01(trauma - decay * Time.deltaTime);
        float t = trauma * trauma; // non-linear
        float nx = (Mathf.PerlinNoise(Time.time * 20.7f, 1.23f) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(3.41f, Time.time * 18.9f) - 0.5f) * 2f;
        float nz = (Mathf.PerlinNoise(Time.time * 22.5f, 7.8f) - 0.5f) * 2f;
        transform.localPosition = basePos + new Vector3(nx, ny, 0f) * amplitude * t;
        transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, nz * rotAmplitude * t);
    }

    public static void Shake(float addTrauma)
    {
        if (Instance)
        {
            Instance.trauma = Mathf.Clamp01(Instance.trauma + addTrauma);
        }
    }
}
