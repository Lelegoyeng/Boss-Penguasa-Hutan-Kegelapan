#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.IO;

public static class BossEpicSceneBuilder
{
    private static void CreateSpellBarUI(WizardSimpleController wizardController)
    {
        // Create Canvas and EventSystem
        var canvasGO = new GameObject("SpellCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        // Create SpellBar Panel
        var panelGO = new GameObject("SpellBarPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRect = panelGO.AddComponent<RectTransform>();
        
        // Anchor to top right corner
        panelRect.anchorMin = new Vector2(1, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = new Vector2(-20, -20); // Padding from corner
        panelRect.sizeDelta = new Vector2(300, 60); // Smaller size for 5 slots

        var hlg = panelGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.padding = new RectOffset(5, 5, 5, 5); // Reduced padding
        hlg.spacing = 10; // Reduced spacing

        var spellBarUI = panelGO.AddComponent<SpellBarUI>();
        spellBarUI.container = panelGO.transform;

        // Create SpellSlot Prefab
        var slotPrefab = CreateSpellSlotPrefab();
        spellBarUI.spellSlotPrefab = slotPrefab;

        // Link UI to Wizard Controller
        wizardController.spellBarUI = spellBarUI;
    }

    private static GameObject CreateSpellSlotPrefab()
    {
        // Create the hierarchy
        var slotGO = new GameObject("SpellSlot");
        var slotRect = slotGO.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(50, 50); // Smaller slot size
        slotGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        var slotUI = slotGO.AddComponent<SpellSlotUI>();

        var highlightGO = new GameObject("Highlight");
        highlightGO.transform.SetParent(slotGO.transform, false);
        var highlightRect = highlightGO.AddComponent<RectTransform>();
        highlightRect.sizeDelta = new Vector2(50, 50); // Match smaller size
        var highlightImage = highlightGO.AddComponent<Image>();
        highlightImage.color = new Color(1f, 0.8f, 0f, 0.6f);
        highlightImage.enabled = false;

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(slotGO.transform, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(45, 45); // Smaller icon
        var iconImage = iconGO.AddComponent<Image>();

        var keyTextGO = new GameObject("KeyText");
        keyTextGO.transform.SetParent(slotGO.transform, false);
        var keyTextRect = keyTextGO.AddComponent<RectTransform>();
        keyTextRect.sizeDelta = new Vector2(15, 15); // Smaller text box
        keyTextRect.anchorMin = new Vector2(1, 0);
        keyTextRect.anchorMax = new Vector2(1, 0);
        keyTextRect.pivot = new Vector2(1, 0);
        keyTextRect.anchoredPosition = new Vector2(-2, 2); // Adjust position
        var keyText = keyTextGO.AddComponent<Text>();
        keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        keyText.fontSize = 12; // Smaller font size
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.color = Color.white;

        // Link components to script
        slotUI.iconImage = iconImage;
        slotUI.highlightImage = highlightImage;
        slotUI.keyText = keyText;

        // Create Prefab
        string dirPath = "Assets/Resources/UI";
        if (!AssetDatabase.IsValidFolder(dirPath))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        }
        string prefabPath = dirPath + "/SpellSlot.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(slotGO, prefabPath);
        Object.DestroyImmediate(slotGO);

        return prefab;
    }

    [MenuItem("Tools/Create Boss Epic Scene", priority = 0)]
    public static void CreateBossEpic()
    {
        try
        {
            // First, ensure default spells exist to prevent runtime errors.
            CreateDefaultSpells();

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var arenaRoot = new GameObject("Arena");
            CreateArenaFloorAndWalls(arenaRoot.transform, 120f, 120f, 0.5f, 6f, 1f);

            var wizardPrefab = FindPrefab(new[] { "t:Prefab PolyArtWizardStandardMat", "t:Prefab Wizard", "t:Prefab Mage" },
                                          new[] { "Assets/WizzardPoliArt", "Assets/WizardPolyArt", "Assets" });

            GameObject player = SetupPlayer(wizardPrefab);
            var wizardController = player.GetComponent<WizardSimpleController>();
            if (wizardController != null)
            {
                CreateSpellBarUI(wizardController);
            }
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
        var wizardController = player.GetComponent<WizardSimpleController>() ?? player.AddComponent<WizardSimpleController>();

        // Tetapkan prefab efek lingkaran sihir
        wizardController.castingEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab");
        GameObject magicCirclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab");
        if (magicCirclePrefab != null)
        {
            wizardController.castingEffectPrefab = magicCirclePrefab;
        }
        else
        {
            Debug.LogWarning("[BossEpic] Prefab lingkaran sihir tidak ditemukan di path yang ditentukan.");
        }

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

    private static void CreateDefaultSpells()
    {
        string spellDir = "Assets/Resources/Spells";
        if (!Directory.Exists(spellDir))
        {
            Directory.CreateDirectory(spellDir);
        }

        // Define spells with dramatic scaling
        if (!File.Exists($"{spellDir}/ProjectileFree1.asset"))
        {
            CreateSpellAsset(
                spellName: "Fireball",
                iconPath: "Assets/Hovl Studio/Magic effects pack/Textures/Flare.png",
                projectilePath: "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals front attack.prefab",
                hitEffectPath: "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals front attack.prefab",
                castSoundPath: "Assets/Resources/Sounds/cast_fire.mp3",
                hitSoundPath: "Assets/Resources/Sounds/hit_fire.mp3",
                projectileScale: 1.5f
                            );
        }

        //  if (!File.Exists($"{spellDir}/ProjectileFree1.asset"))
        // {
        //     CreateSpellAsset(
        //         spellName: "Fireball",
        //         iconPath: "Assets/Hovl Studio/Magic effects pack/Textures/Flare.png",
        //         projectilePath: "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Red energy explosion.prefab",
        //         hitEffectPath: "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Explosion.prefab",
        //         castSoundPath: "Assets/Resources/Sounds/cast_fire.mp3",
        //         hitSoundPath: "Assets/Resources/Sounds/hit_fire.mp3",
        //         projectileScale: 0.5f,
        //         hitEffectScale: 1.0f
        //     );
        // }

        // if (!File.Exists($"{spellDir}/IceShard.asset"))
        // {
        //     CreateSpellAsset(
        //         spellName: "Ice Shard",
        //         iconPath: "Assets/Hovl Studio/Magic effects pack/Textures/Snowflake.png",
        //         projectilePath: "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Snow AOE.prefab",
        //         hitEffectPath: "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Snow hit.prefab",
        //         castSoundPath: "Assets/Resources/Sounds/cast_ice.mp3",
        //         hitSoundPath: "Assets/Resources/Sounds/hit_ice.mp3",
        //         projectileScale: 0.5f,
        //         hitEffectScale: 1.0f
        //     );
        // }

        // if (!File.Exists($"{spellDir}/ElectroShock.asset"))
        // {
        //     CreateSpellAsset(
        //         spellName: "Electro Shock",
        //         iconPath: "Assets/Hovl Studio/Magic effects pack/Textures/Electro.png",
        //         projectilePath: "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Electro hit.prefab",
        //         hitEffectPath: "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Electro hit.prefab",
        //         castSoundPath: "Assets/Resources/Sounds/cast_electro.mp3",
        //         hitSoundPath: "Assets/Resources/Sounds/hit_electro.mp3",
        //         projectileScale: 0.5f,
        //         hitEffectScale: 1.0f
        //     );
        // }

        // if (!File.Exists($"{spellDir}/HolyLight.asset"))
        // {
        //     CreateSpellAsset(
        //         spellName: "Holy Light",
        //         iconPath: "Assets/Hovl Studio/Magic effects pack/Textures/Star.png",
        //         projectilePath: "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Holy hit.prefab",
        //         hitEffectPath: "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Holy hit.prefab",
        //         castSoundPath: "",
        //         hitSoundPath: "",
        //         projectileScale: 0.5f,
        //         hitEffectScale: 1.0f
        //     );
        // }

        // if (!File.Exists($"{spellDir}/StoneBullet.asset"))
        // {
        //     CreateSpellAsset(
        //         spellName: "Stone Bullet",
        //         iconPath: "Assets/Hovl Studio/Magic effects pack/Textures/Stone.png",
        //         projectilePath: "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Stones hit.prefab",
        //         hitEffectPath: "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Stones hit.prefab",
        //         castSoundPath: "",
        //         hitSoundPath: "",
        //         projectileScale: 0.5f,
        //         hitEffectScale: 1.0f
        //     );
        // }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Default spell assets checked/created successfully with new scaling.");
    }

    private static void CreateSpellAsset(string spellName, string iconPath, string projectilePath, string hitEffectPath, string castSoundPath, string hitSoundPath, float projectileScale = 1f, float hitEffectScale = 1f)
    {
        // --- Robust Asset Loading --- 
        bool allAssetsFound = true;

        TextureImporter textureImporter = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (textureImporter != null)
        {
            if (textureImporter.textureType != TextureImporterType.Sprite)
            {
                textureImporter.textureType = TextureImporterType.Sprite;
                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"Changed texture type to Sprite for icon: {iconPath}");
            }
        }

        Texture2D iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        if (iconTexture == null) { Debug.LogError($"[Spell Creation] Failed to load icon for '{spellName}' at: {iconPath}"); allAssetsFound = false; }

        GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath);
        if (projectilePrefab == null) 
        { 
            Debug.LogError($"[Spell Creation] Failed to load projectile prefab for '{spellName}' at: {projectilePath}"); 
            allAssetsFound = false; 
        }
        else
        {
            // Ensure the projectile prefab has the Projectile script
            if (projectilePrefab.GetComponent<Projectile>() == null)
            {
                // This is a complex operation on a prefab asset. We need to open it, modify, and save.
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(projectilePath);
                prefabContents.AddComponent<Projectile>();
                PrefabUtility.SaveAsPrefabAsset(prefabContents, projectilePath);
                PrefabUtility.UnloadPrefabContents(prefabContents);
                Debug.Log($"Added 'Projectile.cs' script to prefab: {projectilePrefab.name}");
            }
        }

        GameObject hitEffect = AssetDatabase.LoadAssetAtPath<GameObject>(hitEffectPath);
        if (hitEffect == null) { Debug.LogError($"[Spell Creation] Failed to load hit effect for '{spellName}' at: {hitEffectPath}"); allAssetsFound = false; }

        AudioClip castSound = AssetDatabase.LoadAssetAtPath<AudioClip>(castSoundPath);
        if (castSound == null) { Debug.LogWarning($"[Spell Creation] Failed to load cast sound for '{spellName}' at: {castSoundPath}"); }

        AudioClip hitSound = AssetDatabase.LoadAssetAtPath<AudioClip>(hitSoundPath);
        if (hitSound == null) { Debug.LogWarning($"[Spell Creation] Failed to load hit sound for '{spellName}' at: {hitSoundPath}"); }

        if (!allAssetsFound)
        {
            Debug.LogError($"--- CANNOT CREATE SPELL '{spellName}' due to missing critical assets. Please check paths. ---");
            return;
        }

        // --- Create and Configure Spell --- 
        Spell newSpell = ScriptableObject.CreateInstance<Spell>();
        newSpell.spellName = spellName;
        newSpell.cooldown = 1.5f;
        newSpell.speed = 25f;
        newSpell.lifeTime = 3f;

        newSpell.icon = Sprite.Create(iconTexture, new Rect(0, 0, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
        newSpell.projectilePrefab = projectilePrefab;
        newSpell.hitEffect = hitEffect;
        newSpell.castSound = castSound;
        newSpell.hitSound = hitSound;
        newSpell.projectileScale = projectileScale;
        newSpell.hitEffectScale = hitEffectScale;

        string assetPath = $"Assets/Resources/Spells/{spellName.Replace(" ", "")}.asset";
        AssetDatabase.CreateAsset(newSpell, assetPath);
        Debug.Log($"Successfully created spell: {spellName}");
    }
}

#endif
