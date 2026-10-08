// ไฟล์ AudioManager.cs — ตัวจัดการเสียงทั้งเกม (เพลงพื้นหลัง BGM + เสียงเอฟเฟกต์ SFX) แบบ Singleton
// สร้างตัวเองอัตโนมัติหลังโหลด Scene แรก และอยู่ข้าม Scene (DontDestroyOnLoad) จึงไม่ต้องวางใน Scene
// ระบบอื่นเรียกผ่าน AudioManager.Instance.PlayBGM("ชื่อ") / PlaySFX("ชื่อ") โดยโหลดไฟล์จาก Resources/Audio/
// ถ้าไม่มีไฟล์เสียง จะสังเคราะห์เสียงชั่วคราวขึ้นมาเอง; ระดับเสียงบันทึกใน PlayerPrefs (หน้าตั้งค่า)
// เสียงชุดใหม่ (FeatureFlags.NewSounds): ไฟล์อยู่ที่ Resources/Audio/BOS/ (SFX_*.wav, BGM_*.ogg)
//   ลำดับการหาไฟล์: Resources/Audio/<ชื่อ> (วางไฟล์ชื่อเดียวกันตรงนี้เพื่อทับเสียงเองได้) -> Resources/Audio/BOS/<ชื่อ> -> เสียงสังเคราะห์เดิม
//   เสียงที่เพิ่งมีชื่อของตัวเอง (วาร์ป, เก็บพาวเวอร์อัป, ฮีล ฯลฯ) ถ้าปิดสวิตช์/ไม่มีไฟล์ จะเล่นเสียงเดิมที่เคยใช้ (LegacyNames)
// เพลงต่อสู้เลือกตามแม็พผ่าน GameplayManager.GetCurrentMapIndex()
using UnityEngine;
using System.Collections.Generic;

// จุดรวมเสียงเพลงและเสียงเอฟเฟกต์: จัดการระดับเสียง แคชเสียง และเรียกเล่นจากระบบเกม
public class AudioManager : MonoBehaviour
{
    // ตัวแปร Singleton ให้สคริปต์อื่นเข้าถึงได้จากทุกที่
    public static AudioManager Instance;
    // AudioSource สำหรับเล่นเพลงพื้นหลัง (วนซ้ำ)
    public AudioSource bgmSource;
    // กลุ่ม AudioSource สำหรับเสียงเอฟเฟกต์ เล่นซ้อนกันได้หลายเสียง
    public List<AudioSource> sfxSources = new List<AudioSource>();
    // ระดับเสียง 0..1: รวม (master), เพลง (ค่าเริ่ม 0.65), เอฟเฟกต์ (ค่าเริ่ม 0.8) เสียงจริง = master * ประเภท
    private float masterVolume = 1f, musicVolume = .65f, sfxVolume = .8f;
    // จำนวน AudioSource ของ SFX สูงสุดที่เล่นพร้อมกันได้ (เกินนี้เสียงใหม่จะถูกข้าม)
    private const int MaxVoices = 12;
    // แคช AudioClip ตามชื่อ จะได้ไม่ต้อง Resources.Load ซ้ำ
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    // เวลาที่เล่นเสียงแต่ละชื่อครั้งล่าสุด ใช้กันเสียงเดียวกันเล่นถี่เกินไป
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    // คลิปที่สังเคราะห์ขึ้นเอง เก็บไว้เพื่อ Destroy ตอนปิด ไม่ให้หน่วยความจำรั่ว
    private readonly List<AudioClip> generated = new List<AudioClip>();
    // true = มีการเปลี่ยนค่าระดับเสียงที่ยังไม่ได้ PlayerPrefs.Save()
    private bool settingsDirty;
    // โฟลเดอร์เสียงชุดใหม่ใน Resources
    private const string PackFolder = "Audio/BOS/";
    // ชื่อเสียงชุดใหม่ -> เสียงเดิมที่จุดนั้นเคยใช้ (ใช้เมื่อปิด NewSounds หรือไม่มีไฟล์ชุดใหม่ ทำให้ทุกอย่างเหมือนเดิม)
    private static readonly Dictionary<string, string> LegacyNames = new Dictionary<string, string>
    {
        { "SFX_Bounce", "SFX_HitMiss" }, { "SFX_Warp", "SFX_ShieldHit" }, { "SFX_PowerUp", "SFX_ShieldHit" },
        { "SFX_Star", "SFX_ShieldHit" }, { "SFX_Heal", "SFX_ShieldHit" }, { "SFX_Nova", "SFX_Explosion" },
        { "SFX_Meteor", "SFX_Explosion" }, { "SFX_MissileHit", "SFX_Hit" }, { "SFX_Emote", "SFX_Click" },
        { "SFX_Celebrate", "SFX_ShieldBreak" }, { "SFX_CrateTick", "SFX_Click" }, { "SFX_CrateOpen", "SFX_ShieldBreak" },
        { "SFX_CrateEpic", "SFX_Explosion" },
    };
    // เสียงที่เล่นถี่ (ยิง/โดน/ระเบิด): ชุดใหม่สุ่มระดับเสียง ±5% ทุกครั้ง ฟังไม่ซ้ำซาก
    private static readonly HashSet<string> VariedPitch = new HashSet<string>
        { "SFX_Laser", "SFX_Hit", "SFX_HitMiss", "SFX_Bounce", "SFX_ShieldHit", "SFX_Explosion", "SFX_MissileHit", "SFX_Meteor" };

