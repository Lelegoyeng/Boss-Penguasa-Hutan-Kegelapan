// Editor-only API untuk menginstansiasi prefab Starter Assets secara otomatis
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Linq;
using System;

public class SceneValidator : MonoBehaviour
{
    [Tooltip("Jika true, validator akan menampilkan log detail saat Start")] public bool verbose = true;
    [Tooltip("Aktifkan perbaikan otomatis untuk masalah umum (tag, komponen, mask, referensi)")] public bool enableAutoFix = true;
    [Tooltip("Jika tersedia, gunakan Cinemachine secara otomatis (FreeLook) dan matikan ThirdPersonCamera")] public bool preferCinemachine = true;

    // Helper methods untuk kompatibilitas API pencarian Unity versi baru/lama
    static T FindFirst<T>(bool includeInactive = false) where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        var mode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        return UnityEngine.Object.FindFirstObjectByType<T>(mode);
#elif UNITY_2022_2_OR_NEWER
        var arr = UnityEngine.Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return arr != null && arr.Length > 0 ? arr[0] : null;
#else
#pragma warning disable CS0618
        return UnityEngine.Object.FindObjectOfType<T>(includeInactive);
#pragma warning restore CS0618
#endif
    }

    static T[] FindAllOfType<T>(bool includeInactive = false) where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#elif UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
#pragma warning disable CS0618
        return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#pragma warning restore CS0618
#endif
    }

    void Start()
    {
        RunValidation();
    }

    public void RunValidation()
    {
        ValidateEnvironmentGround();
        ValidatePlayer();
        ValidateBoss();
        // Pusatkan aktor di tengah terrain sesuai permintaan
        CenterActorsOnTerrain();
        // Pastikan Player & Boss berada di permukaan tanah/terrain
        AlignActorsToGround();
        ValidateArena();
        ValidateUI();
        ValidateAudio();
        ValidateCameraShake();
        ValidateNavMesh();

        // Rapikan kamera agar tidak miring / salah arah
        FixCameraAlignment();

        if (verbose) Debug.Log("[SceneValidator] Selesai memeriksa scene.");
    }

    // Memusatkan Player di tengah terrain dan meletakkan Boss berhadapan pada jarak tertentu mengikuti arah kamera
    void CenterActorsOnTerrain()
    {
        if (!enableAutoFix) return;
        var center = GetTerrainCenterOnSurface();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            player.transform.position = center;
        }

        var boss = FindFirst<BossController>(true);
        if (boss)
        {
            Vector3 camDir = GetCameraFlatForward();
            if (camDir.sqrMagnitude < 0.0001f) camDir = (player ? (player.transform.forward) : Vector3.forward);
            camDir.y = 0f; camDir.Normalize();
            float distance = 8f;
            Vector3 targetPos = center + camDir * distance;
            boss.transform.position = targetPos;
            // Hadapkan boss ke player (horizontal)
            Vector3 look = (center - boss.transform.position); look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                boss.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
    }

    Vector3 GetCameraFlatForward()
    {
        var cam = Camera.main;
        if (!cam) return Vector3.forward;
        var f = cam.transform.forward; f.y = 0f; return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    // Menempatkan Player dan Boss ke permukaan tanah/terrain terdekat
    void AlignActorsToGround()
    {
        try
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) AlignTransformToGround(player.transform, 0.05f);

            var boss = FindFirst<BossController>(true);
            if (boss) AlignTransformToGround(boss.transform, 0.05f);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal menyelaraskan posisi dengan tanah: " + e.Message);
        }
    }

    void AlignTransformToGround(Transform t, float extraHeight)
    {
        if (!t) return;
        Vector3 pos = t.position;
        float groundY = pos.y;

        // Coba raycast dari atas ke bawah untuk dapatkan collider tanah/terrain paling akurat
        Vector3 origin = new Vector3(pos.x, pos.y + 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out var hitInfo, 500f))
        {
            groundY = hitInfo.point.y;
        }
        else
        {
            // Fallback: gunakan Terrain jika ada
            var terrain = FindFirst<Terrain>(true);
            if (terrain && terrain.terrainData != null)
            {
                groundY = terrain.SampleHeight(new Vector3(pos.x, 0f, pos.z)) + terrain.transform.position.y;
            }
            else
            {
                groundY = 0f; // fallback terakhir
            }
        }

        float targetY = groundY + extraHeight;
        if (Mathf.Abs(t.position.y - targetY) > 0.01f)
        {
            t.position = new Vector3(t.position.x, targetY, t.position.z);
            if (verbose) Debug.Log($"[SceneValidator][AutoFix] Memindahkan '{t.name}' ke permukaan tanah (y={targetY:0.00}).");
        }
    }

    // Mengambil titik pusat Terrain (di atas permukaan), fallback (0,0,0) jika tidak ada Terrain
    Vector3 GetTerrainCenterOnSurface()
    {
        var terrain = FindFirst<Terrain>(true);
        if (terrain && terrain.terrainData != null)
        {
            var size = terrain.terrainData.size;
            var basePos = terrain.transform.position;
            var centerXZ = new Vector3(basePos.x + size.x * 0.5f, 0f, basePos.z + size.z * 0.5f);
            float y = terrain.SampleHeight(centerXZ) + basePos.y;
            return new Vector3(centerXZ.x, y, centerXZ.z);
        }
        return Vector3.zero;
    }

    void ValidateEnvironmentGround()
    {
        // Jika sudah ada Terrain aktif, anggap ground sudah tersedia
        var terrain = FindFirst<Terrain>(true);
        if (terrain && terrain.gameObject.activeInHierarchy)
        {
            if (verbose) Debug.Log("[SceneValidator] Terrain terdeteksi. Melewati pembuatan ground plane.");
            // Tambahan: pastikan properti hutan dan dekorasi ada (Editor)
#if UNITY_EDITOR
            if (enableAutoFix) EnsureForestPropsEditor(terrain);
#endif
            return;
        }

        // Editor: coba buat Terrain hutan otomatis jika belum ada
#if UNITY_EDITOR
        if (enableAutoFix)
        {
            var terrGO = EnsureForestTerrainEditor();
            if (terrGO)
            {
                // Hapus plane lama jika ada
                var oldPlane = GameObject.Find("Arena_Ground");
                if (oldPlane) DestroyImmediate(oldPlane);
                terrain = terrGO.GetComponent<Terrain>();
                // Tambahkan properti hutan: bebatuan/kayu + pohon sederhana
                EnsureForestPropsEditor(terrain);
            }
        }
#endif

        // Setup ambience sinematik hutan (kabut, matahari, angin, audio ambience, post-processing)
        EnsureForestCinematicSetup(terrain);

        // Cek apakah sudah ada ground buatan
        var existing = GameObject.Find("Arena_Ground");
        if (existing)
        {
            if (verbose) Debug.Log("[SceneValidator] Arena_Ground sudah ada.");
        }
        else if (enableAutoFix)
        {
            // Buat plane besar sebagai arena dasar
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Arena_Ground";
            plane.transform.position = Vector3.zero;
            // Scale: Unity Plane default 10x10 unit. Skala 20 = 200x200 unit.
            plane.transform.localScale = new Vector3(20f, 1f, 20f);
            // Pastikan ada collider (primitive plane punya MeshCollider)
            var col = plane.GetComponent<Collider>();
            if (!col) plane.AddComponent<MeshCollider>();
            if (verbose) Debug.Log("[SceneValidator][AutoFix] Membuat ground plane 'Arena_Ground' ukuran ~200x200.");

#if UNITY_EDITOR
            // Tandai sebagai Navigation Static agar mudah bake NavMesh
            try
            {
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(plane, UnityEditor.StaticEditorFlags.NavigationStatic);
            }
            catch {}
#endif
        }

        // Pastikan Player/Boss tidak jatuh di bawah ground
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player && player.transform.position.y < 0.5f)
        {
            var p = player.transform.position; p.y = 1.0f; player.transform.position = p;
            if (verbose) Debug.Log("[SceneValidator][AutoFix] Memindahkan Player ke atas ground.");
        }
        var bossCtrl = FindFirst<BossController>(true);
        if (bossCtrl && bossCtrl.transform.position.y < 0.5f)
        {
            var b = bossCtrl.transform.position; b.y = 1.0f; bossCtrl.transform.position = b;
            if (verbose) Debug.Log("[SceneValidator][AutoFix] Memindahkan Boss ke atas ground.");
        }
    }

    void EnsureForestCinematicSetup(Terrain terrain)
    {
        // Fog atmosfer
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.35f, 0.45f, 0.38f);
        RenderSettings.fogDensity = 0.015f;

        // Skybox procedural jika belum ada
