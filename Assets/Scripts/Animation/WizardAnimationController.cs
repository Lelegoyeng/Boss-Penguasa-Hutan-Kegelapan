using UnityEngine;

[RequireComponent(typeof(Animator))]
public class WizardAnimationController : MonoBehaviour
{
    // Animation parameter hashes
    private readonly int _speedHash = Animator.StringToHash("Speed");
    private readonly int _jumpHash = Animator.StringToHash("Jump");
    private readonly int _groundedHash = Animator.StringToHash("Grounded");
    private readonly int _attackHash = Animator.StringToHash("Attack");
    private readonly int _attackComboHash = Animator.StringToHash("AttackCombo");
    private readonly int _defendHash = Animator.StringToHash("Defend");
    private readonly int _hitHash = Animator.StringToHash("Hit");
    private readonly int _dieHash = Animator.StringToHash("Die");
    private readonly int _victoryHash = Animator.StringToHash("Victory");
    private readonly int _interactHash = Animator.StringToHash("Interact");

    private Animator _animator;
    private CharacterController _characterController;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // Update grounded state
        if (_characterController != null)
        {
            bool isGrounded = _characterController.isGrounded;
            _animator.SetBool(_groundedHash, isGrounded);
        }
    }

    public void SetSpeed(float speed)
    {
        _animator.SetFloat(_speedHash, speed);
    }

    public void TriggerJump()
    {
        _animator.SetTrigger(_jumpHash);
    }

    public void TriggerAttack(int combo)
    {
        _animator.SetInteger(_attackComboHash, combo);
        _animator.SetTrigger(_attackHash);
    }

    public void SetDefending(bool isDefending)
    {
        _animator.SetBool(_defendHash, isDefending);
    }

    public void TriggerHit()
    {
        _animator.SetTrigger(_hitHash);
    }

    public void TriggerDie()
    {
        _animator.SetTrigger(_dieHash);
    }

    public void TriggerVictory()
    {
        _animator.SetTrigger(_victoryHash);
    }

    public void TriggerInteract()
    {
        _animator.SetTrigger(_interactHash);
    }

    // Animation Events
    public void OnAttackStart()
    {
        // Can be used to enable weapon collider, play sound, etc.
    }

    public void OnAttackEnd()
    {
        // Can be used to disable weapon collider
    }

    public void OnFootstep()
    {
        // Play footstep sound based on surface
    }
}
