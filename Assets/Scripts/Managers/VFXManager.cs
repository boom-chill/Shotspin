using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VFXManager : MonoBehaviour
{
    [Header("Particle Prefabs")]
    public ParticleSystem gunSmokeEffect;
    public ParticleSystem cardGlowEffect;
    public ParticleSystem shellSparkleEffect;
    public ParticleSystem bloodEffect;
    public ParticleSystem healEffect;

    [Header("Effect Settings")]
    public float effectDuration = 2f;

    public static VFXManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayGunSmokeEffect(Vector3 position)
    {
        if (gunSmokeEffect != null)
        {
            PlayEffect(gunSmokeEffect, position);
        }
    }

    public void PlayCardGlowEffect(Vector3 position)
    {
        if (cardGlowEffect != null)
        {
            PlayEffect(cardGlowEffect, position);
        }
    }

    public void PlayShellSparkleEffect(Vector3 position)
    {
        if (shellSparkleEffect != null)
        {
            PlayEffect(shellSparkleEffect, position);
        }
    }

    public void PlayBloodEffect(Vector3 position)
    {
        if (bloodEffect != null)
        {
            PlayEffect(bloodEffect, position);
        }
    }

    public void PlayHealEffect(Vector3 position)
    {
        if (healEffect != null)
        {
            PlayEffect(healEffect, position);
        }
    }

    void PlayEffect(ParticleSystem effectPrefab, Vector3 position)
    {
        if (effectPrefab != null)
        {
            GameObject effectInstance = Instantiate(effectPrefab.gameObject, position, Quaternion.identity);
            ParticleSystem effect = effectInstance.GetComponent<ParticleSystem>();

            if (effect != null)
            {
                effect.Play();

                // Auto destroy after effect duration
                StartCoroutine(DestroyEffectAfterTime(effectInstance, effectDuration));
            }
        }
    }

    IEnumerator DestroyEffectAfterTime(GameObject effect, float time)
    {
        yield return new WaitForSeconds(time);

        if (effect != null)
        {
            Destroy(effect);
        }
    }

    // Screen shake effect
    public void ShakeCamera(float intensity = 0.5f, float duration = 0.3f)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            StartCoroutine(CameraShakeCoroutine(mainCamera, intensity, duration));
        }
    }

    IEnumerator CameraShakeCoroutine(Camera camera, float intensity, float duration)
    {
        Vector3 originalPosition = camera.transform.position;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float x = Random.Range(-intensity, intensity);
            float z = Random.Range(-intensity, intensity);

            camera.transform.position = originalPosition + new Vector3(x, 0, z);

            yield return null;
        }

        camera.transform.position = originalPosition;
    }
}