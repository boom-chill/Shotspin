using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Music Clips")]
    public AudioClip backgroundMusic;
    public AudioClip tensionMusic;
    public AudioClip victoryMusic;

    [Header("SFX Clips")]
    public AudioClip cardFlipSound;
    public AudioClip cardPlaySound;
    public AudioClip gunRotateSound;
    public AudioClip cylinderRotateSound;
    public AudioClip gunClickSound;
    public AudioClip gunShotSound;
    public AudioClip shellRewardSound;
    public AudioClip itemUseSound;
    public AudioClip shopBuySound;

    public static AudioManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        PlayBackgroundMusic();
    }

    public void PlayBackgroundMusic()
    {
        if (musicSource != null && backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    public void PlayTensionMusic()
    {
        if (musicSource != null && tensionMusic != null)
        {
            musicSource.clip = tensionMusic;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    // Convenience methods for common sounds
    public void PlayCardFlip() => PlaySFX(cardFlipSound);
    public void PlayCardPlay() => PlaySFX(cardPlaySound);
    public void PlayGunRotate() => PlaySFX(gunRotateSound);
    public void PlayCylinderRotate() => PlaySFX(cylinderRotateSound);
    public void PlayGunClick() => PlaySFX(gunClickSound);
    public void PlayGunShot() => PlaySFX(gunShotSound, 0.8f);
    public void PlayShellReward() => PlaySFX(shellRewardSound);
    public void PlayItemUse() => PlaySFX(itemUseSound);
    public void PlayShopBuy() => PlaySFX(shopBuySound);

    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            musicSource.volume = volume;
        }
    }

    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = volume;
        }
    }
}