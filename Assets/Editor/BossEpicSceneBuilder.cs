#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossEpicSceneBuilder
{
    [MenuItem("Tools/Create Boss Epic Scene", priority = 0)]
    public static void CreateBossEpic()
    {
        try
        {
            // Scene baru minimal (tanpa Terrain)
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Buat arena sederhana: lantai + dinding
            var arenaRoot = new GameObject("Arena");
            CreateArenaFloorAndWalls(arenaRoot.transform, 120f, 120f, 0.5f, 6f, 1f);

            // Kamera
            var cam = Camera.main;
            if (!cam)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.transform.position = new Vector3(0, 12, -18);
            cam.transform.rotation = Quaternion.Euler(15, 0, 0);

            // Prefab Wizard
            var wizardPrefab = FindPrefab(new[] { "t:Prefab PolyArtWizardStandardMat", "t:Prefab Wizard", "t:Prefab Mage" },
                                          new[] { "Assets/WizzardPoliArt", "Assets/WizardPolyArt", "Assets" });
            GameObject player = null;
            if (wizardPrefab)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(wizardPrefab);
                player.name = "Player_Wizard";
                player.tag = "Player";
                player.transform.position = new Vector3(0f, 0.2f, 0f);

                var cc = player.GetComponent<CharacterController>();
                if (!cc) cc = player.AddComponent<CharacterController>();

                var anim = player.GetComponent<Animator>();
                if (!anim) anim = player.AddComponent<Animator>();

                var controller = BuildWizardAnimatorController();
                if (controller) anim.runtimeAnimatorController = controller;

                if (!player.GetComponent<WizardSimpleController>())
                    player.AddComponent<WizardSimpleController>();

                // Kamera follow
                var follow = cam.GetComponent<ThirdPersonFollow>();
                if (!follow) follow = cam.gameObject.AddComponent<ThirdPersonFollow>();
                follow.target = player.transform;

                // Material fallback jika ada renderer tanpa material
                ApplyDefaultLitMaterials(player);
            }
            else
            {
                Debug.LogWarning("[BossEpic] Prefab Wizard tidak ditemukan.");
            }

            // Prefab Dragon
            var dragonPrefab = FindPrefab(new[] { "t:Prefab Dragon", "t:Prefab BossDragon", "t:Prefab Wyvern" },
                                          new[] { "Assets", "Assets/Dragons", "Assets/Creatures" });
            if (dragonPrefab)
            {
                var dragon = (GameObject)PrefabUtility.InstantiatePrefab(dragonPrefab);
                dragon.name = "Boss_Dragon";
                dragon.transform.position = new Vector3(0f, 0.2f, 25f);

                if (!dragon.GetComponent<DragonChaseAI>())
                    dragon.AddComponent<DragonChaseAI>();

                ApplyDefaultLitMaterials(dragon);
            }
            else
            {
                Debug.LogWarning("[BossEpic] Prefab Dragon tidak ditemukan.");
            }

            // Simpan scene
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BossEpic.unity");

            if (player)
            {
                Selection.activeObject = player;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            Debug.Log("[BossEpic] Selesai membuat scene BossEpic (tanpa Terrain).");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BossEpic] Gagal: " + e.Message);
        }
    }

    private static GameObject FindPrefab(string[] filters, string[] folders)
    {
        foreach (var f in filters)
        {
            string[] guids = (folders != null && folders.Length > 0)
                ? folders.Where(AssetDatabase.IsValidFolder).SelectMany(fd => AssetDatabase.FindAssets(f, new[] { fd })).ToArray()
                : AssetDatabase.FindAssets(f);
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab) return prefab;
            }
        }
        return null;
    }

    private static RuntimeAnimatorController BuildWizardAnimatorController()
    {
        string[] roots = new[] { "Assets/WizzardPoliArt", "Assets/WizardPolyArt" };
        var valid = roots.Where(AssetDatabase.IsValidFolder).ToArray();
        if (valid.Length == 0) return null;

        var guids = valid.SelectMany(r => AssetDatabase.FindAssets("t:AnimationClip", new[] { r })).Distinct().ToArray();
        var clips = guids.Select(g => AssetDatabase.GUIDToAssetPath(g))
                         .Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p))
                         .Where(c => c && !c.name.StartsWith("__preview__"))
                         .ToList();
        if (clips.Count == 0) return null;

        System.IO.Directory.CreateDirectory("Assets/_Auto");
        string ctrlPath = "Assets/_Auto/WizardAuto.controller";

        var existing = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
        if (existing != null)
        {
            bool invalid = existing.layers == null || existing.layers.Length == 0 || existing.layers[0].stateMachine == null;
            if (!invalid) return existing;
            AssetDatabase.DeleteAsset(ctrlPath);
        }

        var ctrl = new UnityEditor.Animations.AnimatorController();
        AssetDatabase.CreateAsset(ctrl, ctrlPath);

        var layer = new UnityEditor.Animations.AnimatorControllerLayer
        {
            name = "Base Layer",
            defaultWeight = 1f
        };
        var sm = new UnityEditor.Animations.AnimatorStateMachine { name = "Base Layer" };
        AssetDatabase.AddObjectToAsset(sm, ctrl);
        layer.stateMachine = sm;
        ctrl.AddLayer(layer);

        // BlendTree Idle/Move berdasarkan Speed bila tersedia
        var idleClip = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("idle") || c.name.ToLowerInvariant().Contains("wait"));
        var moveClip = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("run") || c.name.ToLowerInvariant().Contains("walk"));

        if (idleClip != null && moveClip != null)
        {
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var bt = new UnityEditor.Animations.BlendTree { name = "Locomotion", blendType = UnityEditor.Animations.BlendTreeType.Simple1D, useAutomaticThresholds = false, blendParameter = "Speed" };
            AssetDatabase.AddObjectToAsset(bt, ctrl);
            bt.AddChild(idleClip, 0f);
            bt.AddChild(moveClip, 1f);

            var locomotion = sm.AddState("Locomotion");
            locomotion.motion = bt;
            sm.defaultState = locomotion;
        }
        else
        {
            UnityEditor.Animations.AnimatorState defaultState = null;
            foreach (var clip in clips)
            {
                var st = sm.AddState(clip.name);
                st.motion = clip;
                if (defaultState == null) defaultState = st;
                var lower = clip.name.ToLowerInvariant();
                if (lower.Contains("idle") || lower.Contains("wait")) defaultState = st;
            }
            if (defaultState != null) sm.defaultState = defaultState;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return ctrl;
    }

    private static void CreateArenaFloorAndWalls(Transform parent, float width, float length, float floorThickness, float wallHeight, float wallThickness)
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader lit = rp == null
            ? Shader.Find("Standard")
            : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));

        // Floor (Plane 10x10, scale agar sesuai ukuran)
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(parent);
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(width / 10f, 1f, length / 10f);
        var fr = floor.GetComponent<MeshRenderer>();
        if (fr && lit)
        {
            var m = new Material(lit) { color = new Color(0.35f, 0.5f, 0.35f, 1f) };
            fr.sharedMaterial = m;
        }

        // Walls
        void MakeWall(Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr && lit)
            {
                var m = new Material(lit) { color = new Color(0.5f, 0.5f, 0.55f, 1f) };
                mr.sharedMaterial = m;
            }
        }

        // Utara/Selatan/Timur/Barat
        MakeWall(new Vector3(0, wallHeight * 0.5f,  length * 0.5f), new Vector3(width, wallHeight, wallThickness));
        MakeWall(new Vector3(0, wallHeight * 0.5f, -length * 0.5f), new Vector3(width, wallHeight, wallThickness));
        MakeWall(new Vector3( width * 0.5f, wallHeight * 0.5f, 0), new Vector3(wallThickness, wallHeight, length));
        MakeWall(new Vector3(-width * 0.5f, wallHeight * 0.5f, 0), new Vector3(wallThickness, wallHeight, length));
    }

    private static void ApplyDefaultLitMaterials(GameObject root)
    {
        if (!root) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader lit = rp == null
            ? Shader.Find("Standard")
            : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
        if (!lit) return;

        foreach (var r in renderers)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            bool hasNull = mats == null || mats.Length == 0 || System.Array.Exists(mats, m => m == null);
            if (hasNull)
            {
                var m = new Material(lit) { color = new Color(0.7f, 0.7f, 0.75f, 1f) };
                r.sharedMaterial = m;
            }
        }
    }
}
#endif
