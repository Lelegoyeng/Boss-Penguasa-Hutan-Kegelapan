using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(WizardAnimationController))]
public class WizardSimpleController : MonoBehaviour
{
    [Header("UI")]
    public SpellBarUI spellBarUI;
    [Header("Movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float rotationSpeed = 12f;
    public float jumpHeight = 1.5f;
    public float gravity = -15f;
    public float groundCheckDistance = 0.3f;
    public LayerMask groundMask;

    [Header("Combat")]
    public Transform spellCastPoint;
    public GameObject castingEffectPrefab; // Prefab untuk efek lingkaran sihir
    private float _lastAttackTime;
    private bool _isDefending;
    private bool _isAttacking;
    private AudioSource _audioSource;
    private AudioClip _defendSFX;

    [Header("Spells")]
    private Spell[] _spells;
    private int _currentSpellIndex = 0;

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
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.volume = 1.0f;

        _defendSFX = Resources.Load<AudioClip>("shield");
        _defendEffectPrefab = Resources.Load<GameObject>("Effects/Magic shield blue");

        // Otomatisasi: Buat SpellCastPoint jika tidak ada
        if (spellCastPoint == null)
        {
            Debug.Log("SpellCastPoint tidak ditemukan, membuat secara otomatis.");
            GameObject castPointObject = new GameObject("SpellCastPoint");
            castPointObject.transform.SetParent(transform);
            // Posisikan di depan dan sedikit di atas karakter
            castPointObject.transform.localPosition = new Vector3(0, 1.5f, 0.5f);
            spellCastPoint = castPointObject.transform;
        }

        // Muat semua aset Spell dari folder Resources/Spells
        Debug.Log("[WizardController] Loading spells from Resources/Spells...");
        _spells = Resources.LoadAll<Spell>("Spells").OrderBy(s => s.name).ToArray();
        if (_spells.Length == 0)
        {
            Debug.LogError("[WizardController] NO SPELLS FOUND in 'Assets/Resources/Spells'. Please create Spell assets there.");
        }
        else
        {
            Debug.Log($"[WizardController] Successfully loaded {_spells.Length} spells.");

            if (spellBarUI != null)
            {
                Debug.Log("[WizardController] SpellBarUI reference is found. Creating spell slots...");
                spellBarUI.CreateSpellSlots(_spells.ToList());
            }
            else
            {
                Debug.LogError("[WizardController] SpellBarUI reference is NULL. Please assign it in the Inspector or check BossEpicSceneBuilder.");
            }

            // Set default spell to fire spell if it exists
            int fireSpellIndex = System.Array.FindIndex(_spells, s => s.spellName.ToLower().Contains("fire"));
            _currentSpellIndex = fireSpellIndex != -1 ? fireSpellIndex : 0;
            SwitchSpell(_currentSpellIndex);
        }

        if (_characterController && _characterController.center == Vector3.zero)
        {
            _characterController.center = new Vector3(0, 1, 0);
            _characterController.height = 1.8f;
            _characterController.radius = 0.3f;
        }
    }

    private void OnEnable()
    {
        if (_playerControls == null) _playerControls = new PlayerControls();
        _playerControls.Player.Enable();
        _playerControls.Player.Jump.performed += OnJump;
        _playerControls.Player.Attack.performed += OnAttack;
        _playerControls.Player.Defend.performed += OnDefend;
        _playerControls.Player.Defend.canceled += OnDefendCanceled;

        _playerControls.Player.SwitchSpell1.performed += ctx => SwitchSpell(0);
        _playerControls.Player.SwitchSpell2.performed += ctx => SwitchSpell(1);
        _playerControls.Player.SwitchSpell3.performed += ctx => SwitchSpell(2);
        _playerControls.Player.SwitchSpell4.performed += ctx => SwitchSpell(3);
        _playerControls.Player.SwitchSpell5.performed += ctx => SwitchSpell(4);
        _playerControls.Player.SwitchSpell6.performed += ctx => SwitchSpell(5);
        _playerControls.Player.SwitchSpell7.performed += ctx => SwitchSpell(6);
        _playerControls.Player.SwitchSpell8.performed += ctx => SwitchSpell(7);
        _playerControls.Player.SwitchSpell9.performed += ctx => SwitchSpell(8);
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

            _playerControls.Player.SwitchSpell1.performed -= ctx => SwitchSpell(0);
            _playerControls.Player.SwitchSpell2.performed -= ctx => SwitchSpell(1);
            _playerControls.Player.SwitchSpell3.performed -= ctx => SwitchSpell(2);
            _playerControls.Player.SwitchSpell4.performed -= ctx => SwitchSpell(3);
            _playerControls.Player.SwitchSpell5.performed -= ctx => SwitchSpell(4);
            _playerControls.Player.SwitchSpell6.performed -= ctx => SwitchSpell(5);
            _playerControls.Player.SwitchSpell7.performed -= ctx => SwitchSpell(6);
            _playerControls.Player.SwitchSpell8.performed -= ctx => SwitchSpell(7);
            _playerControls.Player.SwitchSpell9.performed -= ctx => SwitchSpell(8);
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
        if (_isGrounded && _verticalVel < 0) _verticalVel = -2f;
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
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, targetAngle, 0), rotationSpeed * Time.deltaTime);
            if (!_isDefending) _characterController.Move(_moveDirection * (walkSpeed * Time.deltaTime));
        }
    }
    void SwitchSpell(int index)
    {
        if (index >= 0 && index < _spells.Length)
        {
            _currentSpellIndex = index;
            Debug.Log($"Sihir aktif: {_spells[_currentSpellIndex].spellName}");

            if (spellBarUI != null)
            {
                spellBarUI.UpdateHighlight(_currentSpellIndex);
            }
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
        Debug.Log("[OnAttack] Attack input received.");

        if (_spells.Length == 0) { Debug.LogWarning("[OnAttack] Cancelled: No spells loaded."); return; }
        if (_isAttacking) { Debug.LogWarning("[OnAttack] Cancelled: Already attacking."); return; }
        if (_isDefending) { Debug.LogWarning("[OnAttack] Cancelled: Currently defending."); return; }

        Spell currentSpell = _spells[_currentSpellIndex];
        Debug.Log($"[OnAttack] Attempting to cast '{currentSpell.spellName}'.");

        if (Time.time > _lastAttackTime + currentSpell.cooldown)
        {
            _isAttacking = true;
            _lastAttackTime = Time.time;

            Vector3 lookDirection = _cameraTransform.forward; lookDirection.y = 0;
            transform.rotation = Quaternion.LookRotation(lookDirection);
            _animationController.TriggerAttack(1);

            if (currentSpell.castSound != null) 
            {
                _audioSource.PlayOneShot(currentSpell.castSound);
                Debug.Log("[OnAttack] Cast sound played.");
            }

            // Panggil FireProjectile setelah jeda singkat agar sinkron dengan animasi
            Invoke(nameof(FireProjectileWrapper), 0.2f);
            
            Invoke(nameof(ResetAttack), 0.5f);
        }
        else
        {
            Debug.LogWarning($"[OnAttack] Spell '{currentSpell.spellName}' is on cooldown.");
        }
    }

    private void FireProjectileWrapper()
    {
        FireProjectile(_spells[_currentSpellIndex]);
    }

    private void FireProjectile(Spell currentSpell)
    {
        // Munculkan efek lingkaran sihir di kaki karakter
        if (castingEffectPrefab != null)
        {
            // Posisikan di kaki pemain (y=0) dan jangan jadikan anak dari pemain
            Vector3 spawnPosition = new Vector3(transform.position.x, 0.05f, transform.position.z);
            GameObject magicCircle = Instantiate(castingEffectPrefab, spawnPosition, Quaternion.identity, null);
            Destroy(magicCircle, 1.5f); // Hapus efek setelah 1.5 detik
        }

        if (currentSpell.projectilePrefab != null)
        {
            GameObject projectileGO = Instantiate(currentSpell.projectilePrefab, spellCastPoint.position, _cameraTransform.rotation);
            projectileGO.transform.localScale *= currentSpell.projectileScale;

            Projectile p = projectileGO.GetComponent<Projectile>();
            if (p != null)
            {
                p.speed = currentSpell.speed;
                p.lifeTime = currentSpell.lifeTime;
                p.hitEffect = currentSpell.hitEffect;
                p.hitSound = currentSpell.hitSound;
                p.hitEffectScale = currentSpell.hitEffectScale;
            }
            else
            {
                Debug.LogError($"[FireProjectile] CRITICAL: Projectile prefab '{currentSpell.projectilePrefab.name}' is MISSING the 'Projectile.cs' script!");
            }
        }
        else
        {
            Debug.LogError($"[FireProjectile] CRITICAL: The spell '{currentSpell.spellName}' has a NULL projectilePrefab!");
        }
    }

    private void OnDefend(InputAction.CallbackContext context)
    {
        _isDefending = true;
        _animationController.SetDefending(true);
        if (_defendSFX != null) _audioSource.PlayOneShot(_defendSFX);
        if (_defendEffectPrefab && _currentDefendEffect == null) _currentDefendEffect = Instantiate(_defendEffectPrefab, transform.position, transform.rotation, transform);
    }

    private void OnDefendCanceled(InputAction.CallbackContext context)
    {
        _isDefending = false;
        _animationController.SetDefending(false);
        if (_currentDefendEffect != null) Destroy(_currentDefendEffect);
    }

    void ResetAttack() { _isAttacking = false; }

    void UpdateAnimation()
    {
        if (_animationController == null) return;
        _animationController.SetSpeed(_moveDirection.magnitude);
        _animationController.SetGrounded(_isGrounded);
    }
}
