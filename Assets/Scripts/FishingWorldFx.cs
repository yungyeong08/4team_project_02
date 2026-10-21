using UnityEngine;

/// <summary>Small replaceable world-space ripple/splash placeholder effects.</summary>
public sealed class FishingWorldFx : MonoBehaviour
{
    private static Sprite ringSprite;
    private SpriteRenderer spriteRenderer;
    private float duration;
    private float elapsed;
    private float startScale;
    private float endScale;
    private Color color;

    public static void SpawnRipple(Vector2 position, Color color, float duration = .55f, float size = .9f)
    {
        var effect = new GameObject("Fishing Ripple");
        effect.transform.position = position;
        var renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = GetRingSprite();
        renderer.sortingOrder = 5;
        var animation = effect.AddComponent<FishingWorldFx>();
        animation.spriteRenderer = renderer;
        animation.duration = Mathf.Max(.1f, duration);
        animation.startScale = .12f;
        animation.endScale = Mathf.Max(.2f, size);
        animation.color = color;
    }

    public static void SpawnCatchSplash(Vector2 position)
    {
        SpawnRipple(position, new Color(.55f, .88f, 1f, .8f), .52f, 1.05f);
        SpawnRipple(position + new Vector2(.16f, .08f), new Color(.75f, .94f, 1f, .62f), .34f, .56f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
        if (spriteRenderer != null)
        {
            Color faded = color;
            faded.a *= 1f - t;
            spriteRenderer.color = faded;
        }
        if (t >= 1f) Destroy(gameObject);
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int size = 24;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Vector2 center = new((size - 1) * .5f, (size - 1) * .5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 delta = new(x - center.x, (y - center.y) * 1.8f);
            float distance = delta.magnitude;
            bool ring = distance >= 8.5f && distance <= 10.5f;
            texture.SetPixel(x, y, ring ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        return ringSprite;
    }
}