#if UNITY_EDITOR
        if (!RenderSettings.skybox)
        {
            var skyMat = new Material(Shader.Find("Skybox/Procedural"));
            if (skyMat)
            {
                skyMat.name = "Auto_ProceduralSkybox";
                RenderSettings.skybox = skyMat;
            }
        }
#endif

        // Arahkan matahari (Directional Light) atau buat jika tidak ada
        var sun = FindFirst<Light>(true);
        Light dirLight = null;
        if (sun && sun.type == LightType.Directional) dirLight = sun;
        else
        {
            var allLights = FindAllOfType<Light>(true);
            dirLight = allLights.FirstOrDefault(l => l.type == LightType.Directional);
        }
        if (!dirLight)
        {
            var go = new GameObject("Sun_Light");
            dirLight = go.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.95f, 0.85f);
            dirLight.intensity = 1.1f;
        }
        dirLight.transform.rotation = Quaternion.Euler(50f, 30f, 0f);

        // Wind Zone
        if (!FindFirst<WindZone>(true))
        {
            var wz = new GameObject("WindZone_Forest").AddComponent<WindZone>();
            wz.mode = WindZoneMode.Directional;
            wz.windMain = 0.6f;
            wz.windTurbulence = 0.7f;
            wz.windPulseMagnitude = 0.4f;
            wz.windPulseFrequency = 0.25f;
        }

        // Ambience Audio
        var ambience = GameObject.Find("Ambience_Forest");
        if (!ambience)
        {
            ambience = new GameObject("Ambience_Forest");
            var a = ambience.AddComponent<AudioSource>();
            a.loop = true; a.playOnAwake = true; a.spatialBlend = 0f; a.volume = 0.4f;
            // Catatan: silakan assign audio clip ambience hutan di Inspector.
        }

#if UNITY_EDITOR
        // Post-processing URP (jika tersedia) via reflection agar tidak error bila URP tidak diinstal
        try
        {
            var volType = System.Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            var profType = System.Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime");
            if (volType != null && profType != null)
            {
                var go = GameObject.Find("GlobalVolume_Forest") ?? new GameObject("GlobalVolume_Forest");
                if (go.GetComponent(volType) == null)
                {
                    var vol = go.AddComponent(volType) as Behaviour;
                    go.layer = 0;
                    var profileField = volType.GetProperty("profile");
                    // Buat asset profile
                    var profile = ScriptableObject.CreateInstance(profType.FullName) as ScriptableObject;
                    // Simpan ke Assets agar persisten
                    string basePath = "Assets/_AutoGenerated/PostProcessing";
                    if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_AutoGenerated")) UnityEditor.AssetDatabase.CreateFolder("Assets", "_AutoGenerated");
                    if (!UnityEditor.AssetDatabase.IsValidFolder(basePath)) UnityEditor.AssetDatabase.CreateFolder("Assets/_AutoGenerated", "PostProcessing");
                    UnityEditor.AssetDatabase.CreateAsset(profile, basePath + "/ForestVolumeProfile.asset");
                    profileField?.SetValue(vol, profile, null);
                }
            }
        }
        catch {}
#endif

#if UNITY_EDITOR
        // Hiasi props sederhana di sekitar arena (batu / log) dan bersihkan pusat arena
        if (terrain)
        {
            ClearArenaCenterOnTerrain(terrain, 0.18f);
            ScatterRocksAndLogsEditor(terrain);
        }
#endif
    }

