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
    public bool SoundEffectsEnabled = false;

    [Header("Fishing result clips")]
    [Tooltip("물고기를 실제로 건져 올리는 순간 재생됩니다.")]
    public AudioClip FishCatchSfx;
    [Tooltip("처음 발견한 물고기의 NEW 표시와 함께 재생됩니다.")]
    public AudioClip NewFishSfx;

    [Header("World and fishing clips")]
    public AudioClip FootstepGrass;
    public AudioClip FishingCast;
    public AudioClip WaterSplash;
    public AudioClip FishBite;
    public AudioClip FishingReel;

    [Header("Economy and reward clips")]
    public AudioClip Sell;
    public AudioClip SellAll;
    public AudioClip Purchase;
    [Tooltip("빚 상환 전용 효과음입니다. 적절한 클립이 준비될 때까지 비워둡니다.")]
    public AudioClip DebtRepay;
    public AudioClip QuestComplete;

    [Header("UI clips")]
    public AudioClip UiClick;
    public AudioClip MenuOpen;
    public AudioClip MenuClose;

    [Header("Volume")]
    [Range(0f, 1f)] public float FishCatchVolume = .78f;
    [Range(0f, 1f)] public float NewFishVolume = .68f;
    [Range(0f, 1f)] public float BiteVolume = .82f;
    [Range(0f, 1f)] public float ActionVolume = .68f;
    [Range(0f, 1f)] public float RewardVolume = .72f;
    [Range(0f, 1f)] public float FootstepVolume = .72f;
    [Range(0f, 1f)] public float UiVolume = .50f;
    [Range(0f, 1f)] public float MenuVolume = .30f;
    [Min(0f)] public float NewFishDelay = .56f;
    [Min(0f)] public float QuestCompleteDelay = .72f;
}

/// <summary>Central replaceable SFX hooks. Missing clips stay silent; no synthetic fallback tones are generated.</summary>
public sealed class FishingAudioDirector : MonoBehaviour
{
    private AudioSource source;
    private FishingAudioSettings settings;
    private Coroutine catchSequence;
    private Coroutine questSequence;

    public FishingSurfaceType CurrentSurface { get; set; } = FishingSurfaceType.Grass;

    public void Setup(PixelCharacter playerCharacter, FishingAudioSettings audioSettings)
    {
        settings = audioSettings ?? new FishingAudioSettings();
        if (!settings.SoundEffectsEnabled) return;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
    }

    // Footstep and reel clips stay in the config for later replacement, but are intentionally
    // not played in the current build. Short one-shots sounded mechanical when repeated.
    public void PlayCast() => PlayPrimary(settings?.FishingCast, settings?.ActionVolume ?? 0f);
    public void PlaySplash() => PlayPrimary(settings?.WaterSplash, (settings?.ActionVolume ?? 0f) * .9f);
    public void PlayBite() => PlayPrimary(settings?.FishBite, settings?.BiteVolume ?? 0f);
    public void PlayReel() { }

    public void PlaySell(bool all)
    {
        if (settings == null) return;
        AudioClip clip = all && settings.SellAll != null ? settings.SellAll : settings.Sell;
        PlayPrimary(clip, settings.RewardVolume);
    }

    public void PlayPurchase() => PlayPrimary(settings?.Purchase, settings?.RewardVolume ?? 0f);
    public void PlayDebtRepay() => PlayPrimary(settings?.DebtRepay, settings?.RewardVolume ?? 0f);

    public void PlayQuestComplete() => PlayPrimary(settings?.QuestComplete, settings?.RewardVolume ?? 0f);

    public void PlayQuestCompleteDelayed(float delay = -1f)
    {
        if (!CanPlay || settings.QuestComplete == null) return;
        if (questSequence != null) StopCoroutine(questSequence);
        float resolvedDelay = delay >= 0f ? delay : settings.QuestCompleteDelay;
        questSequence = StartCoroutine(DelayedQuest(resolvedDelay));
    }

    public void PlayUiClick() => PlayUi(settings?.UiClick, settings?.UiVolume ?? 0f);
    public void PlayMenuOpen() => PlayUi(settings?.MenuOpen, settings?.MenuVolume ?? 0f);
    public void PlayMenuClose() => PlayUi(settings?.MenuClose, settings?.MenuVolume ?? 0f);

    public void PlayCatchSequence(bool isNewFish)
    {
        if (!CanPlay) return;
        if (catchSequence != null) StopCoroutine(catchSequence);
        catchSequence = StartCoroutine(CatchSequence(isNewFish));
    }

    private IEnumerator CatchSequence(bool isNewFish)
    {
        PlayPrimary(settings.FishCatchSfx, settings.FishCatchVolume);
        if (isNewFish)
        {
            // The result panel appears after its short reveal delay. Let the NEW badge become
            // visible first, then play the discovery cue without overlapping the catch sound.
            yield return new WaitForSecondsRealtime(settings.NewFishDelay);
            yield return WaitForCurrentSound();
            PlayPrimary(settings.NewFishSfx, settings.NewFishVolume);
        }
        catchSequence = null;
    }

    private IEnumerator DelayedQuest(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
        yield return WaitForCurrentSound();
        PlayQuestComplete();
        questSequence = null;
    }

    private IEnumerator WaitForCurrentSound()
    {
        while (CanPlay && source.isPlaying) yield return null;
    }

    private bool CanPlay => settings != null && settings.SoundEffectsEnabled && source != null;

    private void PlayPrimary(AudioClip clip, float volume)
    {
        if (!CanPlay || clip == null) return;
        // A single primary source enforces "one action = one important sound". A newer gameplay
        // action may replace an older cue, but important cues never stack into a noisy cluster.
        source.Stop();
        source.pitch = 1f;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void PlayUi(AudioClip clip, float volume)
    {
        if (!CanPlay || clip == null || source.isPlaying) return;
        source.pitch = 1f;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
