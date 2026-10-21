using UnityEngine;

/// <summary>Inspector-facing layout and palette for the in-game HUD.</summary>
[System.Serializable]
public sealed class FishingHudSettings
{
    [Header("HUD Layout")]
    [Range(10f, 40f)] public float ScreenMargin = 18f;
    [Range(280f, 420f)] public float SidePanelWidth = 340f;
    [Range(400f, 680f)] public float CenterBannerWidth = 520f;
    [Range(90f, 160f)] public float TopPanelHeight = 116f;
    [Range(4f, 15f)] public float TutorialDisplaySeconds = 7f;
    [Range(120f, 190f)] public float MenuButtonWidth = 148f;
    [Range(44f, 72f)] public float MenuButtonHeight = 54f;

    [Header("HUD Palette")]
    public Color PrimaryPanel = new(.055f, .10f, .12f, .96f);
    public Color SecondaryPanel = new(.075f, .145f, .16f, .95f);
    public Color CardPanel = new(.09f, .17f, .18f, .96f);
    public Color Accent = new(.12f, .61f, .55f, 1f);
    public Color Highlight = new(.35f, .91f, .68f, 1f);
    public Color Reward = new(.98f, .73f, .25f, 1f);
}

/// <summary>Temporary flat IMGUI skin. Final art can replace this without changing game logic.</summary>
public static class FishingUiTheme
{
    public enum HudIcon { Bag, Book, Menu, Rod }

    public static Color Ink { get; private set; } = new(.035f, .047f, .055f, 1f);
    public static Color Panel { get; private set; } = new(.055f, .10f, .12f, .96f);
    public static Color PanelLight { get; private set; } = new(.075f, .145f, .16f, .95f);
    public static Color Card { get; private set; } = new(.09f, .17f, .18f, .96f);
    public static Color CardAlt { get; private set; } = new(.065f, .125f, .14f, .96f);
    public static readonly Color Cream = new(.98f, .93f, .78f, 1f);
    public static readonly Color Muted = new(.64f, .72f, .68f, 1f);
    public static Color Accent { get; private set; } = new(.12f, .61f, .55f, 1f);
    public static Color AccentBright { get; private set; } = new(.35f, .91f, .68f, 1f);
    public static readonly Color Water = new(.09f, .35f, .48f, 1f);
    public static readonly Color WaterBright = new(.22f, .67f, .76f, 1f);
    public static readonly Color Wood = new(.075f, .13f, .15f, 1f);
    public static readonly Color WoodLight = new(.22f, .43f, .44f, 1f);
    public static Color Gold { get; private set; } = new(.98f, .73f, .25f, 1f);
    public static readonly Color Danger = new(.91f, .30f, .25f, 1f);

    public static GUIStyle Title { get; private set; }
    public static GUIStyle Heading { get; private set; }
    public static GUIStyle Banner { get; private set; }
    public static GUIStyle Body { get; private set; }
    public static GUIStyle BodyCentered { get; private set; }
    public static GUIStyle Small { get; private set; }
    public static GUIStyle SmallCentered { get; private set; }
    public static GUIStyle MutedText { get; private set; }
    public static GUIStyle Button { get; private set; }
    public static GUIStyle PrimaryButton { get; private set; }
    public static GUIStyle DangerButton { get; private set; }
    public static GUIStyle DisabledButton { get; private set; }
    public static GUIStyle Tab { get; private set; }
    public static GUIStyle ActiveTab { get; private set; }
    public static GUIStyle Keycap { get; private set; }
    public static GUIStyle HudCaption { get; private set; }
    public static GUIStyle HudTitle { get; private set; }
    public static GUIStyle HudAction { get; private set; }

    public static FishingHudSettings Settings { get; private set; } = new();

    private static GUIStyle panelStyle;
    private static GUIStyle panelLightStyle;
    private static GUIStyle cardStyle;
    private static GUIStyle cardAltStyle;
    private static GUIStyle slotStyle;
    private static GUIStyle titlePlateStyle;
    private static GUIStyle badgeStyle;

