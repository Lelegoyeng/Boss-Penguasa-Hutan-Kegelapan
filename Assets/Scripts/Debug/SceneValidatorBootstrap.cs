using UnityEngine;

public static class SceneValidatorBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureSceneValidator()
    {
        // Cek apakah sudah ada SceneValidator di scene
        var existing = FindFirst<SceneValidator>();
        if (existing != null) return;

        // Buat GameObject baru dan pasang SceneValidator default
        var go = new GameObject("SceneValidator");
        var validator = go.AddComponent<SceneValidator>();
        validator.verbose = true;
        validator.enableAutoFix = true;
        validator.preferCinemachine = true;

        Debug.Log("[SceneValidatorBootstrap] Menambahkan SceneValidator otomatis ke scene (enableAutoFix=ON, preferCinemachine=ON).");
    }

    // Versi-aman untuk mencari instance pertama dari tipe tertentu, tanpa API deprecated
    static T FindFirst<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#elif UNITY_2022_2_OR_NEWER
        var arr = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return (arr != null && arr.Length > 0) ? arr[0] : null;
#else
#pragma warning disable CS0618
        return UnityEngine.Object.FindObjectOfType<T>();
#pragma warning restore CS0618
#endif
    }
}
