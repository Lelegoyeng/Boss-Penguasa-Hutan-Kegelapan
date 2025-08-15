using UnityEngine;

public class ThirdPersonFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 6f, -8f);
    public float followLerp = 8f;
    public float lookLerp = 12f;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 desired = target.position + target.TransformVector(offset);
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
        Quaternion look = Quaternion.LookRotation((target.position + Vector3.up * 1.5f) - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-lookLerp * Time.deltaTime));
    }
}
