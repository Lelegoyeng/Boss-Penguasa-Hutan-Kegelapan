using UnityEngine;

[CreateAssetMenu(fileName = "WizardAnimatorController", menuName = "Animation/Wizard Animator Controller")]
public class WizardAnimatorController : ScriptableObject
{
    [System.Serializable]
    public class AnimationClipReferences
    {
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;
        public AnimationClip jumpStart;
        public AnimationClip jumpLoop;
        public AnimationClip jumpEnd;
        public AnimationClip[] attacks;
        public AnimationClip defendStart;
        public AnimationClip defendLoop;
        public AnimationClip defendHit;
        public AnimationClip getHit;
        public AnimationClip die;
        public AnimationClip victory;
    }

    public AnimationClipReferences animationClips;
    
    [Header("Animator Parameters")]
    public string speedParameter = "Speed";
    public string jumpParameter = "Jump";
    public string groundedParameter = "Grounded";
    public string attackParameter = "Attack";
    public string attackComboParameter = "AttackCombo";
    public string defendParameter = "Defend";
    public string hitParameter = "Hit";
    public string dieParameter = "Die";
    public string victoryParameter = "Victory";
    public string interactParameter = "Interact";

    private Animator _animator;
    private int _currentAttackIndex;

    public void Initialize(Animator animator)
    {
        _animator = animator;
        _currentAttackIndex = 0;
    }

    public void SetSpeed(float speed)
    {
        if (_animator != null)
            _animator.SetFloat(speedParameter, speed);
    }

    public void SetGrounded(bool isGrounded)
    {
        if (_animator != null)
            _animator.SetBool(groundedParameter, isGrounded);
    }

    public void TriggerJump()
    {
        if (_animator != null)
            _animator.SetTrigger(jumpParameter);
    }

    public void TriggerAttack()
    {
        if (_animator != null && animationClips.attacks != null && animationClips.attacks.Length > 0)
        {
            _currentAttackIndex = (_currentAttackIndex + 1) % animationClips.attacks.Length;
            _animator.SetInteger(attackComboParameter, _currentAttackIndex);
            _animator.SetTrigger(attackParameter);
        }
    }

    public void SetDefending(bool isDefending)
    {
        if (_animator != null)
            _animator.SetBool(defendParameter, isDefending);
    }

    public void TriggerHit()
    {
        if (_animator != null)
            _animator.SetTrigger(hitParameter);
    }

    public void TriggerDie()
    {
        if (_animator != null)
            _animator.SetTrigger(dieParameter);
    }

    public void TriggerVictory()
    {
        if (_animator != null)
            _animator.SetTrigger(victoryParameter);
    }

    public void TriggerInteract()
    {
        if (_animator != null)
            _animator.SetTrigger(interactParameter);
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
