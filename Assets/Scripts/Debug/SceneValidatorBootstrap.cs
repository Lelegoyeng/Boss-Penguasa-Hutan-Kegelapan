using UnityEngine;

public static class SceneValidatorBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureSceneValidator()
    {
        // Cek apakah sudah ada SceneValidator di scene
        var existing = Object.FindObjectOfType<SceneValidator>();
        if (existing != null) return;

        // Buat GameObject baru dan pasang SceneValidator default
        var go = new GameObject("SceneValidator");
        var validator = go.AddComponent<SceneValidator>();
        validator.verbose = true;
        validator.enableAutoFix = true;
        validator.preferCinemachine = true;

        Debug.Log("[SceneValidatorBootstrap] Menambahkan SceneValidator otomatis ke scene (enableAutoFix=ON, preferCinemachine=ON).");
    }
}
