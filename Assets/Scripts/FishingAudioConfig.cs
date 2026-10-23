using UnityEngine;

/// <summary>Project-level SFX assignments kept outside the scene so audio can be swapped without touching gameplay code.</summary>
[CreateAssetMenu(fileName = "FishingAudioConfig", menuName = "Fishing/Audio Config")]
public sealed class FishingAudioConfig : ScriptableObject
{
    public FishingAudioSettings Settings = new();

    public static FishingAudioSettings LoadOrFallback(FishingAudioSettings fallback)
    {
        FishingAudioConfig config = Resources.Load<FishingAudioConfig>("Data/FishingAudioConfig");
        return config != null && config.Settings != null
            ? config.Settings
            : fallback ?? new FishingAudioSettings();
    }
}
