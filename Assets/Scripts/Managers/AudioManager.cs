using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    public AudioSource bgmSource;
    public List<AudioSource> sfxSources = new List<AudioSource>();
    private float masterVolume = 1f, musicVolume = .65f, sfxVolume = .8f;
    private const int MaxVoices = 12;
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    private readonly List<AudioClip> generated = new List<AudioClip>();
    private bool settingsDirty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAudio()
    {
        if (Instance == null) new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("MasterVolume", 1f));
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", .65f));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", .8f));
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.spatialBlend = 0;
        UpdateVolumes();
    }

    public void SetMasterVolume(float value) { masterVolume = Mathf.Clamp01(value); Save("MasterVolume", masterVolume); }
    public void SetMusicVolume(float value) { musicVolume = Mathf.Clamp01(value); Save("MusicVolume", musicVolume); }
    public void SetSFXVolume(float value) { sfxVolume = Mathf.Clamp01(value); Save("SFXVolume", sfxVolume); }
    private void Save(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        settingsDirty = true;
        UpdateVolumes();
        CancelInvoke(nameof(FlushSettings));
        Invoke(nameof(FlushSettings), .5f);
    }
    private void FlushSettings() { if (settingsDirty) { PlayerPrefs.Save(); settingsDirty = false; } }
    private void OnApplicationPause(bool paused) { if (paused) FlushSettings(); }
    private void OnApplicationQuit() => FlushSettings();

    private void UpdateVolumes()
    {
        if (bgmSource != null) bgmSource.volume = masterVolume * musicVolume;
        foreach (var source in sfxSources)
            if (source != null) source.volume = masterVolume * sfxVolume;
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) { bgmSource.Stop(); bgmSource.clip = null; return; }
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || masterVolume * sfxVolume <= 0) return;
        sfxSources.RemoveAll(source => source == null);
        AudioSource voice = sfxSources.Find(source => !source.isPlaying);
        if (voice == null)
        {
            if (sfxSources.Count >= MaxVoices) return;
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
            sfxSources.Add(voice);
        }
        voice.volume = masterVolume * sfxVolume;
        voice.clip = clip;
        voice.Play();
    }

    public void PlayBGM(string name)
    {
        if (name == "BGM_Battle") name += "_" + GameplayManager.GetCurrentMapIndex();
        AudioClip clip = Load(name, false);
        PlayBGM(clip); // Missing battle music must not leave lobby music playing.
    }

    public void PlaySFX(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (lastPlayed.TryGetValue(name, out float last) && Time.unscaledTime - last < .055f) return;
        lastPlayed[name] = Time.unscaledTime;
        PlaySFX(Load(name, true));
    }

    private AudioClip Load(string name, bool allowPlaceholder)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (clips.TryGetValue(name, out AudioClip cached)) return cached;
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null && name.StartsWith("BGM_")) clip = CreateMusic(name);
        if (clip == null && allowPlaceholder) clip = Placeholder(name);
        clips[name] = clip;
        if (clip == null) Debug.LogWarning("Audio asset missing: Assets/Resources/Audio/" + name);
        return clip;
    }

    private AudioClip CreateMusic(string name)
    {
        if (name != "BGM_Lobby" && name != "BGM_Battle_0" && name != "BGM_Battle_1" && name != "BGM_Battle_2") return null;
        bool lobby = name == "BGM_Lobby";
        int theme = name.EndsWith("_1") ? 1 : name.EndsWith("_2") ? 2 : 0;
        const int rate = 22050;
        float beat = 60f / (lobby ? 90f : theme == 2 ? 120f : 108f);
        int count = Mathf.RoundToInt(beat * 32 * rate);
        float[] samples = new float[count];
        int[] roots = lobby ? new[] { 45, 41, 48, 43 } : theme == 1 ? new[] { 50, 46, 53, 48 } : new[] { 40, 36, 43, 38 };
        int[] notes = { 0, 7, 12, 3, 7, 15, 12, 7 };
        for (int i = 0; i < count; i++)
        {
            float seconds = (float)i / rate;
            float beats = (float)i / count * 32;
            int bar = Mathf.Min(3, (int)(beats / 8));
            float chordTime = (beats % 8) * beat;
            float chordEnvelope = Mathf.Sin(Mathf.PI * (beats % 8) / 8);
            float root = 440f * Mathf.Pow(2, (roots[bar] - 69) / 12f);
            float pad = (Mathf.Sin(2 * Mathf.PI * root * chordTime)
                + .5f * Mathf.Sin(2 * Mathf.PI * root * Mathf.Pow(2, 7f / 12) * chordTime)
                + .35f * Mathf.Sin(2 * Mathf.PI * root * Mathf.Pow(2, 3f / 12) * chordTime)) * chordEnvelope * .065f;
            int step = (int)(beats * 2);
            float noteTime = (beats * 2 % 1) * beat * .5f;
            float frequency = root * Mathf.Pow(2, (notes[step % 8] + 12) / 12f);
            float pluck = Mathf.Sin(2 * Mathf.PI * frequency * noteTime) * Mathf.Exp(-noteTime * 18)
                * Mathf.Min(1, noteTime * 500) * (lobby ? .035f : .055f);
            float kickTime = beats % 1 * beat;
            float kick = lobby ? 0 : Mathf.Sin(2 * Mathf.PI * (48 * kickTime + 1.7f * (1 - Mathf.Exp(-kickTime * 28))))
                * Mathf.Exp(-kickTime * 20) * .11f;
            float edge = Mathf.Min(1, Mathf.Min(seconds, (count - 1 - i) / (float)rate) * 30);
            samples[i] = Mathf.Clamp((pad + pluck + kick) * edge, -.4f, .4f);
        }
        var clip = AudioClip.Create(name + "_SynthLoop", count, 1, rate, false);
        clip.SetData(samples, 0);
        generated.Add(clip);
        return clip;
    }

    // Synthesized cues; imported assets with matching names always take precedence.
    private AudioClip Placeholder(string name)
    {
        float duration, from, to, noise;
        switch (name)
        {
            case "SFX_Laser": duration = .13f; from = 1400; to = 220; noise = .06f; break;
            case "SFX_Hit": duration = .09f; from = 550; to = 130; noise = .45f; break;
            case "SFX_HitMiss": duration = .08f; from = 260; to = 90; noise = .65f; break;
            case "SFX_ShieldHit": duration = .18f; from = 900; to = 480; noise = .08f; break;
            case "SFX_ShieldBreak": duration = .3f; from = 800; to = 100; noise = .3f; break;
            case "SFX_Stun": duration = .22f; from = 350; to = 1200; noise = .2f; break;
            case "SFX_Explosion": duration = .5f; from = 140; to = 35; noise = .8f; break;
            case "SFX_Click": duration = .055f; from = 720; to = 1000; noise = 0; break;
            default: return null;
        }
        const int rate = 22050;
        float[] samples = new float[Mathf.CeilToInt(duration * rate)];
        var random = new System.Random(17);
        float phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / samples.Length;
            phase += Mathf.Lerp(from, to, t) * Mathf.PI * 2 / rate;
            float envelope = Mathf.Min(1, i / (rate * .005f)) * Mathf.Pow(1 - t, 2);
            samples[i] = .22f * envelope * Mathf.Lerp(Mathf.Sin(phase), (float)random.NextDouble() * 2 - 1, noise);
        }
        var clip = AudioClip.Create("Temporary_" + name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        generated.Add(clip);
        return clip;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        FlushSettings();
        foreach (var clip in generated) if (clip != null) Destroy(clip);
        Instance = null;
    }
}