#if UNITY_EDITOR
    void ClearArenaCenterOnTerrain(Terrain terrain, float radiusNorm)
    {
        if (!terrain) return;
        var tData = terrain.terrainData;
        if (tData == null) return;

        // Hapus pohon di area tengah
        var trees = tData.treeInstances.ToList();
        trees.RemoveAll(ti =>
        {
            var pos = new Vector2(ti.position.x - 0.5f, ti.position.z - 0.5f);
            return pos.magnitude < radiusNorm;
        });
        tData.treeInstances = trees.ToArray();
        terrain.Flush();

        // Hapus rumput pada radius tengah
        int layers = tData.detailPrototypes != null ? tData.detailPrototypes.Length : 0;
        if (layers > 0)
        {
            int w = tData.detailWidth;
            int h = tData.detailHeight;
            int cx = w / 2;
            int cy = h / 2;
            int r = Mathf.RoundToInt(Mathf.Min(w, h) * radiusNorm);
            for (int layer = 0; layer < layers; layer++)
            {
                var map = tData.GetDetailLayer(0, 0, w, h, layer);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int dx = x - cx; int dy = y - cy;
                        if (dx * dx + dy * dy <= r * r) map[y, x] = 0;
                    }
                }
                tData.SetDetailLayer(0, 0, layer, map);
            }
        }
    }

    void TryReplaceBossWithModelEditor(BossController boss)
    {
        if (!boss) return;
        // Jika sudah model (ada SkinnedMeshRenderer) abaikan
        if (boss.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0) return;

        try
        {
            // Cari prefab kandidat
            string[] nameHints = new[] { "Lele", "Troll", "Ogre", "Boss", "Monster", "Enemy" };
            var guids = new System.Collections.Generic.List<string>();
            foreach (var hint in nameHints)
            {
                var hits = AssetDatabase.FindAssets($"t:Prefab {hint}");
                if (hits != null && hits.Length > 0) guids.AddRange(hits);
            }
            GameObject chosenPrefab = null;
            foreach (var guid in guids.Distinct())
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;
                // Prefab dinilai cocok bila punya SkinnedMeshRenderer atau Animator
                bool hasSkin = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;
                bool hasAnimator = prefab.GetComponentInChildren<Animator>(true) != null;
                if (hasSkin || hasAnimator)
                {
                    chosenPrefab = prefab;
                    break;
                }
            }
            if (!chosenPrefab) return;

            var oldGO = boss.gameObject;
            var pos = oldGO.transform.position;
            var rot = oldGO.transform.rotation;

            var newGO = (GameObject)PrefabUtility.InstantiatePrefab(chosenPrefab);
            if (!newGO) return;
            newGO.name = chosenPrefab.name;
            newGO.transform.position = pos;
            newGO.transform.rotation = rot;

            // Pastikan komponen penting
            var newBoss = newGO.GetComponent<BossController>();
            if (!newBoss) newBoss = newGO.AddComponent<BossController>();
            if (!newGO.GetComponent<BossHealth>()) newGO.AddComponent<BossHealth>();
            if (!newGO.GetComponent<NavMeshAgent>()) newGO.AddComponent<NavMeshAgent>();
            if (!newGO.GetComponent<CharacterController>()) newGO.AddComponent<CharacterController>();

            // Pastikan anak Attacks ada
            var attacksRoot = newGO.transform.Find("Attacks");
            if (!attacksRoot)
            {
                var ar = new GameObject("Attacks");
                ar.transform.SetParent(newGO.transform, false);
                attacksRoot = ar.transform;
            }
            // Tambah serangan jika belum ada
            if (newGO.GetComponentsInChildren<BossAttackBase>(true).Length == 0)
            {
                int playerLayer = 0; var player = GameObject.FindGameObjectWithTag("Player");
                if (player) playerLayer = player.layer;
                LayerMask hitMask = 0; if (playerLayer >= 0 && playerLayer <= 31) hitMask = (1 << playerLayer);
                try { var c = attacksRoot.gameObject.AddComponent<CleaveAttack>(); c.hitMask = hitMask; } catch {}
                try { var s = attacksRoot.gameObject.AddComponent<SlamAttack>(); s.hitMask = hitMask; } catch {}
                try { var d = attacksRoot.gameObject.AddComponent<DashAttack>(); d.hitMask = hitMask; } catch {}
            }

            try { newGO.tag = "Boss"; } catch {}

            // Hapus placeholder lama
            Undo.RegisterFullObjectHierarchyUndo(oldGO, "Replace Boss");
            GameObject.DestroyImmediate(oldGO);

            if (verbose) Debug.Log($"[SceneValidator][AutoFix] Mengganti Boss placeholder dengan prefab model: {chosenPrefab.name}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal mengganti Boss dengan model: " + e.Message);
        }
    }

    void ScatterRocksAndLogsEditor(Terrain terrain)
    {
        if (!terrain) return;
        var tData = terrain.terrainData; if (!tData) return;

        string basePath = "Assets/_AutoGenerated/Terrain";
        if (!AssetDatabase.IsValidFolder("Assets/_AutoGenerated")) AssetDatabase.CreateFolder("Assets", "_AutoGenerated");
        if (!AssetDatabase.IsValidFolder(basePath)) AssetDatabase.CreateFolder("Assets/_AutoGenerated", "Terrain");
        string prefabFolder = basePath + "/Props";
        string matFolder = basePath + "/Materials";
        if (!AssetDatabase.IsValidFolder(prefabFolder)) AssetDatabase.CreateFolder(basePath, "Props");
        if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(basePath, "Materials");

        // Materials
        var rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); rockMat.color = new Color(0.45f, 0.45f, 0.5f);
        AssetDatabase.CreateAsset(rockMat, matFolder + "/Rock.mat");
        var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); woodMat.color = new Color(0.32f, 0.22f, 0.12f);
        AssetDatabase.CreateAsset(woodMat, matFolder + "/Wood.mat");

        // Rock prefab (distorted sphere)
        string rockPath = prefabFolder + "/AutoRock.prefab";
        var rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rockPath);
        if (!rockPrefab)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "AutoRock";
            UnityEngine.Object.DestroyImmediate(rock.GetComponent<Collider>());
            var mr = rock.GetComponent<MeshRenderer>(); if (mr) mr.sharedMaterial = rockMat;
            rock.transform.localScale = new Vector3(1.6f, 1.1f, 1.2f);
            rockPrefab = PrefabUtility.SaveAsPrefabAsset(rock, rockPath);
            UnityEngine.Object.DestroyImmediate(rock);
        }

        // Log prefab (lying cylinder)
        string logPath = prefabFolder + "/AutoLog.prefab";
        var logPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(logPath);
        if (!logPrefab)
        {
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "AutoLog";
            UnityEngine.Object.DestroyImmediate(log.GetComponent<Collider>());
            var mr = log.GetComponent<MeshRenderer>(); if (mr) mr.sharedMaterial = woodMat;
            log.transform.localScale = new Vector3(0.35f, 2.2f, 0.35f);
            logPrefab = PrefabUtility.SaveAsPrefabAsset(log, logPath);
            UnityEngine.Object.DestroyImmediate(log);
        }

        // Parent container
        var container = GameObject.Find("Forest_Props") ?? new GameObject("Forest_Props");

        // Scatter around ring area
        var size = tData.size;
        var rnd = new System.Random(123);
        int count = 24;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count + (float)rnd.NextDouble() * 0.02f;
            float angle = t * Mathf.PI * 2f;
            float radius = Mathf.Lerp(0.22f, 0.35f, (float)rnd.NextDouble());
            float nx = 0.5f + Mathf.Cos(angle) * radius;
            float nz = 0.5f + Mathf.Sin(angle) * radius;
            float wx = nx * size.x;
            float wz = nz * size.z;
            float wy = terrain.SampleHeight(new Vector3(wx, 0, wz));

            bool placeRock = rnd.NextDouble() > 0.5;
            var prefab = placeRock ? rockPrefab : logPrefab;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(container.transform, true);
            go.transform.position = new Vector3(wx, wy + (placeRock ? 0f : 0.15f), wz);
            if (!placeRock)
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 90f);
            float s = placeRock ? (0.8f + (float)rnd.NextDouble() * 1.6f) : (0.7f + (float)rnd.NextDouble() * 0.8f);
            go.transform.localScale = Vector3.one * s;
        }
    }

