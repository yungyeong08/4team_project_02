using UnityEngine;

/// <summary>Shared allocation-free IMGUI theme for the playable prototype.</summary>
public static class FishingUiTheme
{
    public static readonly Color Ink = new(.055f, .075f, .09f, 1f);
    public static readonly Color Panel = new(.09f, .14f, .16f, 1f);
    public static readonly Color PanelLight = new(.13f, .20f, .21f, 1f);
    public static readonly Color Card = new(.16f, .24f, .23f, 1f);
    public static readonly Color CardAlt = new(.12f, .18f, .19f, 1f);
    public static readonly Color Cream = new(.96f, .91f, .78f, 1f);
    public static readonly Color Muted = new(.67f, .74f, .69f, 1f);
    public static readonly Color Accent = new(.25f, .72f, .55f, 1f);
    public static readonly Color AccentBright = new(.40f, .88f, .63f, 1f);
    public static readonly Color Gold = new(.96f, .72f, .28f, 1f);
    public static readonly Color Danger = new(.86f, .31f, .28f, 1f);

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

    private static Texture2D panelTexture;
    private static Texture2D panelLightTexture;
    private static Texture2D cardTexture;
    private static Texture2D cardAltTexture;
    private static Texture2D buttonTexture;
    private static Texture2D buttonHoverTexture;
    private static Texture2D buttonActiveTexture;
    private static Texture2D primaryTexture;
    private static Texture2D primaryHoverTexture;
    private static Texture2D dangerTexture;
    private static Texture2D disabledTexture;
    private static Texture2D borderTexture;
    private static Texture2D shadowTexture;
    private static Texture2D dimTexture;
    private static bool initialized;

    public static void Ensure()
    {
        if (initialized && Title != null) return;
        initialized = true;

        panelTexture = MakeTexture(Panel);
        panelLightTexture = MakeTexture(PanelLight);
        cardTexture = MakeTexture(Card);
        cardAltTexture = MakeTexture(CardAlt);
        buttonTexture = MakeTexture(new Color(.20f, .30f, .29f, 1f));
        buttonHoverTexture = MakeTexture(new Color(.27f, .40f, .36f, 1f));
        buttonActiveTexture = MakeTexture(new Color(.14f, .24f, .23f, 1f));
        primaryTexture = MakeTexture(new Color(.17f, .55f, .39f, 1f));
        primaryHoverTexture = MakeTexture(new Color(.23f, .67f, .47f, 1f));
        dangerTexture = MakeTexture(new Color(.62f, .20f, .20f, 1f));
        disabledTexture = MakeTexture(new Color(.15f, .18f, .18f, 1f));
        borderTexture = MakeTexture(new Color(.35f, .49f, .43f, 1f));
        shadowTexture = MakeTexture(new Color(0f, 0f, 0f, .36f));
        dimTexture = MakeTexture(new Color(.015f, .025f, .03f, .76f));

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
        DangerButton = CreateButton(dangerTexture, MakeTexture(new Color(.76f, .26f, .23f, 1f)), buttonActiveTexture, Color.white);
        DisabledButton = CreateButton(disabledTexture, disabledTexture, disabledTexture, new Color(.48f, .54f, .51f));
        Tab = CreateButton(cardAltTexture, buttonHoverTexture, buttonActiveTexture, Muted);
        ActiveTab = CreateButton(primaryTexture, primaryHoverTexture, buttonActiveTexture, Color.white);
    }

    public static void DrawDim(Rect rect)
    {
        Ensure();
        GUI.DrawTexture(rect, dimTexture);
    }

    public static void DrawPanel(Rect rect, bool light = false)
    {
        Ensure();
        GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 10f, rect.width, rect.height), shadowTexture);
        GUI.DrawTexture(rect, borderTexture);
        GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), light ? panelLightTexture : panelTexture);
    }

    public static void DrawCard(Rect rect, bool alternate = false)
    {
        Ensure();
        GUI.DrawTexture(rect, borderTexture);
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), alternate ? cardAltTexture : cardTexture);
    }

    public static void DrawDivider(Rect rect)
    {
        Ensure();
        GUI.DrawTexture(rect, borderTexture);
    }

    public static void DrawBar(Rect rect, float value, Color fill)
    {
        Ensure();
        value = Mathf.Clamp01(value);
        GUI.DrawTexture(rect, borderTexture);
        Rect inner = new(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
        GUI.DrawTexture(inner, MakeTextureCachedBackground());
        if (value <= 0f) return;
        Color previous = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * value, inner.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    public static void DrawTextureOrSwatch(Rect rect, Texture texture, Color swatch)
    {
        DrawCard(rect, true);
        Rect inner = new(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
        if (texture != null) GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit, true);
        else
        {
            Color previous = GUI.color;
            GUI.color = swatch;
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            GUI.color = previous;
        }
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

    private static GUIStyle Label(int size, FontStyle fontStyle, TextAnchor alignment, Color color)
    {
        return new GUIStyle(GUI.skin.label)
        {
            alignment = alignment,
            fontSize = size,
            fontStyle = fontStyle,
            wordWrap = true,
            normal = { textColor = color }
        };
    }

    private static GUIStyle CreateButton(Texture2D normal, Texture2D hover, Texture2D active, Color textColor)
    {
        var style = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(12, 12, 7, 7),
            border = new RectOffset(2, 2, 2, 2)
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

    private static Texture2D MakeTextureCachedBackground() => cardAltTexture;
}
