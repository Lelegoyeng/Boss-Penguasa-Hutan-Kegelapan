using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class WizardSimpleController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 12f;
    public float gravity = -9.81f;

    [Header("Animation")]
    public float moveThreshold = 0.1f;

    private CharacterController _cc;
    private Animator _anim;
    private float _verticalVel;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _anim = GetComponent<Animator>();
        if (_cc && _cc.center == Vector3.zero)
        {
            _cc.center = new Vector3(0, 1, 0);
            _cc.height = 2f;
            _cc.radius = 0.3f;
        }
    }

    void Update()
    {
        Vector2 input = ReadMoveInput();
        Vector3 dir = new Vector3(input.x, 0, input.y);
        dir = Vector3.ClampMagnitude(dir, 1f);

        // camera-relative
        if (Camera.main)
        {
            Vector3 camF = Camera.main.transform.forward; camF.y = 0; camF.Normalize();
            Vector3 camR = Camera.main.transform.right; camR.y = 0; camR.Normalize();
            dir = camF * dir.z + camR * dir.x;
        }

        // rotate
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // gravity
        if (_cc.isGrounded && _verticalVel < 0) _verticalVel = -2f;
        _verticalVel += gravity * Time.deltaTime;

        Vector3 velocity = dir * moveSpeed;
        velocity.y = _verticalVel;
        _cc.Move(velocity * Time.deltaTime);

        // animation (drive by Speed parameter only)
        if (_anim)
        {
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            _anim.SetFloat("Speed", speed);
        }
    }

    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        var gamepad = UnityEngine.InputSystem.Gamepad.current;
        float x = 0f, y = 0f;
        if (gamepad != null)
        {
            var ls = gamepad.leftStick.ReadValue();
            x = ls.x; y = ls.y;
        }
        else if (keyboard != null)
        {
            x = (keyboard.dKey.isPressed ? 1f : 0f) + (keyboard.aKey.isPressed ? -1f : 0f);
            y = (keyboard.wKey.isPressed ? 1f : 0f) + (keyboard.sKey.isPressed ? -1f : 0f);
        }
        return new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }
}
