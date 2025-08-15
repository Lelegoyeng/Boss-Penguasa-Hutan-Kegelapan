using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(WizardAnimationController))]
public class WizardSimpleController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float rotationSpeed = 12f;
    public float jumpHeight = 1.5f;
    public float gravity = -15f;
    public float groundCheckDistance = 0.3f;
    public LayerMask groundMask;

    [Header("Combat")]
    public Transform attackPoint;
    public float attackCooldown = 0.5f;
    private float _lastAttackTime;
    private int _attackCombo;
    private bool _isDefending;
    private bool _isAttacking;
    private AudioSource _audioSource;
    private AudioClip _defendSFX;

    // Components
    private CharacterController _characterController;
    private WizardAnimationController _animationController;
    private Transform _cameraTransform;

    // Input
    private PlayerControls _playerControls;
    private Vector2 _moveInput;
    
    // Movement
    private float _verticalVel;
    private bool _isGrounded;
    private Vector3 _moveDirection;
    private float _currentSpeed;
    private GameObject _defendEffectPrefab;
    private GameObject _currentDefendEffect;

    void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _animationController = GetComponent<WizardAnimationController>();
        _cameraTransform = Camera.main.transform;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.volume = 1.0f; // Set volume to max
        _defendSFX = Resources.Load<AudioClip>("shield");
        _defendEffectPrefab = Resources.Load<GameObject>("Effects/Magic shield blue");

        if (_defendEffectPrefab == null)
        {
            Debug.LogError("Defend effect prefab not found. Make sure 'Magic shield blue.prefab' is inside the 'Assets/Resources/Effects' folder.");
        }

        
        // Initialize character controller if needed
        if (_characterController && _characterController.center == Vector3.zero)
        {
            _characterController.center = new Vector3(0, 1, 0);
            _characterController.height = 1.8f;
            _characterController.radius = 0.3f;
        }
    }

    private void OnEnable()
    {
        if (_playerControls == null)
        {
            _playerControls = new PlayerControls();
        }
        _playerControls.Player.Enable();
        _playerControls.Player.Jump.performed += OnJump;
        _playerControls.Player.Attack.performed += OnAttack;
        _playerControls.Player.Defend.performed += OnDefend;
        _playerControls.Player.Defend.canceled += OnDefendCanceled;
    }

    private void OnDisable()
    {
        if (_playerControls != null)
        {
            _playerControls.Player.Disable();
            _playerControls.Player.Jump.performed -= OnJump;
            _playerControls.Player.Attack.performed -= OnAttack;
            _playerControls.Player.Defend.performed -= OnDefend;
            _playerControls.Player.Defend.canceled -= OnDefendCanceled;
        }
    }

    void Update()
    {
        HandleGravity();
        HandleMovement();
        UpdateAnimation();
    }

    void HandleGravity()
    {
        _isGrounded = Physics.CheckSphere(transform.position, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
        
        if (_isGrounded && _verticalVel < 0)
        {
            _verticalVel = -2f;
        }
        _verticalVel += gravity * Time.deltaTime;
        _characterController.Move(Vector3.up * (_verticalVel * Time.deltaTime));
    }

    void HandleMovement()
    {
        if (_isAttacking) return;

        _moveInput = _playerControls.Player.Move.ReadValue<Vector2>();

        Vector3 forward = _cameraTransform.forward;
        Vector3 right = _cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        _moveDirection = (forward * _moveInput.y + right * _moveInput.x).normalized;

        if (_moveDirection.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(_moveDirection.x, _moveDirection.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationSpeed, 0.1f);
            transform.rotation = Quaternion.Euler(0, angle, 0);

            if (!_isDefending)
            {
                // Note: Running is not implemented in the input actions, using walkSpeed.
                _currentSpeed = walkSpeed;
                _characterController.Move(_moveDirection * (_currentSpeed * Time.deltaTime));
            }
        }
        else
        {
            _currentSpeed = 0f;
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (_isGrounded)
        {
            _verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _animationController.TriggerJump();
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!_isAttacking && Time.time > _lastAttackTime + attackCooldown)
        {
            _isAttacking = true;
            _attackCombo = (_attackCombo % 3) + 1; // Cycle through 3 attack combos
            _animationController.TriggerAttack(_attackCombo);
            _lastAttackTime = Time.time;

            // Reset attack state after animation (ideally, use animation events)
            Invoke(nameof(ResetAttack), 0.5f);
        }
    }

    private void OnDefend(InputAction.CallbackContext context)
    {
        _isDefending = true;
        _animationController.SetDefending(true);

        if (_defendSFX != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_defendSFX);
        }

        if (_defendEffectPrefab && _currentDefendEffect == null)
        {
            _currentDefendEffect = Instantiate(_defendEffectPrefab, transform.position, transform.rotation, transform);
        }
    }

    private void OnDefendCanceled(InputAction.CallbackContext context)
    {
        _isDefending = false;
        _animationController.SetDefending(false);

        if (_currentDefendEffect != null)
        {
            Destroy(_currentDefendEffect);
            _currentDefendEffect = null;
        }
    }

    void ResetAttack()
    {
        _isAttacking = false;
    }

    void UpdateAnimation()
    {
        if (_animationController == null) return;
        
        // Update speed in animation controller
        float speed = _moveDirection.magnitude;
        _animationController.SetSpeed(speed);
        _animationController.SetGrounded(_isGrounded);
    }

}