#if UNITY_EDITOR
    GameObject EnsureForestTerrainEditor()
    {
        try
        {
            var existingTerrain = FindFirst<Terrain>(true);
            if (existingTerrain) return existingTerrain.gameObject;

            // Siapkan folder asset
            string basePath = "Assets/_AutoGenerated/Terrain";
            if (!AssetDatabase.IsValidFolder("Assets/_AutoGenerated"))
                AssetDatabase.CreateFolder("Assets", "_AutoGenerated");
            if (!AssetDatabase.IsValidFolder(basePath))
                AssetDatabase.CreateFolder("Assets/_AutoGenerated", "Terrain");

            // Buat TerrainData
            var tData = new TerrainData();
            tData.heightmapResolution = 513;
            tData.alphamapResolution = 512;
            tData.size = new Vector3(400f, 60f, 400f);

            // Generate kontur Perlin sederhana
            int hm = tData.heightmapResolution;
            float[,] heights = new float[hm, hm];
            float scale = 0.01f; // frekuensi noise
            for (int y = 0; y < hm; y++)
            {
                for (int x = 0; x < hm; x++)
                {
                    float nx = x * scale;
                    float ny = y * scale;
                    float h = Mathf.PerlinNoise(nx, ny) * 0.25f; // 0 - 0.25
                    h += Mathf.PerlinNoise(nx * 0.25f, ny * 0.25f) * 0.1f; // low freq detail
                    heights[y, x] = Mathf.Clamp01(h);
                }
            }
            tData.SetHeights(0, 0, heights);

            // Buat tekstur hijau sederhana sebagai TerrainLayer
            string grassTexPath = basePath + "/Grass.png";
            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(grassTexPath);
            if (!grassTex)
            {
                grassTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Color32 c = new Color32(60, 120, 60, 255);
                grassTex.SetPixels32(new Color32[] { c, c, c, c });
                grassTex.Apply();
                var png = grassTex.EncodeToPNG();
                System.IO.File.WriteAllBytes(grassTexPath, png);
                AssetDatabase.ImportAsset(grassTexPath);
                grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(grassTexPath);
            }

            var layer = new TerrainLayer();
            layer.diffuseTexture = grassTex;
            layer.tileSize = new Vector2(8, 8);
            AssetDatabase.CreateAsset(layer, basePath + "/Grass.terrainlayer");
            tData.terrainLayers = new TerrainLayer[] { layer };

            // Buat GameObject Terrain
            var terrGO = Terrain.CreateTerrainGameObject(tData);
            terrGO.name = "ForestTerrain";
            terrGO.transform.position = Vector3.zero;

            // Tandai navigation static
            try
            {
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(terrGO, UnityEditor.StaticEditorFlags.NavigationStatic);
            }
            catch {}

            if (verbose) Debug.Log("[SceneValidator][AutoFix] Membuat Terrain hutan sederhana (400x60x400).");
            return terrGO;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal membuat Terrain: " + e.Message);
            return null;
        }
    }
#endif
#else
    // Stub non-Editor: no-op agar pemanggilan tetap valid di build runtime
    void TryReplaceBossWithModelEditor(BossController boss) { }
