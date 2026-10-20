using UnityEngine;

/// <summary>Runtime player movement and sprite animation.</summary>
public sealed class PixelCharacter : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] walkDown;
    private Sprite[] walkUp;
    private Sprite[] fishingFrames;
    private float frameTime;
    private bool isFishing;
    private bool facingUp;
    public float speed = 2.2f;
    public float runSpeed = 4.4f;

    public void Setup()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 3;
        var collider = GetComponent<CircleCollider2D>();
        if (collider == null) collider = gameObject.AddComponent<CircleCollider2D>();
        collider.radius = .16f;

        walkDown = CreateWalkCycle(
            LoadCharacterSprite("Sprites/Player/MainCharacter/Player1"),
            LoadCharacterSprite("Sprites/Player/MainCharacter/Player2"),
            LoadCharacterSprite("Sprites/Player/MainCharacter/Player3"));
        walkUp = CreateWalkCycle(
            LoadCharacterSprite("Sprites/Player/MainCharacter/Player4", true),
            LoadCharacterSprite("Sprites/Player/MainCharacter/Player5", true),
            LoadCharacterSprite("Sprites/Player/MainCharacter/Player6", true));
        fishingFrames = CreateSheetFrames(Resources.Load<Texture2D>("Sprites/Player/FishingDown"), 48f);

        SetFrame(walkDown, 0);
    }

    public void Move()
    {
        Vector2 movement = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        movement = movement.normalized;
        float currentSpeed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        ? runSpeed : speed;

        Vector2 nextPosition = (Vector2)transform.position + movement * currentSpeed * Time.deltaTime;
        Collider2D[] collisionBuffer = Physics2D.OverlapCircleAll(nextPosition, .14f);
        int hitCount = collisionBuffer.Length;
        bool blocked = false;
        for (int i = 0; i < hitCount; i++)
            if (collisionBuffer[i] != null && collisionBuffer[i].gameObject != gameObject) { blocked = true; break; }
        if (!blocked) transform.position = nextPosition;

        if (movement.y > .01f) facingUp = true;
        else if (movement.y < -.01f) facingUp = false;

        frameTime += Time.deltaTime;
        bool isMoving = movement.sqrMagnitude > .01f;
        SetFrame(facingUp ? walkUp : walkDown, isMoving ? Mathf.FloorToInt(frameTime * 7f) : 0);
        if (Mathf.Abs(movement.x) > .01f) spriteRenderer.flipX = movement.x < 0;
        else spriteRenderer.flipX = false;
    }

    public void SetFishing(bool value)
    {
        isFishing = value;
        frameTime = 0;
        if (value) SetFrame(fishingFrames, 0);
        else SetFrame(facingUp ? walkUp : walkDown, 0);
    }

    private void Update()
    {
        if (!isFishing || spriteRenderer == null) return;
        frameTime += Time.deltaTime;
        SetFrame(fishingFrames, Mathf.FloorToInt(frameTime * 7f));
    }

    private static Sprite LoadCharacterSprite(string resourcePath, bool removeWhiteBackground = false)
    {
        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null) return null;
        if (removeWhiteBackground) texture = RemoveWhiteBackground(texture);
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(.5f, .1f),
            48f);
    }

    private static Texture2D RemoveWhiteBackground(Texture2D source)
    {
        var pixels = source.GetPixels32();
        int width = source.width;
        int height = source.height;
        var background = new bool[pixels.Length];
        var queue = new int[pixels.Length];
        int head = 0;
        int tail = 0;

        void EnqueueBackground(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int index = y * width + x;
            if (background[index]) return;

            Color32 color = pixels[index];
            int minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            int maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            bool isLightNeutralBackground = color.a > 0 && minimum >= 170 && maximum - minimum <= 40;
            if (!isLightNeutralBackground) return;

            background[index] = true;
            queue[tail++] = index;
        }

        for (int x = 0; x < width; x++)
        {
            EnqueueBackground(x, 0);
            EnqueueBackground(x, height - 1);
        }
        for (int y = 0; y < height; y++)
        {
            EnqueueBackground(0, y);
            EnqueueBackground(width - 1, y);
        }

        while (head < tail)
        {
            int index = queue[head++];
            int x = index % width;
            int y = index / width;
            Color32 color = pixels[index];
            pixels[index] = new Color32(color.r, color.g, color.b, 0);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                    if (offsetX != 0 || offsetY != 0)
                        EnqueueBackground(x + offsetX, y + offsetY);
        }

        var cleaned = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = source.name + "_Transparent"
        };
        cleaned.SetPixels32(pixels);
        cleaned.Apply();
        return cleaned;
    }

    private static Sprite[] CreateWalkCycle(Sprite first, Sprite second, Sprite third)
    {
        if (first == null || second == null || third == null) return new Sprite[0];
        return new[] { first, second, third, second };
    }

    private static Sprite[] CreateSheetFrames(Texture2D source, float pixelsPerUnit)
    {
        if (source == null) return new Sprite[0];
        source.filterMode = FilterMode.Point;
        int count = source.width / 64;
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
            frames[i] = Sprite.Create(source, new Rect(i * 64, 0, 64, 64), new Vector2(.5f, .1f), pixelsPerUnit);
        return frames;
    }

    private void SetFrame(Sprite[] frames, int frame)
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0) return;
        spriteRenderer.sprite = frames[Mathf.Abs(frame) % frames.Length];
    }
}