    private static Texture2D panelTexture;
    private static Texture2D panelLightTexture;
    private static Texture2D cardTexture;
    private static Texture2D cardAltTexture;
    private static Texture2D slotTexture;
    private static Texture2D buttonTexture;
    private static Texture2D buttonHoverTexture;
    private static Texture2D buttonActiveTexture;
    private static Texture2D primaryTexture;
    private static Texture2D primaryHoverTexture;
    private static Texture2D dangerTexture;
    private static Texture2D dangerHoverTexture;
    private static Texture2D disabledTexture;
    private static Texture2D woodTexture;
    private static Texture2D keyTexture;
    private static Texture2D dimTexture;
    private static Texture2D trackTexture;
    private static bool initialized;

    public static void Configure(FishingHudSettings settings)
    {
        Settings = settings ?? new FishingHudSettings();
        Panel = Settings.PrimaryPanel;
        PanelLight = Settings.SecondaryPanel;
        Card = Settings.CardPanel;
        CardAlt = Color.Lerp(Settings.PrimaryPanel, Settings.SecondaryPanel, .42f);
        Accent = Settings.Accent;
        AccentBright = Settings.Highlight;
        Gold = Settings.Reward;
        initialized = false;
    }

    public static void Ensure()
    {
        if (initialized && Title != null) return;
        initialized = true;

        panelTexture = MakeFrameTexture(new Color(.015f, .035f, .045f), Panel, new Color(.18f, .34f, .34f), new Color(.025f, .06f, .07f));
        panelLightTexture = MakeFrameTexture(new Color(.02f, .05f, .055f), PanelLight, new Color(.25f, .43f, .38f), new Color(.035f, .08f, .08f));
        cardTexture = MakeFrameTexture(new Color(.02f, .055f, .06f), Card, new Color(.20f, .36f, .32f), new Color(.035f, .09f, .09f));
        cardAltTexture = MakeFrameTexture(new Color(.018f, .045f, .055f), CardAlt, new Color(.14f, .27f, .27f), new Color(.025f, .07f, .075f));
        slotTexture = MakeFrameTexture(new Color(.02f, .04f, .045f), new Color(.055f, .095f, .105f), new Color(.19f, .31f, .28f), new Color(.02f, .055f, .06f));
        buttonTexture = MakeFrameTexture(new Color(.025f, .055f, .06f), new Color(.13f, .24f, .23f), new Color(.27f, .43f, .36f), new Color(.055f, .12f, .12f));
        buttonHoverTexture = MakeFrameTexture(new Color(.03f, .08f, .075f), new Color(.18f, .34f, .30f), new Color(.40f, .61f, .46f), new Color(.07f, .16f, .14f));
        buttonActiveTexture = MakeFrameTexture(new Color(.015f, .04f, .045f), new Color(.08f, .17f, .17f), new Color(.15f, .29f, .27f), new Color(.025f, .07f, .075f), pressed: true);
        primaryTexture = MakeFrameTexture(new Color(.025f, .10f, .09f), new Color(.09f, .47f, .39f), new Color(.36f, .85f, .61f), new Color(.035f, .20f, .17f));
        primaryHoverTexture = MakeFrameTexture(new Color(.03f, .13f, .105f), new Color(.12f, .60f, .46f), new Color(.50f, .96f, .70f), new Color(.04f, .25f, .19f));
        dangerTexture = MakeFrameTexture(new Color(.14f, .035f, .03f), new Color(.56f, .14f, .13f), new Color(.91f, .37f, .27f), new Color(.25f, .055f, .05f));
        dangerHoverTexture = MakeFrameTexture(new Color(.18f, .045f, .035f), new Color(.70f, .18f, .15f), new Color(1f, .49f, .33f), new Color(.31f, .07f, .055f));
        disabledTexture = MakeFrameTexture(new Color(.035f, .045f, .045f), new Color(.10f, .13f, .13f), new Color(.20f, .23f, .22f), new Color(.055f, .07f, .07f));
        woodTexture = MakeFrameTexture(new Color(.02f, .055f, .065f), PanelLight, Accent, PanelLight);
        keyTexture = MakeFrameTexture(new Color(.025f, .04f, .045f), new Color(.20f, .27f, .27f), new Color(.48f, .59f, .53f), new Color(.08f, .12f, .12f));

        dimTexture = MakeTexture(new Color(.01f, .018f, .023f, .82f));
        trackTexture = MakeTexture(new Color(.025f, .09f, .12f, 1f));

        panelStyle = FramedStyle(panelTexture, 4);
        panelLightStyle = FramedStyle(panelLightTexture, 4);
        cardStyle = FramedStyle(cardTexture, 4);
        cardAltStyle = FramedStyle(cardAltTexture, 4);
        slotStyle = FramedStyle(slotTexture, 4);
        titlePlateStyle = FramedStyle(woodTexture, 4);
        badgeStyle = FramedStyle(cardAltTexture, 4);

        Title = Label(28, FontStyle.Bold, TextAnchor.MiddleCenter, Cream);
        Heading = Label(18, FontStyle.Bold, TextAnchor.MiddleLeft, Cream);
        Banner = Label(20, FontStyle.Bold, TextAnchor.MiddleCenter, Cream);
        Body = Label(16, FontStyle.Normal, TextAnchor.MiddleLeft, Cream);
        BodyCentered = Label(16, FontStyle.Normal, TextAnchor.MiddleCenter, Cream);
        Small = Label(14, FontStyle.Normal, TextAnchor.MiddleLeft, Muted);
        SmallCentered = Label(14, FontStyle.Normal, TextAnchor.MiddleCenter, Muted);
        MutedText = Label(15, FontStyle.Normal, TextAnchor.MiddleLeft, Muted);

        Button = CreateButton(buttonTexture, buttonHoverTexture, buttonActiveTexture, Cream);
        PrimaryButton = CreateButton(primaryTexture, primaryHoverTexture, buttonActiveTexture, Color.white);
        DangerButton = CreateButton(dangerTexture, dangerHoverTexture, buttonActiveTexture, Color.white);
        DisabledButton = CreateButton(disabledTexture, disabledTexture, disabledTexture, new Color(.43f, .49f, .47f));
        Tab = CreateButton(cardAltTexture, buttonHoverTexture, buttonActiveTexture, Muted);
        ActiveTab = CreateButton(primaryTexture, primaryHoverTexture, buttonActiveTexture, Color.white);
        Keycap = CreateButton(keyTexture, keyTexture, buttonActiveTexture, Cream);
        Keycap.fontSize = 14;
        Keycap.padding = new RectOffset(5, 5, 3, 4);
        HudCaption = Label(12, FontStyle.Bold, TextAnchor.MiddleLeft, Muted);
        HudTitle = Label(20, FontStyle.Bold, TextAnchor.MiddleCenter, Cream);
        HudAction = Label(15, FontStyle.Normal, TextAnchor.MiddleCenter, AccentBright);
    }

