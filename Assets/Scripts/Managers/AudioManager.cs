using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioSource bgmSource;
    public List<AudioSource> sfxSources = new List<AudioSource>();

    private float masterVolume = 1f;
    private float musicVolume = 1f;
    private float sfxVolume = 1f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

        UpdateVolumes();
    }

    public void SetMasterVolume(float vol)
    {
        masterVolume = vol;
        PlayerPrefs.SetFloat("MasterVolume", vol);
        UpdateVolumes();
    }

    public void SetMusicVolume(float vol)
    {
        musicVolume = vol;
        PlayerPrefs.SetFloat("MusicVolume", vol);
        UpdateVolumes();
    }

    public void SetSFXVolume(float vol)
    {
        sfxVolume = vol;
        PlayerPrefs.SetFloat("SFXVolume", vol);
        UpdateVolumes();
    }

    private void UpdateVolumes()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = musicVolume * masterVolume;
        }

        foreach (var sfx in sfxSources)
        {
            if (sfx != null)
                sfx.volume = sfxVolume * masterVolume;
        }
    }

    public void PlayBGM(AudioClip clip)
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
        }

        if (bgmSource.clip == clip) return; // Already playing

        bgmSource.clip = clip;
        bgmSource.volume = musicVolume * masterVolume;
        bgmSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = sfxVolume * masterVolume;
        source.Play();

        sfxSources.Add(source);
        Destroy(source, clip.length);
        sfxSources.RemoveAll(s => s == null);
    }

    public void PlayBGM(string clipName)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + clipName);
        if (clip != null) PlayBGM(clip);
        else Debug.LogWarning("BGM not found: " + clipName);
    }

    public void PlaySFX(string clipName)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + clipName);
        if (clip != null) PlaySFX(clip);
        // ไม่ log warning เพื่อป้องกันสแปมถ้ายังไม่ได้ใส่ไฟล์เสียง
    }
}
