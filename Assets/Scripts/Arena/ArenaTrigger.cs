using UnityEngine;

public class ArenaTrigger : MonoBehaviour
{
    [Header("Setup")] 
    public Collider triggerZone; // if null, use this collider (isTrigger)
    public GameObject[] barriersToEnable;
    public BossController boss;
    public MusicSwitcher music;
    public BossHealthBar bossUI;

    private bool activated;
    private bool subscribedPhase2;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activated) return;
        if (!triggerZone || other == triggerZone || other.CompareTag("Player"))
        {
            Activate();
        }
    }

    public void Activate()
    {
        if (activated) return;
        activated = true;
        // enable barriers
        foreach (var b in barriersToEnable) if (b) b.SetActive(true);
        // wake boss
        if (boss)
        {
            boss.enabled = true;
            if (!boss.player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) boss.player = p.transform;
            }
        }
        // music
        if (music) music.PlayIntroThenPhase1();
        if (boss && boss.health && music && !subscribedPhase2)
        {
            boss.health.OnHalfHealth += music.PlayPhase2;
            subscribedPhase2 = true;
        }
        // UI: auto-assign if missing
        if (!bossUI)
        {
            bossUI = FindObjectOfType<BossHealthBar>(true);
        }
        if (bossUI)
        {
            if (!bossUI.bossHealth && boss) bossUI.bossHealth = boss.health;
            bossUI.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (subscribedPhase2 && boss && boss.health && music)
        {
            boss.health.OnHalfHealth -= music.PlayPhase2;
            subscribedPhase2 = false;
        }
    }
}