    public static void DrawDim(Rect rect)
    {
        Ensure();
        GUI.DrawTexture(rect, dimTexture);
    }

    public static void DrawPanel(Rect rect, bool light = false)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, light ? panelLightStyle : panelStyle);
    }

    public static void DrawCard(Rect rect, bool alternate = false)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, alternate ? cardAltStyle : cardStyle);
    }

    public static void DrawSlot(Rect rect, Color accent, bool selected = false)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, selected ? cardStyle : slotStyle);
        DrawSolid(new Rect(rect.x + 3f, rect.y + 3f, 5f, rect.height - 6f), accent);
        if (selected)
        {
            DrawSolid(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, 3f), accent);
            DrawSolid(new Rect(rect.x + 3f, rect.yMax - 6f, rect.width - 6f, 3f), accent);
        }
    }

    public static void DrawTitlePlate(Rect rect, string title, Color? accent = null)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, titlePlateStyle);
        DrawSolid(new Rect(rect.x + 4f, rect.y + 4f, 5f, rect.height - 8f), accent ?? Accent);
        GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 14f, rect.height), title, Title);
    }

    public static void DrawNotice(Rect rect, string text, Color? accent = null)
    {
        Ensure();
        DrawPanel(rect, true);
        DrawSolid(new Rect(rect.x + 5f, rect.y + 5f, 5f, rect.height - 10f), accent ?? WaterBright);
        GUI.Label(new Rect(rect.x + 16f, rect.y + 2f, rect.width - 24f, rect.height - 4f), text, Banner);
    }

    public static void DrawInteractionPrompt(Rect rect, string key, string label)
    {
        Ensure();
        DrawPanel(rect, true);
        Rect keyRect = new(rect.x + 10f, rect.y + 8f, 40f, rect.height - 16f);
        GUI.Label(keyRect, key, Keycap);
        GUI.Label(new Rect(rect.x + 60f, rect.y + 2f, rect.width - 70f, rect.height - 4f), label, BodyCentered);
    }

    public static void DrawHudPanel(Rect rect, string caption, Color accent, bool emphasized = false)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, emphasized ? panelLightStyle : panelStyle);
        DrawSolid(new Rect(rect.x + 4f, rect.y + 4f, 5f, rect.height - 8f), accent);
        if (!string.IsNullOrWhiteSpace(caption))
            ColoredLabel(new Rect(rect.x + 16f, rect.y + 7f, rect.width - 28f, 18f), caption, HudCaption, accent);
    }

    public static void DrawLocationBanner(Rect rect, string location, string action, bool actionable, float actionAlpha = 1f)
    {
        Ensure();
        DrawHudPanel(rect, "현재 지역", actionable ? AccentBright : WaterBright, actionable);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 25f, rect.width - 36f, 30f), location, HudTitle);
        Color actionColor = actionable ? AccentBright : Muted;
        actionColor.a *= Mathf.Clamp01(actionAlpha);
        ColoredLabel(new Rect(rect.x + 18f, rect.y + 56f, rect.width - 36f, 25f), action, HudAction, actionColor);
    }

    public static void DrawTutorialGuide(Rect rect, string text)
    {
        Ensure();
        DrawHudPanel(rect, "GUIDE", Gold, true);
        GUI.Label(new Rect(rect.x + 76f, rect.y + 5f, rect.width - 90f, rect.height - 10f), text, BodyCentered);
    }

    public static bool DrawMenuButton(Rect rect, HudIcon icon, string label, string key)
    {
        Ensure();
        bool clicked = GUI.Button(rect, GUIContent.none, Button);
        Rect iconRect = new(rect.x + 12f, rect.y + 13f, 28f, 28f);
        DrawHudIcon(iconRect, icon, AccentBright);
        float keyWidth = key != null && key.Length > 1 ? 42f : 32f;
        GUI.Label(new Rect(rect.x + 47f, rect.y + 5f, rect.width - keyWidth - 62f, rect.height - 10f), label, BodyCentered);
        GUI.Label(new Rect(rect.xMax - keyWidth - 11f, rect.y + 14f, keyWidth, rect.height - 28f), key, Keycap);
        return clicked;
    }

    public static void DrawHudIcon(Rect rect, HudIcon icon, Color color)
    {
        Ensure();
        float t = 3f;
        if (icon == HudIcon.Bag)
        {
            DrawFlatOutline(new Rect(rect.x + 3f, rect.y + 8f, rect.width - 6f, rect.height - 11f), color, t);
            DrawFlatOutline(new Rect(rect.x + 9f, rect.y + 2f, rect.width - 18f, 10f), color, t);
        }
        else if (icon == HudIcon.Book)
        {
            DrawFlatOutline(new Rect(rect.x + 1f, rect.y + 4f, rect.width * .46f, rect.height - 8f), color, 2f);
            DrawFlatOutline(new Rect(rect.x + rect.width * .53f, rect.y + 4f, rect.width * .46f, rect.height - 8f), color, 2f);
            DrawSolid(new Rect(rect.center.x - 1f, rect.y + 4f, 2f, rect.height - 8f), color);
        }
        else if (icon == HudIcon.Rod)
        {
            DrawSolid(new Rect(rect.x + 8f, rect.y + 3f, 3f, rect.height - 7f), color);
            DrawSolid(new Rect(rect.x + 10f, rect.y + 3f, rect.width - 14f, 3f), color);
            DrawSolid(new Rect(rect.xMax - 6f, rect.y + 5f, 2f, rect.height - 9f), color);
        }
        else
        {
            DrawSolid(new Rect(rect.x + 3f, rect.y + 5f, rect.width - 6f, 3f), color);
            DrawSolid(new Rect(rect.x + 3f, rect.center.y - 1f, rect.width - 6f, 3f), color);
            DrawSolid(new Rect(rect.x + 3f, rect.yMax - 8f, rect.width - 6f, 3f), color);
        }
    }

    public static void DrawMoneyChip(Rect rect, int amount)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, badgeStyle);
        DrawSolid(new Rect(rect.x + 7f, rect.y + rect.height * .5f - 6f, 12f, 12f), Gold);
        DrawSolid(new Rect(rect.x + 10f, rect.y + rect.height * .5f - 3f, 6f, 6f), new Color(.55f, .28f, .08f));
        ColoredLabel(new Rect(rect.x + 25f, rect.y, rect.width - 32f, rect.height), $"{amount:N0} G", Heading, Gold);
    }

    public static void DrawBadge(Rect rect, string text, Color color)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, badgeStyle);
        DrawSolid(new Rect(rect.x + 4f, rect.y + 4f, 4f, rect.height - 8f), color);
        ColoredLabel(new Rect(rect.x + 12f, rect.y, rect.width - 16f, rect.height), text, SmallCentered, color);
    }

    public static void DrawDivider(Rect rect)
    {
        Ensure();
        DrawSolid(rect, new Color(.31f, .49f, .41f));
        if (rect.height >= 3f) DrawSolid(new Rect(rect.x, rect.y, rect.width, 1f), new Color(.52f, .70f, .54f));
    }

    public static void DrawBar(Rect rect, float value, Color fill)
        => DrawBar(rect, value, fill, null);

    public static void DrawBar(Rect rect, float value, Color fill, string label)
    {
        Ensure();
        value = Mathf.Clamp01(value);
        GUI.Box(rect, GUIContent.none, slotStyle);
        Rect inner = new(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
        DrawSolid(inner, new Color(.015f, .045f, .055f));
        if (value > 0f)
        {
            Rect fillRect = new(inner.x, inner.y, inner.width * value, inner.height);
            DrawSolid(fillRect, fill);
        }
        for (int i = 1; i < 10; i++)
        {
            float x = inner.x + inner.width * i / 10f;
            DrawSolid(new Rect(x, inner.y, 1f, inner.height), new Color(0f, 0f, 0f, .22f));
        }
        if (!string.IsNullOrEmpty(label)) GUI.Label(rect, label, SmallCentered);
    }

    public static void DrawFishingLane(Rect rect)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, slotStyle);
        Rect inner = new(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
        GUI.DrawTexture(inner, trackTexture);
        DrawSolid(new Rect(inner.x, inner.y + inner.height * .5f - 1f, inner.width, 2f), new Color(.17f, .45f, .52f, .55f));
        for (int i = 1; i < 20; i++)
        {
            float x = inner.x + inner.width * i / 20f;
            float tick = i % 5 == 0 ? 11f : 6f;
            DrawSolid(new Rect(x, inner.yMax - tick, 1f, tick), new Color(.28f, .59f, .61f, .50f));
        }
    }

    public static void DrawCaptureZone(Rect rect)
    {
        Ensure();
        DrawSolid(rect, new Color(AccentBright.r, AccentBright.g, AccentBright.b, .30f));
        DrawSolid(new Rect(rect.x, rect.y, rect.width, 3f), AccentBright);
        DrawSolid(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), AccentBright);
        DrawSolid(new Rect(rect.x, rect.y, 3f, rect.height), AccentBright);
        DrawSolid(new Rect(rect.xMax - 3f, rect.y, 3f, rect.height), AccentBright);
    }

    public static void DrawTextureOrSwatch(Rect rect, Texture texture, Color swatch)
    {
        Ensure();
        GUI.Box(rect, GUIContent.none, slotStyle);
        Rect inner = new(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
        if (texture != null) GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit, true);
        else DrawSolid(inner, swatch);
    }

    public static void DrawRarityStrip(Rect rect, Color color)
    {
        Ensure();
        DrawSolid(new Rect(rect.x + 3f, rect.y + 3f, 5f, rect.height - 6f), color);
    }

    public static bool StyledButton(Rect rect, string text, bool primary = false, bool danger = false, bool enabled = true)
    {
        Ensure();
        GUIStyle style = !enabled ? DisabledButton : danger ? DangerButton : primary ? PrimaryButton : Button;
        bool previous = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = GUI.Button(rect, text, style);
        GUI.enabled = previous;
        return clicked;
    }

    public static void ColoredLabel(Rect rect, string text, GUIStyle style, Color color)
    {
        Color previous = GUI.contentColor;
        GUI.contentColor = color;
        GUI.Label(rect, text, style);
        GUI.contentColor = previous;
    }

    public static void DrawFlatRect(Rect rect, Color color)
    {
        Ensure();
        DrawSolid(rect, color);
    }

    public static void DrawFlatOutline(Rect rect, Color color, float thickness = 2f)
    {
        Ensure();
        thickness = Mathf.Max(1f, thickness);
        DrawSolid(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawSolid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawSolid(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawSolid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static void DrawSolid(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static GUIStyle Label(int size, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        return new GUIStyle(GUI.skin.label)
        {
            alignment = alignment,
            fontSize = size,
            fontStyle = fontStyle,
            wordWrap = true,
            clipping = TextClipping.Clip,
            normal = { textColor = color }
        };
    }

    private static GUIStyle FramedStyle(Texture2D background, int border)
    {
        return new GUIStyle(GUI.skin.box)
        {
            normal = { background = background },
            border = new RectOffset(border, border, border, border),
            padding = new RectOffset(border, border, border, border)
        };
    }

    private static GUIStyle CreateButton(Texture2D normal, Texture2D hover, Texture2D active, Color textColor)
    {
        var style = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(12, 12, 7, 8),
            border = new RectOffset(4, 4, 4, 4)
        };
        style.normal.background = normal;
        style.normal.textColor = textColor;
        style.hover.background = hover;
        style.hover.textColor = Color.white;
        style.active.background = active;
        style.active.textColor = Color.white;
        style.focused.background = normal;
        style.focused.textColor = textColor;
        style.onNormal.background = primaryTexture;
        style.onNormal.textColor = Color.white;
        return style;
    }

    private static Texture2D MakeTexture(Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Fishing UI " + ColorUtility.ToHtmlStringRGBA(color),
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D MakeFrameTexture(Color outline, Color fill, Color highlight, Color shade, bool pressed = false)
    {
        const int size = 9;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Fishing UI Pixel Frame",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool outer = x == 0 || y == 0 || x == size - 1 || y == size - 1;
            texture.SetPixel(x, y, outer ? outline : fill);
        }

        texture.Apply(false, true);
        return texture;
    }
}