#endif

    void ValidatePlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
        {
            // coba temukan kandidat player
            var ph = FindFirst<PlayerHealth>(true);
            if (ph) player = ph.gameObject;
            else
            {
                var cc = FindAllOfType<CharacterController>(true).FirstOrDefault();
                if (cc) player = cc.gameObject;
            }
            if (!player)
            {
                // coba cari berdasarkan nama GameObject "Player"
                var allRoots = gameObject.scene.GetRootGameObjects();
                foreach (var root in allRoots)
                {
                    var candidates = root.GetComponentsInChildren<Transform>(true);
                    var found = candidates.FirstOrDefault(t => string.Equals(t.name, "Player", StringComparison.OrdinalIgnoreCase));
                    if (found)
                    {
                        player = found.gameObject;
                        break;
                    }
                }
            }
            if (player && enableAutoFix)
            {
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Menemukan kandidat Player: " + player.name + ". Set tag 'Player'.");
                player.tag = "Player";
            }
        }
        if (!player)
        {
            Debug.LogError("[SceneValidator] Player dengan tag 'Player' tidak ditemukan.");
            if (enableAutoFix)
            {
#if UNITY_EDITOR
                var spawned = TrySpawnStarterAssetsPlayerEditor();
                if (spawned)
                {
                    player = spawned;
                    if (verbose) Debug.Log("[SceneValidator][AutoFix] Meng-instantiate Player Starter Assets secara otomatis.");
                    PositionMainCameraNear(player.transform);
                    return;
                }
#endif
            }
            if (enableAutoFix)
            {
                player = CreateBasicPlayer();
                if (player)
                {
                    if (verbose) Debug.Log("[SceneValidator][AutoFix] Membuat Player sederhana (Capsule + CharacterController + PlayerHealth).");
                    PositionMainCameraNear(player.transform);
                    return;
                }
            }
            return;
        }
        if (!player.GetComponent<CharacterController>())
        {
            Debug.LogWarning("[SceneValidator] Player tidak memiliki CharacterController.");
            if (enableAutoFix)
            {
                player.AddComponent<CharacterController>();
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Menambahkan CharacterController ke Player.");
            }
        }
        if (!player.GetComponent<PlayerHealth>())
        {
            Debug.LogWarning("[SceneValidator] Player tidak memiliki PlayerHealth (IDamageable). Boss tidak bisa memberi damage.");
            if (enableAutoFix)
            {
                player.AddComponent<PlayerHealth>();
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Menambahkan PlayerHealth ke Player.");
            }
        }
    }

    void ValidateBoss()
    {
        var boss = FindFirst<BossController>(true);
        if (!boss)
        {
            Debug.LogError("[SceneValidator] BossController tidak ditemukan di scene.");
            if (enableAutoFix)
            {
                var created = CreateBasicBoss();
                if (created)
                {
                    boss = created.GetComponent<BossController>();
                    if (verbose) Debug.Log("[SceneValidator][AutoFix] Membuat Boss sederhana (Capsule + NavMeshAgent + CharacterController + BossHealth + Attacks).");
                }
            }
            if (!boss) return;
        }
        
#if UNITY_EDITOR
        // Jika Boss masih placeholder (tidak ada SkinnedMeshRenderer), coba ganti dengan prefab model 3D yang cocok
        if (enableAutoFix)
        {
            TryReplaceBossWithModelEditor(boss);
        }
#endif
        var health = boss.GetComponent<BossHealth>();
        if (!health) Debug.LogError("[SceneValidator] BossHealth tidak ditemukan pada Boss.");
        if (!boss.GetComponent<NavMeshAgent>()) Debug.LogWarning("[SceneValidator] Boss tidak memiliki NavMeshAgent.");
        if (!boss.GetComponent<CharacterController>())
        {
            Debug.LogWarning("[SceneValidator] Boss tidak memiliki CharacterController (dibutuhkan untuk DashAttack).");
            if (enableAutoFix)
            {
                boss.gameObject.AddComponent<CharacterController>();
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Menambahkan CharacterController ke Boss.");
            }
        }

        var attacks = boss.GetComponentsInChildren<BossAttackBase>(true);
        if (attacks == null || attacks.Length == 0) Debug.LogWarning("[SceneValidator] Boss tidak memiliki komponen serangan (BossAttackBase).");

        foreach (var atk in attacks)
        {
            // Hit mask check jika ada properti
            if (atk is CleaveAttack cleave)
            {
                if (cleave.hitMask.value == 0)
                {
                    Debug.LogWarning("[SceneValidator] CleaveAttack hitMask belum diatur.");
                    AutoFixHitMaskToPlayer(cleave.gameObject, ref cleave.hitMask);
                }
            }
            if (atk is SlamAttack slam)
            {
                if (slam.hitMask.value == 0)
                {
                    Debug.LogWarning("[SceneValidator] SlamAttack hitMask belum diatur.");
                    AutoFixHitMaskToPlayer(slam.gameObject, ref slam.hitMask);
                }
            }
            if (atk is DashAttack dash)
            {
                if (dash.hitMask.value == 0)
                {
                    Debug.LogWarning("[SceneValidator] DashAttack hitMask belum diatur.");
                    AutoFixHitMaskToPlayer(dash.gameObject, ref dash.hitMask);
                }
            }
        }
    }

    void ValidateArena()
    {
        var arena = FindFirst<ArenaTrigger>(true);
        if (!arena)
        {
            Debug.LogWarning("[SceneValidator] ArenaTrigger tidak ditemukan (boss akan tetap bisa aktif manual).");
            return;
        }
        var col = arena.GetComponent<Collider>();
        if (!col || !col.isTrigger)
        {
            Debug.LogWarning("[SceneValidator] ArenaTrigger membutuhkan Collider dengan isTrigger = true.");
            if (enableAutoFix)
            {
                if (!col) col = arena.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Menambahkan Collider isTrigger pada ArenaTrigger.");
            }
        }
        if (arena.barriersToEnable == null || arena.barriersToEnable.Length == 0)
        {
            Debug.Log("[SceneValidator] ArenaTrigger: barriersToEnable kosong (opsional).");
        }
        if (!arena.boss)
        {
            Debug.LogWarning("[SceneValidator] ArenaTrigger: field 'boss' belum diassign.");
            if (enableAutoFix)
            {
                var boss = FindFirst<BossController>(true);
                if (boss)
                {
                    arena.boss = boss;
                    if (verbose) Debug.Log("[SceneValidator][AutoFix] Menghubungkan ArenaTrigger.boss ke BossController.");
                }
            }
        }
    }

    void ValidateUI()
    {
        var bossUI = FindFirst<BossHealthBar>(true);
        if (!bossUI)
        {
            Debug.LogWarning("[SceneValidator] BossHealthBar (UI) tidak ditemukan. Tambahkan UI Slider + TMP Text dan komponen BossHealthBar.");
            return;
        }
        if (!bossUI.slider)
        {
            Debug.LogWarning("[SceneValidator] BossHealthBar: 'slider' belum diassign.");
        }
        if (!bossUI.nameText)
        {
            Debug.LogWarning("[SceneValidator] BossHealthBar: 'nameText' belum diassign.");
        }
        if (!bossUI.bossHealth && enableAutoFix)
        {
            var boss = FindFirst<BossController>(true);
            if (boss)
            {
                bossUI.bossHealth = boss.GetComponent<BossHealth>();
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Menghubungkan BossHealthBar ke BossHealth.");
            }
        }
    }

    void ValidateAudio()
    {
        var music = FindFirst<MusicSwitcher>(true);
        if (!music)
        {
            Debug.Log("[SceneValidator] MusicSwitcher tidak ditemukan (opsional).");
            if (enableAutoFix)
            {
                var go = new GameObject("Music");
                var src = go.AddComponent<AudioSource>();
                src.loop = true;
                go.AddComponent<MusicSwitcher>();
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Membuat GameObject 'Music' dengan AudioSource + MusicSwitcher.");
            }
        }
    }

    void ValidateCameraShake()
    {
        var shake = FindFirst<CameraShakeService>(true);
        if (!shake)
        {
            Debug.LogWarning("[SceneValidator] CameraShakeService tidak ditemukan. Tambahkan GameObject dengan komponen CameraShakeService agar getaran kamera bekerja.");
            if (enableAutoFix)
            {
                var go = new GameObject("CameraShakeService");
                go.AddComponent<CameraShakeService>();
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Membuat GameObject 'CameraShakeService'.");
            }
        }
        var mainCam = Camera.main;
        if (!mainCam)
        {
            Debug.LogWarning("[SceneValidator] MainCamera tidak ditemukan atau tidak bertag 'MainCamera'.");
        }
        else
        {
            // Pastikan kamera TPS terpasang
            var tps = mainCam.GetComponent<ThirdPersonCamera>();
            if (!tps)
            {
                Debug.Log("[SceneValidator] ThirdPersonCamera belum terpasang pada MainCamera.");
                if (enableAutoFix)
                {
                    tps = mainCam.gameObject.AddComponent<ThirdPersonCamera>();
                    if (verbose) Debug.Log("[SceneValidator][AutoFix] Menambahkan ThirdPersonCamera ke MainCamera. Target akan di-assign otomatis.");
                }
            }

            // Setup Cinemachine jika tersedia dan diinginkan
            if (preferCinemachine)
            {
                bool cmSetup = TrySetupCinemachine(mainCam);
                if (cmSetup && tps)
                {
                    tps.enabled = false;
                    if (verbose) Debug.Log("[SceneValidator] Cinemachine aktif. ThirdPersonCamera dinonaktifkan untuk menghindari konflik.");
                }
            }
        }
    }

    void ValidateNavMesh()
    {
        var boss = FindFirst<BossController>(true);
        if (!boss) return;
        var agent = boss.GetComponent<NavMeshAgent>();
        if (!agent) return;
        // Cek apakah posisi boss berada di navmesh sample terdekat
        if (!NavMesh.SamplePosition(boss.transform.position, out var hit, 1.0f, NavMesh.AllAreas))
        {
            Debug.LogWarning("[SceneValidator] Boss tidak berada di atas NavMesh. Pastikan NavMesh sudah di-Bake dan posisi Boss berada di area hijau.");
            // Editor: coba auto-bake NavMesh jika diizinkan
            if (enableAutoFix)
            {
#if UNITY_EDITOR
                try
                {
                    UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
                    UnityEditor.AI.NavMeshBuilder.BuildNavMeshAsync();
                    if (verbose) Debug.Log("[SceneValidator][AutoFix] Melakukan bake NavMesh otomatis (Editor).");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[SceneValidator] Gagal auto-bake NavMesh: " + e.Message);
                }
#endif
            }
            // Coba cari titik NavMesh terdekat dengan radius lebih besar, lalu pindahkan Boss ke sana
            float[] radii = new float[] { 10f, 25f, 50f, 100f };
            bool moved = false;
            foreach (var r in radii)
            {
                if (NavMesh.SamplePosition(boss.transform.position, out hit, r, NavMesh.AllAreas))
                {
                    if (agent.Warp(hit.position))
                    {
                        if (verbose) Debug.Log($"[SceneValidator][AutoFix] Memindahkan Boss ke NavMesh terdekat dalam radius {r}.");
                        moved = true;
                        break;
                    }
                }
            }
            if (!moved)
            {
                // Coba di sekitar Player
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player)
                {
                    foreach (var r in radii)
                    {
                        if (NavMesh.SamplePosition(player.transform.position, out hit, r, NavMesh.AllAreas))
                        {
                            if (agent.Warp(hit.position))
                            {
                                if (verbose) Debug.Log($"[SceneValidator][AutoFix] Memindahkan Boss ke NavMesh di sekitar Player (radius {r}).");
                                moved = true;
                                break;
                            }
                        }
                    }
                }
            }
            if (!moved && verbose)
            {
                Debug.Log("[SceneValidator][AutoFix] Tidak ditemukan NavMesh terdekat untuk memindahkan Boss.");
            }
        }
    }

    void AutoFixHitMaskToPlayer(GameObject context, ref LayerMask mask)
    {
        if (!enableAutoFix) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;
        int playerLayer = player.layer;
        if (playerLayer < 0 || playerLayer > 31) return;
        int bit = 1 << playerLayer;
        if (mask.value == 0) mask = bit;
        else mask |= bit;
        if (verbose) Debug.Log("[SceneValidator][AutoFix] Set hitMask pada '" + context.name + "' untuk menyertakan layer Player (" + LayerMask.LayerToName(playerLayer) + ").");
    }

    // ========== CINEMACHINE AUTO SETUP (Reflection) ==========
    bool TrySetupCinemachine(Camera mainCam)
    {
        try
        {
            var brainType = Type.GetType("Cinemachine.CinemachineBrain, Cinemachine");
            var freeLookType = Type.GetType("Cinemachine.CinemachineFreeLook, Cinemachine");
            if (brainType == null || freeLookType == null)
            {
                if (verbose) Debug.Log("[SceneValidator] Cinemachine tidak terdeteksi. Instal via Package Manager jika ingin menggunakan.");
                return false;
            }

            // Tambahkan Brain ke MainCamera jika belum ada
            if (mainCam.GetComponent(brainType) == null)
            {
                mainCam.gameObject.AddComponent(brainType);
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Menambahkan CinemachineBrain ke MainCamera.");
            }

            // Cari target Cinemachine bawaan Starter Assets, jika tidak ada fallback ke CameraPivot
            Transform target = null;
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO)
            {
                target = FindStarterAssetsCinemachineTarget(playerGO.transform);
                if (target && verbose) Debug.Log("[SceneValidator] Menemukan CinemachineTarget bawaan Starter Assets: " + target.name);
            }
            if (!target)
            {
                target = EnsureCameraPivot();
                if (verbose && target) Debug.Log("[SceneValidator] Menggunakan CameraPivot sebagai target Cinemachine.");
            }
            if (target == null) return false;

            // Cari atau buat FreeLook
            GameObject freeLookGO = GameObject.Find("CM FreeLook");
            Component freeLookComp = freeLookGO ? freeLookGO.GetComponent(freeLookType) : null;
            if (freeLookComp == null)
            {
                freeLookGO = new GameObject("CM FreeLook");
                freeLookComp = freeLookGO.AddComponent(freeLookType);
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Membuat 'CM FreeLook'.");
            }

            // Set Follow & LookAt
            var followProp = freeLookType.GetProperty("Follow");
            var lookAtProp = freeLookType.GetProperty("LookAt");
            followProp?.SetValue(freeLookComp, target, null);
            lookAtProp?.SetValue(freeLookComp, target, null);

            // Tambahkan CinemachineCollider jika ada
            var colliderType = Type.GetType("Cinemachine.CinemachineCollider, Cinemachine");
            if (colliderType != null && freeLookGO.GetComponent(colliderType) == null)
            {
                freeLookGO.AddComponent(colliderType);
                if (verbose) Debug.Log("[SceneValidator][AutoFix] Menambahkan CinemachineCollider ke 'CM FreeLook'.");
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal setup Cinemachine: " + e.Message);
            return false;
        }
    }

    Transform EnsureCameraPivot()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
        {
            var ph = FindFirst<PlayerHealth>(true);
            if (ph) player = ph.gameObject;
        }
        if (!player) return null;

        Transform pivot = player.transform.Find("CameraPivot");
        if (!pivot)
        {
            GameObject pivotGO = new GameObject("CameraPivot");
            pivot = pivotGO.transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.7f, 0f);
            pivot.localRotation = Quaternion.identity;
            if (verbose) Debug.Log("[SceneValidator][AutoFix] Membuat CameraPivot di bawah Player.");
        }
        return pivot;
    }

    Transform FindStarterAssetsCinemachineTarget(Transform playerRoot)
    {
        if (!playerRoot) return null;
        // Pola umum Starter Assets:
        // PlayerArmature/PlayerCameraRoot/CinemachineTarget atau PlayerCameraTarget
        var t = playerRoot.Find("PlayerCameraRoot/CinemachineTarget");
        if (!t) t = playerRoot.Find("PlayerCameraRoot/PlayerCameraTarget");
        if (!t) t = playerRoot.Find("CinemachineTarget");
        if (!t) t = playerRoot.Find("PlayerCameraTarget");
        if (t) return t;

        // Cari by name secara luas
        var all = playerRoot.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all)
        {
            var n = tr.name.ToLowerInvariant();
            if (n.Contains("cinemachinetarget") || n.Contains("camera target") || n.Contains("playercameratarget"))
                return tr;
        }
        return null;
    }

    // Merapikan kamera utama agar tidak miring (roll) dan menghadap ke Player
    void FixCameraAlignment()
    {
        var cam = Camera.main;
        if (!cam) return;

        // Jika Cinemachine aktif, biarkan Cinemachine mengatur kamera
        var cmBrain = cam.GetComponent("CinemachineBrain");
        if (preferCinemachine && cmBrain != null) return;

        // Coba target ke Player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            // Posisikan kamera sedikit di belakang dan atas player
            var desiredPos = player.transform.position + new Vector3(0f, 2f, -5f);
            cam.transform.position = desiredPos;
            var lookDir = player.transform.position - cam.transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                cam.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                // Hilangkan roll
                var e = cam.transform.eulerAngles;
                cam.transform.eulerAngles = new Vector3(e.x, e.y, 0f);
            }
            cam.nearClipPlane = 0.1f;
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, 50f, 70f);
        }
    }

