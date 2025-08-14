using UnityEngine;
// Dukungan Input System baru (paket Unity Input System)
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(50)]
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform followTarget; // Disarankan: CameraPivot (anak dari Player)
    public string playerTag = "Player";

    [Header("Orbit Settings")]
    public float distance = 4.5f;
    public float minDistance = 1.0f;
    public float maxDistance = 6.0f;
    public float mouseSensitivityX = 250f; // derajat/detik
    public float mouseSensitivityY = 150f; // derajat/detik
    public float minPitch = -35f;
    public float maxPitch = 70f;

    [Header("Smoothing")]
    public float rotationDamp = 12f;
    public float distanceDamp = 10f;

    [Header("Collision")]
    public LayerMask collisionMask = ~0; // default: semua layer
    public float sphereCastRadius = 0.2f;
    public float collisionBuffer = 0.1f;

    [Header("UX")]
    public bool lockCursor = true;

    float yaw;   // rotasi horizontal
    float pitch; // rotasi vertikal
    float targetDistance;
    Vector3 currentCamPos;
    Quaternion currentCamRot;

    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);

        if (followTarget == null)
        {
            AutoAssignTarget();
        }

        // Inisialisasi rotasi dari arah kamera terhadap target
        if (followTarget)
        {
            Vector3 dir = (transform.position - followTarget.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                // hitung yaw/pitch dari dir
                yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float lenXZ = new Vector2(dir.x, dir.z).magnitude;
                pitch = Mathf.Atan2(dir.y, lenXZ) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
        }
        currentCamPos = transform.position;
        currentCamRot = transform.rotation;
    }

    void LateUpdate()
    {
        if (!followTarget)
        {
            // Coba ulang assign target secara silent saat runtime
            AutoAssignTarget(silent:true);
            if (!followTarget) return;
        }

        // Input mouse
        float mx = 0f, my = 0f;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current != null)
        {
            // delta dalam pixel/frame, skala kecil agar nyaman
            Vector2 d = Mouse.current.delta.ReadValue();
            mx = d.x * 0.02f;
            my = d.y * 0.02f;
        }
#else
        mx = Input.GetAxisRaw("Mouse X");
        my = Input.GetAxisRaw("Mouse Y");
#endif

        yaw += mx * mouseSensitivityX * Time.deltaTime;
        pitch -= my * mouseSensitivityY * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Target rotasi dari yaw/pitch
        Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);

        // Hitung jarak dengan collision handling
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        Vector3 desiredCamOffset = targetRot * new Vector3(0f, 0f, -targetDistance);
        Vector3 desiredCamPos = followTarget.position + desiredCamOffset;

        // SphereCast untuk tabrakan agar kamera tidak tembus
        float adjustedDistance = targetDistance;
        Vector3 castDir = (desiredCamPos - followTarget.position).normalized;
        if (Physics.SphereCast(followTarget.position, sphereCastRadius, castDir, out RaycastHit hit, targetDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            adjustedDistance = Mathf.Max(minDistance, hit.distance - collisionBuffer);
        }

        Vector3 finalCamPos = followTarget.position + (targetRot * new Vector3(0f, 0f, -adjustedDistance));

        // Smooth damp (lerp dengan rot/pos damp)
        currentCamRot = Quaternion.Slerp(currentCamRot, targetRot, 1f - Mathf.Exp(-rotationDamp * Time.deltaTime));
        currentCamPos = Vector3.Lerp(currentCamPos, finalCamPos, 1f - Mathf.Exp(-distanceDamp * Time.deltaTime));

        transform.SetPositionAndRotation(currentCamPos, currentCamRot);
    }

    void AutoAssignTarget(bool silent = false)
    {
        // Cari Player
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (!player)
        {
            var ph = FindObjectOfType<PlayerHealth>(true);
            if (ph) player = ph.gameObject;
            if (!player)
            {
                // cari berdasarkan nama "Player"
                var all = FindObjectsOfType<Transform>(true);
                var found = System.Array.Find(all, t => string.Equals(t.name, "Player", System.StringComparison.OrdinalIgnoreCase));
                if (found) player = found.gameObject;
            }
        }
        if (!player)
        {
            if (!silent)
                Debug.LogWarning("[ThirdPersonCamera] Player tidak ditemukan untuk Follow. Set followTarget manual.");
            return;
        }

        // Cari atau buat CameraPivot dibawah Player
        Transform pivot = player.transform.Find("CameraPivot");
        if (!pivot)
        {
            GameObject pivotGO = new GameObject("CameraPivot");
            pivot = pivotGO.transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.7f, 0f);
            pivot.localRotation = Quaternion.identity;
            if (!silent)
                Debug.Log("[ThirdPersonCamera] Membuat CameraPivot di bawah Player.");
        }
        followTarget = pivot;
    }
}
