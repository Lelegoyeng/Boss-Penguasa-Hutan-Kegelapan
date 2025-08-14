using UnityEngine;
using System.Collections;

public static class HitStop
{
    static bool running;

    public static void Do(float duration)
    {
        if (running) return;
        var beh = GetHelper();
        beh.StartCoroutine(CoHitStop(duration));
    }

    static IEnumerator CoHitStop(float duration)
    {
        running = true;
        float original = Time.timeScale;
        Time.timeScale = 0.0001f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = original;
        running = false;
    }

    static HitStopHelper _helper;
    static HitStopHelper GetHelper()
    {
        if (_helper) return _helper;
        var go = new GameObject("__HitStopHelper");
        Object.DontDestroyOnLoad(go);
        _helper = go.AddComponent<HitStopHelper>();
        return _helper;
    }

    public class HitStopHelper : MonoBehaviour {}
}