#if UNITY_EDITOR
    // Tambahan properti hutan sederhana: bebatuan/kayu + pohon procedural
    void EnsureForestPropsEditor(Terrain terrain)
    {
        if (!terrain) return;
        try
        {
            // Bersihkan dekorasi agar hanya tersisa tanah
            CleanupForestDecorationsEditor();
            // Tetap bersihkan area tengah (jaga arena rata)
            ClearArenaCenterOnTerrain(terrain, 12f);
            // Tambahkan dinding pembatas di tepi terrain agar tidak jatuh keluar
            CreateBoundaryWallsEditor(terrain, 2f, 50f);
            if (verbose) Debug.Log("[SceneValidator][AutoFix] Membersihkan dekorasi (tanpa pohon/props) dan menambahkan dinding pembatas tepi terrain.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal membuat dekorasi hutan: " + e.Message);
        }
    }

    // Hapus dekorasi yang pernah dibuat (pohon dan props) agar tinggal tanah saja
    void CleanupForestDecorationsEditor()
    {
        var trees = GameObject.Find("Forest_Trees");
        if (trees) UnityEngine.Object.DestroyImmediate(trees);
        var props = GameObject.Find("Forest_Props");
        if (props) UnityEngine.Object.DestroyImmediate(props);
    }

    // Buat 4 dinding BoxCollider di sekeliling terrain agar player/boss tidak jatuh keluar
    void CreateBoundaryWallsEditor(Terrain terrain, float thickness, float height)
    {
        if (!terrain || terrain.terrainData == null) return;
        var size = terrain.terrainData.size;
        var basePos = terrain.transform.position;

        var parent = GameObject.Find("TerrainBoundary") ?? new GameObject("TerrainBoundary");

        // Hapus dinding lama jika ada
        foreach (Transform c in parent.transform)
        {
            UnityEngine.Object.DestroyImmediate(c.gameObject);
        }

        float y = basePos.y + height * 0.5f;
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;

        // Kiri
        CreateWall(parent.transform, new Vector3(basePos.x - thickness * 0.5f, y, basePos.z + halfZ), new Vector3(thickness, height, size.z + thickness));
        // Kanan
        CreateWall(parent.transform, new Vector3(basePos.x + size.x + thickness * 0.5f, y, basePos.z + halfZ), new Vector3(thickness, height, size.z + thickness));
        // Depan
        CreateWall(parent.transform, new Vector3(basePos.x + halfX, y, basePos.z - thickness * 0.5f), new Vector3(size.x + thickness, height, thickness));
        // Belakang
        CreateWall(parent.transform, new Vector3(basePos.x + halfX, y, basePos.z + size.z + thickness * 0.5f), new Vector3(size.x + thickness, height, thickness));
    }

    void CreateWall(Transform parent, Vector3 position, Vector3 size)
    {
        var go = new GameObject("BoundaryWall");
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
        box.isTrigger = false;
        try { UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.BatchingStatic | UnityEditor.StaticEditorFlags.NavigationStatic); } catch {}
    }
