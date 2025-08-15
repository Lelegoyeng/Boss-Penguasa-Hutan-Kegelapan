#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossEpicSceneBuilder
{
    [MenuItem("Tools/Create Boss Epic Scene", priority = 0)]
    public static void CreateBossEpic()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var arenaRoot = new GameObject("Arena");
            CreateArenaFloorAndWalls(arenaRoot.transform, 120f, 120f, 0.5f, 6f, 1f);

            var wizardPrefab = FindPrefab(new[] { "t:Prefab PolyArtWizardStandardMat", "t:Prefab Wizard", "t:Prefab Mage" },
                                          new[] { "Assets/WizzardPoliArt", "Assets/WizardPolyArt", "Assets" });

            GameObject player = SetupPlayer(wizardPrefab);
            Camera mainCamera = SetupCamera(player.transform);

            SetupDragon();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BossEpic.unity");

            if (player)
            {
                Selection.activeObject = player;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            Debug.Log("[BossEpic] Successfully created Boss Epic Scene.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BossEpic] Failed to create scene: " + e.Message + "\n" + e.StackTrace);
        }
    }

    private static GameObject SetupPlayer(GameObject wizardPrefab)
    {
        if (!wizardPrefab)
        {
            Debug.LogWarning("[BossEpic] Wizard prefab not found. Creating a placeholder.");
            return new GameObject("Player_Placeholder");
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(wizardPrefab);
        player.name = "Player_Wizard";
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 0.2f, 0f);

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) 
        {
            cc = player.AddComponent<CharacterController>();
        }
        cc.center = new Vector3(0, 1, 0);
        cc.height = 1.8f;
        cc.radius = 0.3f;

        var anim = player.GetComponent<Animator>() ?? player.AddComponent<Animator>();
        var controller = BuildWizardAnimatorController();
        if (controller) anim.runtimeAnimatorController = controller;

        if (!player.GetComponent<WizardAnimationController>()) player.AddComponent<WizardAnimationController>();
        if (!player.GetComponent<WizardSimpleController>()) player.AddComponent<WizardSimpleController>();

        ApplyDefaultLitMaterials(player);
        return player;
    }

    private static Camera SetupCamera(Transform target)
    {
        var cam = Camera.main;
        if (!cam)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        var follow = cam.GetComponent<ThirdPersonFollow>() ?? cam.gameObject.AddComponent<ThirdPersonFollow>();
        follow.target = target;
        follow.distance = 5f;
        follow.minDistance = 2f;
        follow.maxDistance = 10f;
        follow.height = 2f;
        follow.minHeight = 1f;
        follow.maxHeight = 3f;
        follow.rotationSpeed = 5f;
        follow.zoomSpeed = 10f;
        follow.positionSmoothTime = 0.2f;
        follow.rotationSmoothTime = 0.2f;
        follow.collisionLayers = -1; // Collide with everything
        follow.cameraRadius = 0.3f;

        return cam;
    }

    private static void SetupDragon()
    {
        var dragonPrefab = FindPrefab(new[] { "t:Prefab Dragon", "t:Prefab BossDragon", "t:Prefab Wyvern" },
                                      new[] { "Assets", "Assets/Dragons", "Assets/Creatures" });
        if (dragonPrefab)
        {
            var dragon = (GameObject)PrefabUtility.InstantiatePrefab(dragonPrefab);
            dragon.name = "Boss_Dragon";
            dragon.transform.position = new Vector3(0f, 0.2f, 25f);
            if (!dragon.GetComponent<DragonChaseAI>()) dragon.AddComponent<DragonChaseAI>();
            ApplyDefaultLitMaterials(dragon);
        }
        else
        {
            Debug.LogWarning("[BossEpic] Dragon prefab not found.");
        }
    }

    private static RuntimeAnimatorController BuildWizardAnimatorController()
    {
        string[] roots = new[] { "Assets/WizzardPoliArt", "Assets/WizardPolyArt" };
        var clips = roots.Where(AssetDatabase.IsValidFolder)
                         .SelectMany(r => AssetDatabase.FindAssets("t:AnimationClip", new[] { r }))
                         .Distinct()
                         .Select(g => AssetDatabase.GUIDToAssetPath(g))
                         .Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p))
                         .Where(c => c && !c.name.StartsWith("__preview__"))
                         .ToDictionary(c => c.name.ToLowerInvariant(), c => c);

        if (clips.Count == 0) return null;

        System.IO.Directory.CreateDirectory("Assets/_Auto");
        string ctrlPath = "Assets/_Auto/WizardAuto.controller";
        AssetDatabase.DeleteAsset(ctrlPath);

        var ctrl = new AnimatorController();
        AssetDatabase.CreateAsset(ctrl, ctrlPath);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("AttackCombo", AnimatorControllerParameterType.Int);
        ctrl.AddParameter("Defend", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("GetHit", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var layer = new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f };
        var sm = new AnimatorStateMachine { name = "Base Layer", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ctrl);
        layer.stateMachine = sm;
        ctrl.AddLayer(layer);

        // Locomotion
        var idleClip = clips.Values.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("idle"));
        var walkClip = clips.Values.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("walk"));
        var runClip = clips.Values.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("run"));

        var locomotionTree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false, blendParameter = "Speed" };
        AssetDatabase.AddObjectToAsset(locomotionTree, ctrl);
        if (idleClip) locomotionTree.AddChild(idleClip, 0f);
        if (walkClip) locomotionTree.AddChild(walkClip, 0.5f);
        if (runClip) locomotionTree.AddChild(runClip, 1f);

        var locomotionState = sm.AddState("Locomotion");
        locomotionState.motion = locomotionTree;
        sm.defaultState = locomotionState;

        // Attacks
        for (int i = 1; i <= 3; i++)
        {
            var attackClip = clips.Values.FirstOrDefault(c => c.name.ToLowerInvariant().Contains($"attack_0{i}"));
            if (attackClip)
            {
                var attackState = sm.AddState($"Attack_{i}");
                attackState.motion = attackClip;
                var t = sm.AddAnyStateTransition(attackState);
                t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                t.AddCondition(AnimatorConditionMode.Equals, i, "AttackCombo");
                t.hasExitTime = false;
                t.duration = 0.1f;
                var exitT = attackState.AddTransition(locomotionState);
                exitT.hasExitTime = true;
                exitT.exitTime = 0.8f;
                exitT.duration = 0.25f;
            }
        }

        // Jump
        var jumpClip = clips.Values.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("jump"));
        if (jumpClip)
        {
            var jumpState = sm.AddState("Jump");
            jumpState.motion = jumpClip;
            var t = sm.AddAnyStateTransition(jumpState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            t.hasExitTime = false;
            t.duration = 0.1f;
            var exitT = jumpState.AddTransition(locomotionState);
            exitT.hasExitTime = true;
            exitT.exitTime = 0.8f;
            exitT.duration = 0.25f;
            exitT.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        }

        // Defend
        var defendClip = clips.Values.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("defend"));
        if (defendClip)
        {
            var defendState = sm.AddState("Defend");
            defendState.motion = defendClip;
            var t = locomotionState.AddTransition(defendState);
            t.AddCondition(AnimatorConditionMode.If, 1, "Defend");
            t.duration = 0.1f;
            var exitT = defendState.AddTransition(locomotionState);
            exitT.AddCondition(AnimatorConditionMode.IfNot, 1, "Defend");
            exitT.duration = 0.1f;
        }

        // Get Hit
        var getHitClip = clips.Values.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("gethit"));
        if (getHitClip)
        {
            var getHitState = sm.AddState("Get Hit");
            getHitState.motion = getHitClip;
            var t = sm.AddAnyStateTransition(getHitState);
            t.AddCondition(AnimatorConditionMode.If, 0, "GetHit");
            t.duration = 0.1f;
            t.hasExitTime = false;
            var exitT = getHitState.AddTransition(locomotionState);
            exitT.hasExitTime = true;
            exitT.exitTime = 0.8f;
            exitT.duration = 0.15f;
        }

        // Die
        var dieClip = clips.Values.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("die"));
        if (dieClip)
        { 
            var dieState = sm.AddState("Die");
            dieState.motion = dieClip;
            var t = sm.AddAnyStateTransition(dieState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Die");
            t.duration = 0.1f;
            t.hasExitTime = false;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return ctrl;
    }

    private static GameObject FindPrefab(string[] filters, string[] folders)
    {
        return filters.SelectMany(f => AssetDatabase.FindAssets(f, folders.Where(AssetDatabase.IsValidFolder).ToArray()))
                      .Select(AssetDatabase.GUIDToAssetPath)
                      .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                      .FirstOrDefault(p => p != null);
    }

    private static void CreateArenaFloorAndWalls(Transform parent, float width, float length, float floorThickness, float wallHeight, float wallThickness)
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader lit = rp ? (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit")) : Shader.Find("Standard");
        if (!lit) { Debug.LogWarning("Could not find a Lit shader."); return; }

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(parent);
        floor.transform.localScale = new Vector3(width / 10f, 1f, length / 10f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = new Material(lit) { color = new Color(0.35f, 0.5f, 0.35f, 1f) };

        void MakeWall(Vector3 pos, Vector3 scale) {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(lit) { color = new Color(0.5f, 0.5f, 0.55f, 1f) };
        }

        MakeWall(new Vector3(0, wallHeight * 0.5f,  length * 0.5f), new Vector3(width, wallHeight, wallThickness));
        MakeWall(new Vector3(0, wallHeight * 0.5f, -length * 0.5f), new Vector3(width, wallHeight, wallThickness));
        MakeWall(new Vector3( width * 0.5f, wallHeight * 0.5f, 0), new Vector3(wallThickness, wallHeight, length));
        MakeWall(new Vector3(-width * 0.5f, wallHeight * 0.5f, 0), new Vector3(wallThickness, wallHeight, length));
    }

    private static void ApplyDefaultLitMaterials(GameObject root)
    {
        if (!root) return;
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader lit = rp ? (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit")) : Shader.Find("Standard");
        if (!lit) return;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r && (r.sharedMaterials == null || r.sharedMaterials.Any(m => m == null)))
            {
                r.sharedMaterial = new Material(lit) { color = new Color(0.7f, 0.7f, 0.75f, 1f) };
            }
        }
    }
}

#endif
