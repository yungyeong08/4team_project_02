using System.Collections;
using UnityEngine;

public enum FishingSurfaceType
{
    Grass,
    Dirt,
    Wood,
    WaterEdge
}

[System.Serializable]
public sealed class FishingAudioSettings
{
    [Header("Playback")]
    [Tooltip("효과음을 다시 사용할 때만 켭니다.")]
    public bool SoundEffectsEnabled = false;

    [Header("Optional replacement clips")]
    public AudioClip FootstepGrass;
    public AudioClip FishingCast;
    public AudioClip WaterSplash;
    public AudioClip FishBite;
    public AudioClip FishingReel;
    public AudioClip FishingSuccess;
    public AudioClip FishCatch;
    public AudioClip Sell;
    public AudioClip SellAll;
    public AudioClip QuestComplete;
    public AudioClip UiClick;

    [Header("Volume priority")]
    [Range(0f, 1f)] public float BiteVolume = .82f;
    [Range(0f, 1f)] public float SuccessVolume = .82f;
    [Range(0f, 1f)] public float ActionVolume = .68f;
    [Range(0f, 1f)] public float RewardVolume = .72f;
    [Range(0f, 1f)] public float FootstepVolume = .72f;
    [Range(0f, 1f)] public float UiVolume = .50f;
}

/// <summary>
/// Centralized replaceable audio cues. When no clip is assigned, short procedural placeholders
/// are generated in memory, so no external audio license is required.
/// </summary>
public sealed class FishingAudioDirector : MonoBehaviour
{
    private AudioSource source;
    private PixelCharacter player;
    private FishingAudioSettings settings;
    private float footstepTimer;

    private AudioClip fallbackFootstep;
    private AudioClip fallbackCast;
    private AudioClip fallbackSplash;
    private AudioClip fallbackBite;
    private AudioClip fallbackSuccess;
    private AudioClip fallbackCatch;
    private AudioClip fallbackSell;
    private AudioClip fallbackSellAll;
    private AudioClip fallbackQuest;
    private AudioClip fallbackUi;

    public FishingSurfaceType CurrentSurface { get; set; } = FishingSurfaceType.Grass;

    public void Setup(PixelCharacter playerCharacter, FishingAudioSettings audioSettings)
    {
        player = playerCharacter;
        settings = audioSettings ?? new FishingAudioSettings();
        if (!settings.SoundEffectsEnabled) return;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
        source.mute = false;
        LoadBundledClips();
        BuildFallbacks();
    }

