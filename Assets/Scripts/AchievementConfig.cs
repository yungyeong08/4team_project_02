using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AchievementConfig", menuName = "Fishing/Achievement Config")]
public sealed class AchievementConfig : ScriptableObject
{
    public List<AchievementDefinition> Achievements = new();
    [Min(.5f)] public float NotificationDuration = 2.8f;

    public void ApplyDefaults() => Achievements = AchievementDefaults.Create();

    public static AchievementConfig LoadOrCreateRuntimeDefault()
    {
        AchievementConfig config = Resources.Load<AchievementConfig>("Data/AchievementConfig");
        if (config != null && config.Achievements != null && config.Achievements.Count > 0) return config;
        config = CreateInstance<AchievementConfig>();
        config.ApplyDefaults();
        return config;
    }
}