#endif
    // ====== AUTO-SPAWN PLACEHOLDERS ======
    GameObject CreateBasicPlayer()
    {
        try
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.tag = "Player";
            // Remove collider if CharacterController will handle collisions
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            go.AddComponent<PlayerHealth>();

            // Tempatkan di pusat Terrain agar tidak di ujung
            go.transform.position = GetTerrainCenterOnSurface();
            AlignTransformToGround(go.transform, 0.05f);
            return go;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal membuat Player sederhana: " + e.Message);
            return null;
        }
    }

    GameObject CreateBasicBoss()
    {
        try
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Boss";
            // boss layer/tags opsional
            // Collider dari primitive tidak dibutuhkan saat pakai CharacterController
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);

            // Tambah komponen inti
            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 2.5f;

            var bossCC = go.AddComponent<CharacterController>();
            bossCC.height = 2.2f;
            bossCC.center = new Vector3(0f, 1.1f, 0f);

            go.AddComponent<BossHealth>();
            var ctrl = go.AddComponent<BossController>();

            // Buat child Attacks
            var attacksRoot = new GameObject("Attacks");
            attacksRoot.transform.SetParent(go.transform, false);

            // HitMask diarahkan ke layer Player kalau ada
            int playerLayer = 0;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) playerLayer = player.layer;
            LayerMask hitMask = 0;
            if (playerLayer >= 0 && playerLayer <= 31) hitMask = (1 << playerLayer);

            // Tambah serangan jika tipe tersedia
            try { var c = attacksRoot.AddComponent<CleaveAttack>(); c.hitMask = hitMask; } catch {}
            try { var s = attacksRoot.AddComponent<SlamAttack>(); s.hitMask = hitMask; } catch {}
            try { var d = attacksRoot.AddComponent<DashAttack>(); d.hitMask = hitMask; } catch {}

            // Posisi Boss berhadapan dengan Player di arah kamera pada jarak tetap
            Vector3 center = GetTerrainCenterOnSurface();
            Vector3 basis = (player ? player.transform.position : center);
            Vector3 dir = GetCameraFlatForward();
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            float distance = 8f;
            Vector3 spawnPos = basis + dir * distance;
            go.transform.position = spawnPos;
            // Putar boss menghadap ke Player/basis
            Vector3 look = (basis - go.transform.position); look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            AlignTransformToGround(go.transform, 0.05f);

            return go;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal membuat Boss sederhana: " + e.Message);
            return null;
        }
    }

    void PositionMainCameraNear(Transform target)
    {
        var cam = Camera.main;
        if (!cam || !target) return;
        cam.transform.position = target.position + new Vector3(0f, 2f, -5f);
        cam.transform.rotation = Quaternion.LookRotation(target.position - cam.transform.position, Vector3.up);
    }

