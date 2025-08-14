using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Weapon")]
    public Transform weaponRoot;            // parent untuk senjata
    public WeaponHitbox weaponHitbox;       // hitbox serangan
    public Transform weaponModel;           // visual pedang
    public float weaponDamageLight = 10f;
    public float weaponDamageHeavy = 20f;

    [Header("Combo")]
    public int maxCombo = 3;
    public float lightAttackDuration = 0.4f;     // durasi total tiap step combo
    public float lightActiveStart = 0.15f;       // mulai aktif hitbox
    public float lightActiveEnd = 0.30f;         // selesai aktif
    public float comboQueueWindow = 0.2f;        // waktu di akhir step untuk input berikutnya

    [Header("Heavy Attack")]
    public float heavyWindup = 0.35f;
    public float heavyActiveTime = 0.2f;
    public float heavyRecovery = 0.35f;

    [Header("Parry")]
    public KeyCode parryKey = KeyCode.F;
    public float parryWindow = 0.25f;            // window perfect parry
    public float parryCooldown = 0.6f;           // setelah parry selesai
    public bool isParrying { get; private set; }

    [Header("Input")]
    public KeyCode lightKey = KeyCode.Mouse0;
    public KeyCode heavyKey = KeyCode.Mouse1;

    int comboStep = 0;
    bool inAttack = false;
    bool comboQueued = false;
    float lastParryTime = -999f;
    Animator anim;

    void Awake()
    {
        EnsureWeapon();
        anim = GetComponent<Animator>();
    }

    void EnsureWeapon()
    {
        // Cari tulang tangan kanan jika ada Animator humanoid
        Transform rightHand = null;
        if (!weaponRoot)
        {
            var animHum = GetComponent<Animator>();
            if (animHum && animHum.isHuman)
            {
                rightHand = animHum.GetBoneTransform(HumanBodyBones.RightHand);
            }

            var wr = new GameObject("WeaponRoot").transform;
            if (rightHand)
            {
                wr.SetParent(rightHand, false);
                wr.localPosition = new Vector3(0.05f, 0.0f, 0.1f);
                wr.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                wr.SetParent(transform, false);
                wr.localPosition = new Vector3(0.25f, 1.0f, 0.6f); // fallback: di depan kanan sedikit
                wr.localRotation = Quaternion.identity;
            }
            weaponRoot = wr;
        }
        if (!weaponHitbox)
        {
            var sword = new GameObject("Greatsword");
            sword.transform.SetParent(weaponRoot, false);
            sword.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            sword.transform.localRotation = Quaternion.identity;
            sword.transform.localScale = new Vector3(0.18f, 0.18f, 1.4f);

            var mf = sword.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateSwordMesh(); // mesh sederhana balok tipis
            var mr = sword.AddComponent<MeshRenderer>();
            var lit = GetDefaultLitShader();
            var mat = new Material(lit);
            mat.color = new Color(0.75f, 0.75f, 0.8f, 1f);
            mr.sharedMaterial = mat;

            var col = sword.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var hit = sword.AddComponent<WeaponHitbox>();
            hit.owner = this.gameObject;
            hit.damage = weaponDamageLight;
            hit.enabled = false; // hanya aktif saat frame serang
            weaponHitbox = hit;
            weaponModel = sword.transform;
        }
    }

    Shader GetDefaultLitShader()
    {
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        var s = Shader.Find("Universal Render Pipeline/Lit");
        if (s) return s;
#elif UNITY_RENDER_PIPELINE_HDRP
        var s = Shader.Find("HDRP/Lit");
        if (s) return s;
#endif
        var std = Shader.Find("Standard");
        return std ? std : Shader.Find("Universal Render Pipeline/Lit");
    }

    Mesh CreateSwordMesh()
    {
        // Balok sederhana sebagai placeholder pedang
        var mesh = new Mesh();
        Vector3[] v = new Vector3[]
        {
            new Vector3(-0.05f,-0.05f,-0.8f), new Vector3(0.05f,-0.05f,-0.8f), new Vector3(0.05f,0.05f,-0.8f), new Vector3(-0.05f,0.05f,-0.8f),
            new Vector3(-0.05f,-0.05f, 0.8f), new Vector3(0.05f,-0.05f, 0.8f), new Vector3(0.05f,0.05f, 0.8f), new Vector3(-0.05f,0.05f, 0.8f)
        };
        int[] t = new int[]
        {
            0,2,1, 0,3,2, // back
            4,5,6, 4,6,7, // front
            0,1,5, 0,5,4, // bottom
            2,3,7, 2,7,6, // top
            1,2,6, 1,6,5, // right
            3,0,4, 3,4,7  // left
        };
        mesh.vertices = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        return mesh;
    }

    void Update()
    {
        // Input parry
        if (ParryPressedThisFrame() && Time.time >= lastParryTime + parryCooldown)
        {
            StartCoroutine(CoParry());
        }

        // Input serang
        if (!inAttack)
        {
            if (LightPressedThisFrame())
            {
                StartCoroutine(CoLightCombo());
                return;
            }
            if (HeavyPressedThisFrame())
            {
                StartCoroutine(CoHeavyAttack());
                return;
            }
        }
        else
        {
            // Saat sedang menyerang, antrikan input untuk step berikutnya
            if (LightPressedThisFrame()) comboQueued = true;
        }
    }

    // Helper input agar kompatibel dengan Input System baru maupun legacy
    bool LightPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (lightKey == KeyCode.Mouse0) return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (lightKey == KeyCode.Mouse1) return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        if (Keyboard.current != null)
        {
            if (lightKey == KeyCode.F) return Keyboard.current.fKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(lightKey);
#endif
    }

    bool HeavyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (heavyKey == KeyCode.Mouse0) return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (heavyKey == KeyCode.Mouse1) return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        if (Keyboard.current != null)
        {
            if (heavyKey == KeyCode.F) return Keyboard.current.fKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(heavyKey);
#endif
    }

    bool ParryPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (parryKey == KeyCode.Mouse0) return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (parryKey == KeyCode.Mouse1) return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        if (Keyboard.current != null)
        {
            if (parryKey == KeyCode.F) return Keyboard.current.fKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(parryKey);
#endif
    }

    IEnumerator CoLightCombo()
    {
        inAttack = true;
        comboQueued = false;
        float t;
        bool proceedNext = false;
        do
        {
            proceedNext = false;
            comboStep = Mathf.Clamp(comboStep + 1, 1, maxCombo);
            // Trigger animasi sesuai step
            if (anim)
            {
                string trig = comboStep == 1 ? "Light1" : (comboStep == 2 ? "Light2" : "Light3");
                anim.ResetTrigger("Light1"); anim.ResetTrigger("Light2"); anim.ResetTrigger("Light3");
                anim.SetTrigger(trig);
            }
            // Aktifkan hitbox di window aktif
            t = 0f;
            weaponHitbox.damage = weaponDamageLight;
            Quaternion initialRot = weaponModel ? weaponModel.localRotation : Quaternion.identity;
            while (t < lightAttackDuration)
            {
                bool active = (t >= lightActiveStart && t <= lightActiveEnd);
                weaponHitbox.enabled = active;
                // Ayunan sederhana: amplitudo berdasarkan step
                if (weaponModel)
                {
                    float norm = Mathf.Clamp01(t / lightAttackDuration);
                    float amp = comboStep == 1 ? 45f : (comboStep == 2 ? 60f : 75f);
                    float angle = Mathf.Sin(norm * Mathf.PI) * amp; // 0..amp..0
                    weaponModel.localRotation = initialRot * Quaternion.Euler(0f, 0f, -angle);
                }
                t += Time.deltaTime;
                yield return null;
                // Di akhir serangan, izinkan queue
                if (lightAttackDuration - t <= comboQueueWindow && comboQueued && comboStep < maxCombo)
                {
                    proceedNext = true;     // tandai lanjut ke step berikutnya
                    comboQueued = false;    // konsumsi input yang diantrikan
                    break;                  // keluar dari loop durasi step
                }
            }
            if (weaponModel) weaponModel.localRotation = initialRot;
        } while (comboStep < maxCombo && proceedNext);

        weaponHitbox.enabled = false;
        inAttack = false;
        comboStep = 0;
    }

    IEnumerator CoHeavyAttack()
    {
        inAttack = true;
        if (anim)
        {
            anim.ResetTrigger("Heavy");
            anim.SetTrigger("Heavy");
        }
        // windup
        float t = 0f;
        Quaternion initialRot = weaponModel ? weaponModel.localRotation : Quaternion.identity;
        while (t < heavyWindup) { t += Time.deltaTime; yield return null; }
        // active
        weaponHitbox.damage = weaponDamageHeavy;
        weaponHitbox.enabled = true;
        t = 0f;
        while (t < heavyActiveTime)
        {
            if (weaponModel)
            {
                float norm = Mathf.Clamp01(t / heavyActiveTime);
                float angle = Mathf.Sin(norm * Mathf.PI) * 100f;
                weaponModel.localRotation = initialRot * Quaternion.Euler(0f, 0f, -angle);
            }
            t += Time.deltaTime; yield return null;
        }
        weaponHitbox.enabled = false;
        // recovery
        t = 0f;
        while (t < heavyRecovery)
        {
            t += Time.deltaTime; yield return null;
        }
        if (weaponModel) weaponModel.localRotation = initialRot;
        inAttack = false;
    }

    IEnumerator CoParry()
    {
        isParrying = true;
        lastParryTime = Time.time;
        if (anim)
        {
            anim.ResetTrigger("Parry");
            anim.SetTrigger("Parry");
        }
        float t = 0f;
        // Anda bisa menambahkan efek VFX/SFX di sini
        while (t < parryWindow)
        {
            t += Time.deltaTime;
            yield return null;
        }
        isParrying = false;
    }

    // Dipanggil dari hitbox musuh untuk mencoba parry
    public bool TryParryIncoming(GameObject attacker)
    {
        if (isParrying)
        {
            // TODO: tambahkan logika balikkan serangan/men-stun musuh
            Debug.Log("[PlayerCombat] Parry sukses terhadap " + attacker.name);
            return true;
        }
        return false;
    }
}
