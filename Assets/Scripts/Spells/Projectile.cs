using UnityEngine;

using UnityEngine;

public class Projectile : MonoBehaviour
{
    // Properti yang diatur oleh WizardSimpleController
    public float speed = 20f;
    public float lifeTime = 3f;
    public GameObject hitEffect;
    public AudioClip hitSound;
    public float hitEffectScale = 1f; // Skala untuk efek ledakan

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Hindari tabrakan dengan Player atau trigger lain
        if (other.CompareTag("Player") || other.isTrigger)
        {
            return;
        }

        // Munculkan efek visual saat tabrakan
        if (hitEffect != null)
        {
            GameObject effectGO = Instantiate(hitEffect, transform.position, Quaternion.identity);
            effectGO.transform.localScale *= hitEffectScale;
        }

        // Mainkan suara saat tabrakan
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        // Hancurkan proyektil
        Destroy(gameObject);
    }
}
