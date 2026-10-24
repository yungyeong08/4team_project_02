using UnityEngine;

/// <summary>Replaceable character art references used by the runtime-created player.</summary>
[CreateAssetMenu(fileName = "PixelCharacterVisualConfig", menuName = "Fishing/Pixel Character Visual Config")]
public sealed class PixelCharacterVisualConfig : ScriptableObject
{
    [Header("Horizontal walk cycle")]
    public Sprite[] WalkLeft = new Sprite[3];
    public Sprite[] WalkRight = new Sprite[3];

    [Header("Fishing pose")]
    public Sprite FishingPose;

    public static PixelCharacterVisualConfig Load()
    {
        return Resources.Load<PixelCharacterVisualConfig>("Data/PixelCharacterVisualConfig");
    }
}
