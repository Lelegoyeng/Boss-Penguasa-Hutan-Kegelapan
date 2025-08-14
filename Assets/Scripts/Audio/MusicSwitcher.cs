using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicSwitcher : MonoBehaviour
{
    public AudioClip intro;
    public AudioClip phase1;
    public AudioClip phase2;
    public float crossfadeTime = 0.8f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
    }

    public void PlayIntroThenPhase1()
    {
        if (intro)
        {
            source.loop = false;
            source.clip = intro;
            source.Play();
            Invoke(nameof(PlayPhase1), intro.length);
        }
        else
        {
            PlayPhase1();
        }
    }

    public void PlayPhase1()
    {
        CrossfadeTo(phase1);
    }

    public void PlayPhase2()
    {
        CrossfadeTo(phase2);
    }

    void CrossfadeTo(AudioClip clip)
    {
        if (!clip) return;
        StartCoroutine(CrossfadeRoutine(clip));
    }

    System.Collections.IEnumerator CrossfadeRoutine(AudioClip next)
    {
        float t = 0f;
        float startVol = source.volume;
        while (t < crossfadeTime)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, t / crossfadeTime);
            yield return null;
        }
        source.clip = next;
        source.loop = true;
        source.Play();
        t = 0f;
        while (t < crossfadeTime)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, startVol, t / crossfadeTime);
            yield return null;
        }
    }
}