#if UNITY_EDITOR
    GameObject TrySpawnStarterAssetsPlayerEditor()
    {
        try
        {
            // Cari prefab di proyek (umumnya berada di Assets/StarterAssets/ThirdPersonController/Prefabs)
            string[] candidates = new[] { "ThirdPersonController", "PlayerArmature" };
            foreach (var name in candidates)
            {
                // Cari prefab dengan nama kandidat
                string filter = "t:Prefab " + name;
                var guids = AssetDatabase.FindAssets(filter);
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    // Instantiate sebagai Prefab agar link tetap
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (go == null) continue;
                    go.name = prefab.name;
                    // Set tag Player dan tambahkan PlayerHealth bila belum ada
                    try { go.tag = "Player"; } catch {}
                    if (!go.GetComponent<PlayerHealth>()) go.AddComponent<PlayerHealth>();
                    // Pastikan ada CharacterController (harusnya bawaan Starter Assets sudah ada)
                    if (!go.GetComponent<CharacterController>()) go.AddComponent<CharacterController>();
                    // Posisikan dekat kamera
                    PositionMainCameraNear(go.transform);
                    return go;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal spawn Starter Assets Player: " + e.Message);
        }
        return null;
    }

    [MenuItem("Tools/Scene/Jalankan Scene Validator")]
    static void RunSceneValidatorMenu()
    {
        try
        {
            var existing = FindFirst<SceneValidator>(true);
            if (!existing)
            {
                var go = new GameObject("SceneValidator");
                existing = go.AddComponent<SceneValidator>();
                existing.verbose = true;
                existing.enableAutoFix = true;
                existing.preferCinemachine = true;
            }
            existing.RunValidation();
            Debug.Log("[SceneValidator][Menu] Validasi scene dijalankan.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneValidator] Gagal menjalankan validasi via menu: " + e.Message);
        }
    }
#endif
}