    private void Update()
    {
        if (settings == null || !settings.SoundEffectsEnabled) return;

        if (player == null || !player.IsMoving || player.IsFishing)
        {
            footstepTimer = 0f;
            return;
        }

        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f) return;
        if (CurrentSurface == FishingSurfaceType.Grass)
        {
            LoadBundledClips();
            Play(settings.FootstepGrass, fallbackFootstep, settings.FootstepVolume, Random.Range(.94f, 1.06f));
        }
        footstepTimer = player.IsRunning ? .28f : .43f;
    }

    public void PlayCast()
    {
        LoadBundledClips();
        Play(settings.FishingCast, fallbackCast, settings.ActionVolume);
    }

    public void PlaySplash()
    {
        LoadBundledClips();
        Play(settings.WaterSplash, fallbackSplash, settings.ActionVolume * .9f);
    }

    public void PlayBite()
    {
        LoadBundledClips();
        Play(settings.FishBite, fallbackBite, settings.BiteVolume);
    }

    public void PlayReel()
    {
        LoadBundledClips();
        Play(settings.FishingReel, fallbackUi, settings.ActionVolume * .42f, Random.Range(.96f, 1.04f));
    }

    public void PlaySell(bool all)
    {
        LoadBundledClips();
        Play(all ? settings.SellAll : settings.Sell, all ? fallbackSellAll : fallbackSell,
            settings.RewardVolume, all ? .94f : Random.Range(.98f, 1.04f));
    }

    public void PlayQuestComplete()
    {
        LoadBundledClips();
        Play(settings.QuestComplete, fallbackQuest, settings.RewardVolume);
    }

    public void PlayQuestCompleteDelayed(float delay = .55f)
    {
        if (settings == null || !settings.SoundEffectsEnabled) return;
        StartCoroutine(DelayedQuest(delay));
    }

    public void PlayUiClick()
    {
        LoadBundledClips();
        Play(settings.UiClick, fallbackUi, settings.UiVolume);
    }

    public void PlayCatchSequence()
    {
        if (settings == null || !settings.SoundEffectsEnabled) return;
        StopCoroutine(nameof(CatchSequence));
        StartCoroutine(nameof(CatchSequence));
    }

    private IEnumerator CatchSequence()
    {
        LoadBundledClips();
        Play(settings.FishingSuccess, fallbackSuccess, settings.SuccessVolume);
        yield return new WaitForSecondsRealtime(.13f);
        Play(settings.FishCatch, fallbackCatch, settings.ActionVolume);
    }

    private IEnumerator DelayedQuest(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
        PlayQuestComplete();
    }

    private void Play(AudioClip assigned, AudioClip fallback, float volume, float pitch = 1f)
    {
        if (settings == null || !settings.SoundEffectsEnabled || source == null) return;
        AudioClip clip = assigned != null ? assigned : fallback;
        if (clip == null) return;
        source.pitch = pitch;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
        source.pitch = 1f;
    }

    private void BuildFallbacks()
    {
        fallbackFootstep = CreateNoise("Temp Grass Footstep", .08f, .20f, 520f);
        fallbackCast = CreateSweep("Temp Fishing Cast", .18f, 1100f, 260f, .22f);
        fallbackSplash = CreateNoise("Temp Water Splash", .20f, .32f, 900f);
        fallbackBite = CreateTone("Temp Fish Bite", .16f, 740f, .32f, 8f);
        fallbackSuccess = CreateTone("Temp Fishing Success", .28f, 620f, .28f, 5f);
        fallbackCatch = CreateNoise("Temp Fish Catch", .24f, .30f, 1250f);
        fallbackSell = CreateNoise("Temp Sell", .16f, .20f, 2400f);
        fallbackSellAll = CreateNoise("Temp Sell All", .24f, .23f, 2100f);
        fallbackQuest = CreateTone("Temp Quest Complete", .32f, 520f, .25f, 4f);
        fallbackUi = CreateNoise("Temp UI Click", .045f, .12f, 1800f);
    }

    private void LoadBundledClips()
    {
        settings.FishingCast ??= Resources.Load<AudioClip>("Audio/Fishing/fishing-snap");
        settings.WaterSplash ??= Resources.Load<AudioClip>("Audio/Fishing/fishing-splash");
        settings.FishBite ??= Resources.Load<AudioClip>("Audio/Fishing/fishing-plop");
        settings.FishingReel ??= Resources.Load<AudioClip>("Audio/Fishing/fishing-reel");
        settings.FishingSuccess ??= Resources.Load<AudioClip>("Audio/Fishing/fishing-catch");
        settings.FishCatch ??= Resources.Load<AudioClip>("Audio/Fishing/fishing-splash");
        settings.FootstepGrass ??= Resources.Load<AudioClip>("Audio/Game/footstep-grass");
        settings.Sell ??= Resources.Load<AudioClip>("Audio/Game/coin-sell");
        settings.SellAll ??= Resources.Load<AudioClip>("Audio/Game/coin-sell");
        settings.QuestComplete ??= Resources.Load<AudioClip>("Audio/Game/quest-complete");
        settings.UiClick ??= Resources.Load<AudioClip>("Audio/Game/ui-click");
    }

    private static AudioClip CreateTone(string name, float duration, float frequency, float gain, float decay)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-decay * t) * Mathf.Min(1f, t * 80f);
            samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * gain;
        }
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateSweep(string name, float duration, float fromFrequency, float toFrequency, float gain)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        var samples = new float[sampleCount];
        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = i / (float)Mathf.Max(1, sampleCount - 1);
            float frequency = Mathf.Lerp(fromFrequency, toFrequency, normalized);
            phase += frequency * Mathf.PI * 2f / sampleRate;
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            samples[i] = Mathf.Sin(phase) * envelope * gain;
        }
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateNoise(string name, float duration, float gain, float smoothing)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        var samples = new float[sampleCount];
        float filtered = 0f;
        float blend = 1f - Mathf.Exp(-Mathf.PI * 2f * Mathf.Max(20f, smoothing) / sampleRate);
        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = i / (float)Mathf.Max(1, sampleCount - 1);
            filtered = Mathf.Lerp(filtered, Random.Range(-1f, 1f), blend);
            float envelope = Mathf.Sin(normalized * Mathf.PI) * (1f - normalized * .35f);
            samples[i] = filtered * envelope * gain;
        }
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