    // Unity เรียกอัตโนมัติหลังโหลด Scene แรก: ถ้ายังไม่มี AudioManager ให้สร้าง GameObject ใหม่พร้อมคอมโพเนนต์นี้
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAudio()
    {
        if (Instance == null) new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    // ตั้งค่า Singleton (ถ้ามีตัวซ้ำให้ทำลายทิ้ง), ให้อยู่ข้าม Scene, โหลดระดับเสียงจาก PlayerPrefs
    // และเตรียม AudioSource ของ BGM ให้เล่นวนแบบเสียง 2D
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // ไม่มี AudioListener ในฉากใดเลย = ใน Unity Editor ไม่มีเสียง: สร้างตัวรับเสียงบน AudioManager (อยู่ข้ามฉาก) ถ้ายังไม่มีตัวอื่น
        if (FeatureFlags.AudioListenerGuard && UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("MasterVolume", 1f));
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", .65f));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", .8f));
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.spatialBlend = 0;
        UpdateVolumes();
    }

    // Setter ระดับเสียง เรียกจาก Slider ในหน้าตั้งค่า: บีบค่าให้อยู่ 0..1 แล้วบันทึก
    public void SetMasterVolume(float value) { masterVolume = Mathf.Clamp01(value); Save("MasterVolume", masterVolume); }
    // ตั้งระดับเสียงเพลงพื้นหลัง (BGM) 0..1 แล้วบันทึกคีย์ "MusicVolume"
    public void SetMusicVolume(float value) { musicVolume = Mathf.Clamp01(value); Save("MusicVolume", musicVolume); }
    // ตั้งระดับเสียงเอฟเฟกต์ (SFX) 0..1 แล้วบันทึกคีย์ "SFXVolume"
    public void SetSFXVolume(float value) { sfxVolume = Mathf.Clamp01(value); Save("SFXVolume", sfxVolume); }
    // บันทึกค่าลง PlayerPrefs, อัปเดตเสียงทันที แล้วหน่วง 0.5 วิค่อย Save ลงดิสก์ (กันเซฟถี่ตอนลาก Slider)
    private void Save(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        settingsDirty = true;
        UpdateVolumes();
        CancelInvoke(nameof(FlushSettings));
        Invoke(nameof(FlushSettings), .5f);
    }
    // เขียน PlayerPrefs ลงดิสก์จริงเมื่อมีค่าค้าง; ถูกเรียกตอนครบเวลาหน่วง, ตอนแอปถูกพัก (มือถือ) และตอนปิดเกม
    private void FlushSettings() { if (settingsDirty) { PlayerPrefs.Save(); settingsDirty = false; } }
    // Unity เรียกเมื่อแอปถูกพัก (สลับแอปบนมือถือ): เซฟค่าระดับเสียงที่ค้างอยู่ทันที
    private void OnApplicationPause(bool paused) { if (paused) FlushSettings(); }
    // Unity เรียกตอนปิดเกม: เซฟค่าระดับเสียงที่ค้างอยู่ก่อนออก
    private void OnApplicationQuit() => FlushSettings();

    // นำระดับเสียงปัจจุบันไปใส่ให้ AudioSource ของ BGM และ SFX ทุกตัว
    private void UpdateVolumes()
    {
        if (bgmSource != null) bgmSource.volume = masterVolume * musicVolume;
        foreach (var source in sfxSources)
            if (source != null) source.volume = masterVolume * sfxVolume;
    }

    // เล่นเพลงพื้นหลังจาก AudioClip: ถ้า null ให้หยุดเพลง, ถ้าเป็นเพลงเดิมที่กำลังเล่นอยู่ไม่ต้องเริ่มใหม่
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) { bgmSource.Stop(); bgmSource.clip = null; return; }
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    // เล่นเสียงเอฟเฟกต์: หา AudioSource ที่ว่าง ถ้าไม่มีให้เพิ่มใหม่ (ไม่เกิน MaxVoices) แล้วเล่นคลิป (pitch 1 = ปกติ)
    public void PlaySFX(AudioClip clip, float pitch = 1f)
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
        voice.pitch = pitch;
        voice.clip = clip;
        voice.Play();
    }

    // เล่น BGM ตามชื่อ ถ้าเป็น "BGM_Battle" จะต่อท้ายด้วยเลขแม็พ (เช่น BGM_Battle_2 = แม็พหุ่นยนต์)
    public void PlayBGM(string name)
    {
        int map = GameplayManager.GetCurrentMapIndex();
        if (name == "BGM_Battle") name += "_" + map;
        AudioClip clip = Load(name, false);
        // แม็พ 3/4 ไม่มีไฟล์เพลงของตัวเอง (ปิด NewSounds): สถานี = เพลงแม็พปริซึม, ลาวา = เพลงแม็พหุ่นยนต์
        if (clip == null && name.StartsWith("BGM_Battle_") && map >= 3) clip = Load("BGM_Battle_" + (map == 3 ? 1 : 2), false);
        PlayBGM(clip); // Missing battle music must not leave lobby music playing.
    }

    // เล่น SFX ตามชื่อ (เรียกจากระบบยิง/โดนยิง/ปุ่ม ฯลฯ) ถ้าชื่อเดียวกันเพิ่งเล่นไปไม่ถึง 0.055 วิ จะข้ามไม่เล่นซ้ำ
    public void PlaySFX(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        // ปิดเสียงชุดใหม่: ชื่อใหม่กลับไปใช้ชื่อเสียงเดิม (นับเวลากันเล่นถี่ร่วมกันแบบเดิมด้วย)
        if (!FeatureFlags.NewSounds && LegacyNames.TryGetValue(name, out string legacy)) name = legacy;
        if (lastPlayed.TryGetValue(name, out float last) && Time.unscaledTime - last < .055f) return;
        lastPlayed[name] = Time.unscaledTime;
        AudioClip clip = Load(name, !LegacyNames.ContainsKey(name));
        if (clip == null && LegacyNames.TryGetValue(name, out legacy)) clip = Load(legacy, true);
        float pitch = FeatureFlags.NewSounds && VariedPitch.Contains(name) ? Random.Range(.95f, 1.05f) : 1f;
        PlaySFX(clip, pitch);
    }

    // โหลดคลิปตามชื่อ: ดูแคชก่อน -> Resources/Audio/<name> -> เสียงชุดใหม่ Resources/Audio/BOS/<name> (เปิด NewSounds)
    // -> ถ้าไม่มีและเป็น BGM ให้สังเคราะห์เพลง -> ถ้าเป็น SFX (allowPlaceholder) ให้สังเคราะห์เสียงชั่วคราว แล้วเก็บผลลงแคช (รวมกรณี null)
    private AudioClip Load(string name, bool allowPlaceholder)
    {
        if (string.IsNullOrEmpty(name)) return null;
        // แคชแยกตามสวิตช์ (สลับสวิตช์ระหว่างเล่นแล้วได้เสียงตามสวิตช์ทันที)
        string key = (FeatureFlags.NewSounds ? "new:" : "old:") + name;
        if (clips.TryGetValue(key, out AudioClip cached)) return cached;
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null && FeatureFlags.NewSounds) clip = Resources.Load<AudioClip>(PackFolder + name);
        if (clip == null && name.StartsWith("BGM_")) clip = CreateMusic(name);
        if (clip == null && allowPlaceholder) clip = Placeholder(name);
        clips[key] = clip;
        if (clip == null) Debug.LogWarning("Audio asset missing: Assets/Resources/Audio/" + name);
        return clip;
    }

    // สังเคราะห์เพลงวนซ้ำ (32 จังหวะ, mono 22050 Hz) สำหรับ BGM_Lobby และ BGM_Battle_0/1/2 เมื่อไม่มีไฟล์เพลงจริง
    // แต่ละธีมต่างกันที่ tempo (BPM) และโน้ตราก; เพลง Lobby ไม่มีเสียงกลอง (kick)
    private AudioClip CreateMusic(string name)
    {
        if (name != "BGM_Lobby" && name != "BGM_Battle_0" && name != "BGM_Battle_1" && name != "BGM_Battle_2") return null;
        bool lobby = name == "BGM_Lobby";
        int theme = name.EndsWith("_1") ? 1 : name.EndsWith("_2") ? 2 : 0;
        // 1) ตั้งค่าพื้นฐาน: sample rate, ความยาว 1 จังหวะ (วินาที) ตาม BPM, จำนวน sample ทั้งหมด
        const int rate = 22050;
        float beat = 60f / (lobby ? 90f : theme == 2 ? 120f : 108f);
        int count = Mathf.RoundToInt(beat * 32 * rate);
        float[] samples = new float[count];
        // โน้ตราก (MIDI) ของคอร์ด 4 ห้อง และรูปแบบโน้ต arpeggio (ระยะครึ่งเสียง)
        int[] roots = lobby ? new[] { 45, 41, 48, 43 } : theme == 1 ? new[] { 50, 46, 53, 48 } : new[] { 40, 36, 43, 38 };
        int[] notes = { 0, 7, 12, 3, 7, 15, 12, 7 };
        // 2) สร้างแต่ละ sample = pad (คอร์ด) + pluck (โน้ตดีด) + kick (กลอง) แล้ว fade หัวท้ายกันเสียงคลิก
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
        // 3) สร้าง AudioClip, ปรับความดังให้พอดี แล้วเก็บไว้ในรายการคลิปที่สังเคราะห์
        var clip = AudioClip.Create(name + "_SynthLoop", count, 1, rate, false);
        NormalizeGeneratedAudio(samples, .6f, 3f);
        clip.SetData(samples, 0);
        generated.Add(clip);
        return clip;
    }

    // Synthesized cues; imported assets with matching names always take precedence.
    private AudioClip Placeholder(string name)
    {
        // แต่ละเสียงกำหนด: ความยาว (วินาที), ความถี่เริ่ม->จบ (Hz), สัดส่วน noise (0 = เสียงใส, 1 = noise ล้วน)
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
        // สร้าง sample: ไล่ความถี่จาก from ไป to, envelope ค่อย ๆ เบาลง และผสม noise ตามสัดส่วน
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
        NormalizeGeneratedAudio(samples, name == "SFX_Click" ? .35f : .55f, 3f);
        clip.SetData(samples, 0);
        generated.Add(clip);
        return clip;
    }

    // ปรับความดังของเสียงสังเคราะห์ให้ยอดคลื่นเท่ากับ targetPeak แต่ขยายไม่เกิน maximumGain เท่า
    private static void NormalizeGeneratedAudio(float[] samples, float targetPeak, float maximumGain)
    {
        float peak = 0;
        foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
        if (peak < .0001f) return;
        float gain = Mathf.Min(maximumGain, targetPeak / peak);
        for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
    }

    // Share headroom during busy firefights instead of letting many louder effects pile up.
    // ทุกเฟรม (หลัง Update) ลดเสียง SFX ตามจำนวนเสียงที่เล่นพร้อมกัน (หารด้วยรากที่สอง) กันเสียงแตก
    private void LateUpdate()
    {
        int voices = 0;
        foreach (var source in sfxSources) if (source != null && source.isPlaying) voices++;
        float mix = 1f / Mathf.Sqrt(Mathf.Max(1, voices));
        foreach (var source in sfxSources)
            if (source != null) source.volume = masterVolume * sfxVolume * mix;
    }

    // ตอนถูกทำลาย: บันทึกค่าที่ค้าง, ทำลายคลิปสังเคราะห์ทั้งหมด แล้วล้าง Instance
    private void OnDestroy()
    {
        if (Instance != this) return;
        FlushSettings();
        foreach (var clip in generated) if (clip != null) Destroy(clip);
        Instance = null;
    }
}
