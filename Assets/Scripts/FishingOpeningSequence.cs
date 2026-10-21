using System.Collections;
using UnityEngine;

[System.Serializable]
public sealed class FishingOpeningSettings
{
    [Range(1f, 3f)] public float FirstLineSeconds = 1.7f;
    [Range(1f, 3f)] public float SecondLineSeconds = 2.1f;
    [Range(1f, 3f)] public float ThirdLineSeconds = 1.7f;
    [Range(.2f, 1.2f)] public float FadeSeconds = .65f;
    [Range(.4f, 2f)] public float CameraPanSeconds = 1.05f;
    [Range(.1f, 1f)] public float PlayerRevealDelay = .45f;
    [Range(1f, 2f)] public float RegionTitleSeconds = 1.5f;
}

/// <summary>Short new-game story and first-pond camera reveal using the existing map and character.</summary>
public sealed class FishingOpeningSequence : MonoBehaviour
{
    private EarlyGameQuestSystem progression;
    private GameSaveSystem saveSystem;
    private PixelCharacter player;
    private Camera gameCamera;
    private CameraFollow2D cameraFollow;
    private FishingSpotDefinition firstLake;
    private FishingOpeningSettings settings;
    private Coroutine sequence;
    private string storyText = "";
    private float blackAlpha;
    private bool isBlocking;
    private bool showRegionTitle;
    private float regionTitleElapsed;

    public bool IsBlocking => isBlocking;

    public void Setup(EarlyGameQuestSystem questSystem, GameSaveSystem gameSave, PixelCharacter playerCharacter,
        Camera cameraComponent, CameraFollow2D follow, FishingSpotDefinition lake, FishingOpeningSettings openingSettings)
    {
        progression = questSystem;
        saveSystem = gameSave;
        player = playerCharacter;
        gameCamera = cameraComponent;
        cameraFollow = follow;
        firstLake = lake;
        settings = openingSettings ?? new FishingOpeningSettings();
        if (saveSystem != null) saveSystem.NewGameStarted += PlayIfNeeded;
        PlayIfNeeded();
    }

    public void PlayIfNeeded()
    {
        if (progression == null || progression.OpeningSeen) return;
        if (sequence != null) StopCoroutine(sequence);
        sequence = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        isBlocking = true;
        blackAlpha = 1f;
        player?.SetVisible(false);
        if (cameraFollow != null) cameraFollow.enabled = false;

        storyText = "도시에서 남은 것은 빚뿐이었다.";
        yield return WaitOrSkip(settings.FirstLineSeconds);
        storyText = "그러던 중, 어느 작은 마을에\n값비싼 물고기가 산다는 소문을 들었다.";
        yield return WaitOrSkip(settings.SecondLineSeconds);
        storyText = "낚아서 팔고,\n빚을 갚아보자.";
        yield return WaitOrSkip(settings.ThirdLineSeconds);

        storyText = "";
        Vector3 playerCameraPosition = GetPlayerCameraPosition();
        Vector3 pondCameraPosition = firstLake != null
            ? new Vector3(firstLake.WaterCenter.x, firstLake.WaterCenter.y, -10f)
            : playerCameraPosition;
        if (gameCamera != null) gameCamera.transform.position = pondCameraPosition;

        float elapsed = 0f;
        while (elapsed < settings.FadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            blackAlpha = 1f - Mathf.Clamp01(elapsed / settings.FadeSeconds);
            yield return null;
        }
        blackAlpha = 0f;

        elapsed = 0f;
        Vector3 start = gameCamera != null ? gameCamera.transform.position : playerCameraPosition;
        while (elapsed < settings.CameraPanSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / settings.CameraPanSeconds));
            if (gameCamera != null) gameCamera.transform.position = Vector3.Lerp(start, playerCameraPosition, t);
            yield return null;
        }

        player?.SetVisible(true);
        yield return new WaitForSecondsRealtime(settings.PlayerRevealDelay);
        showRegionTitle = true;
        regionTitleElapsed = 0f;
        while (regionTitleElapsed < settings.RegionTitleSeconds)
        {
            regionTitleElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        showRegionTitle = false;
        yield return new WaitForSecondsRealtime(.2f);
        if (cameraFollow != null) cameraFollow.enabled = true;
        progression.MarkOpeningSeen();
        saveSystem?.MarkDirty();
        isBlocking = false;
        sequence = null;
    }

    private IEnumerator WaitOrSkip(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) break;
            yield return null;
        }
    }

    private Vector3 GetPlayerCameraPosition()
    {
        Vector3 position = player != null ? player.transform.position : Vector3.zero;
        return new Vector3(position.x, position.y, -10f);
    }

    private void OnGUI()
    {
        if (!isBlocking && blackAlpha <= 0f) return;
        FishingUiTheme.Ensure();
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(Mathf.Max(blackAlpha, string.IsNullOrEmpty(storyText) ? 0f : 1f)));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;

        float scale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        if (!string.IsNullOrEmpty(storyText))
        {
            GUI.Label(new Rect(width * .16f, height * .40f, width * .68f, 150f), storyText, FishingUiTheme.Title);
            FishingUiTheme.ColoredLabel(new Rect(width * .25f, height - 90f, width * .5f, 30f),
                "SPACE로 넘기기", FishingUiTheme.SmallCentered, new Color(1f, 1f, 1f, .48f));
        }
        else if (showRegionTitle)
        {
            float remaining = settings.RegionTitleSeconds - regionTitleElapsed;
            float alpha = Mathf.Clamp01(Mathf.Min(regionTitleElapsed / .2f, remaining / .25f));
            Color faded = new(1f, 1f, 1f, alpha);
            Color old = GUI.color;
            GUI.color = faded;
            string displayName = firstLake != null && !string.IsNullOrWhiteSpace(firstLake.DisplayName)
                ? firstLake.DisplayName : "마을 연못 · 초급";
            string[] pieces = displayName.Split('·');
            var card = new Rect((width - 410f) * .5f, height * .25f, 410f, 82f);
            FishingUiTheme.DrawPanel(card, true);
            GUI.Label(new Rect(card.x + 20f, card.y + 9f, card.width - 40f, 36f), pieces[0].Trim(), FishingUiTheme.Title);
            FishingUiTheme.ColoredLabel(new Rect(card.x + 20f, card.y + 47f, card.width - 40f, 24f),
                pieces.Length > 1 ? pieces[1].Trim() + " 지역" : "초급 지역",
                FishingUiTheme.SmallCentered, FishingUiTheme.AccentBright);
            GUI.color = old;
        }
        GUI.matrix = previousMatrix;
    }

    private void OnDestroy()
    {
        if (saveSystem != null) saveSystem.NewGameStarted -= PlayIfNeeded;
    }
}
