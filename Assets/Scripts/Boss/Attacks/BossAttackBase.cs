using UnityEngine;
using System.Collections;

public abstract class BossAttackBase : MonoBehaviour
{
    [Header("Common Attack Settings")]
    public string attackName = "Attack";
    public float cooldown = 3f;
    public float windupTime = 0.5f;
    public float activeTime = 0.2f;
    public float recoveryTime = 0.4f;
    public float damage = 30f;
    public AnimationCurve dangerCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("SFX (Optional)")]
    public AudioSource audioSource;
    public AudioClip windupSfx;
    public AudioClip activeSfx;
    public AudioClip recoverySfx;

    protected bool isOnCooldown;
    public bool IsBusy { get; private set; }

    public System.Action<string> OnAttackStarted;
    public System.Action<string> OnAttackFinished;

    public virtual bool CanUse() => !isOnCooldown && !IsBusy;

    public IEnumerator ExecuteAttackRoutine(Transform target)
    {
        if (!CanUse()) yield break;
        IsBusy = true;
        OnAttackStarted?.Invoke(attackName);

        // Windup phase
        PlaySfx(windupSfx);
        yield return OnWindup(target);

        // Active phase
        PlaySfx(activeSfx);
        yield return OnActive(target);

        // Recovery phase
        PlaySfx(recoverySfx);
        yield return OnRecovery(target);

        IsBusy = false;
        OnAttackFinished?.Invoke(attackName);
        StartCoroutine(Cooldown());
    }

    protected virtual IEnumerator Cooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldown);
        isOnCooldown = false;
    }

    protected void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        if (audioSource)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

    protected abstract IEnumerator OnWindup(Transform target);
    protected abstract IEnumerator OnActive(Transform target);
    protected abstract IEnumerator OnRecovery(Transform target);
}
