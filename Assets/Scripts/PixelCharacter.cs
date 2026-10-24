using UnityEngine;

/// <summary>Runtime player movement and sprite animation.</summary>
public sealed class PixelCharacter : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Transform visualTransform;
    private Sprite[] walkDown;
    private Sprite[] walkUp;
    private Sprite[] walkLeft;
    private Sprite[] walkRight;
    private Sprite fishingPose;
    private float frameTime;
    private bool isFishing;
    private bool isReeling;
    private bool facingUp;
    private bool facingLeft;
    private bool facingHorizontal;
    private Vector3 visualOrigin;
    private bool isMoving;
    private bool isRunning;
    public float speed = 2.2f;
    public float runSpeed = 4.4f;

    public bool IsMoving => isMoving;
    public bool IsRunning => isRunning;
    public bool IsFishing => isFishing;

    public void Setup()
    {
        visualTransform = transform.Find("Visual");
        if (visualTransform == null)
        {
            var visualObject = new GameObject("Visual");
            visualTransform = visualObject.transform;
            visualTransform.SetParent(transform, false);
        }
        spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
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

        PixelCharacterVisualConfig visualConfig = PixelCharacterVisualConfig.Load();
        walkLeft = CreateWalkCycle(visualConfig != null ? visualConfig.WalkLeft : null);
        walkRight = CreateWalkCycle(visualConfig != null ? visualConfig.WalkRight : null);
        fishingPose = NormalizeSprite(visualConfig != null ? visualConfig.FishingPose : null);

        SetFrame(walkDown, 0);
        visualOrigin = Vector3.zero;
    }

    public void Move()
    {
        Vector2 movement = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        movement = movement.normalized;
        isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isRunning ? runSpeed : speed;

        Vector2 nextPosition = (Vector2)transform.position + movement * currentSpeed * Time.deltaTime;
        Collider2D[] collisionBuffer = Physics2D.OverlapCircleAll(nextPosition, .14f);
        int hitCount = collisionBuffer.Length;
        bool blocked = false;
        for (int i = 0; i < hitCount; i++)
            if (collisionBuffer[i] != null && collisionBuffer[i].gameObject != gameObject) { blocked = true; break; }
        if (!blocked) transform.position = nextPosition;

        if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y) && Mathf.Abs(movement.x) > .01f)
        {
            facingHorizontal = true;
            facingLeft = movement.x < 0f;
        }
        else if (Mathf.Abs(movement.y) > .01f)
        {
            facingHorizontal = false;
            facingUp = movement.y > 0f;
        }

        frameTime += Time.deltaTime;
        isMoving = movement.sqrMagnitude > .01f && !blocked;
        int frame = isMoving ? Mathf.FloorToInt(frameTime * 7f) : 0;
        if (facingHorizontal && HasFrames(facingLeft ? walkLeft : walkRight))
            SetFrame(facingLeft ? walkLeft : walkRight, frame);
        else
            SetFrame(facingUp ? walkUp : walkDown, frame);
        spriteRenderer.flipX = false;
    }

    public void SetFishing(bool value)
    {
        isFishing = value;
        isReeling = false;
        isMoving = false;
        isRunning = false;
        frameTime = 0;
        visualTransform.localPosition = visualOrigin;
        visualTransform.localRotation = Quaternion.identity;
        if (value && fishingPose != null) spriteRenderer.sprite = fishingPose;
        else SetIdleFrame();
    }

    public void StopMovement()
    {
        isMoving = false;
        isRunning = false;
        frameTime = 0f;
        if (!isFishing) SetIdleFrame();
    }

    public void SetReeling(bool value)
    {
        isReeling = isFishing && value;
        frameTime = 0f;
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            isMoving = false;
            isRunning = false;
        }
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
    }

    private void Update()
    {
        if (!isFishing || spriteRenderer == null) return;
        frameTime += Time.deltaTime;
        if (fishingPose != null) spriteRenderer.sprite = fishingPose;

        // The supplied fishing art is a single pose. A restrained tug motion makes the
        // reeling phase readable without inventing extra frames or moving the collider.
        if (isReeling)
        {
            float tug = Mathf.Sin(frameTime * 12f);
            visualTransform.localPosition = visualOrigin + Vector3.up * (tug > .25f ? .025f : 0f);
            visualTransform.localRotation = Quaternion.Euler(0f, 0f, tug * .7f);
        }
        else
        {
            visualTransform.localPosition = visualOrigin;
            visualTransform.localRotation = Quaternion.identity;
        }
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

    private static Sprite[] CreateWalkCycle(Sprite[] source)
    {
        if (source == null || source.Length < 3) return new Sprite[0];
        Sprite first = NormalizeSprite(source[0]);
        Sprite second = NormalizeSprite(source[1]);
        Sprite third = NormalizeSprite(source[2]);
        return CreateWalkCycle(first, second, third);
    }

    private static Sprite NormalizeSprite(Sprite source)
    {
        if (source == null || source.texture == null) return null;
        source.texture.filterMode = FilterMode.Point;
        Sprite normalized = Sprite.Create(source.texture, source.rect, new Vector2(.5f, .1f), 48f);
        normalized.name = source.name;
        return normalized;
    }

    private static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;

    private void SetIdleFrame()
    {
        if (facingHorizontal && HasFrames(facingLeft ? walkLeft : walkRight))
            SetFrame(facingLeft ? walkLeft : walkRight, 0);
        else
            SetFrame(facingUp ? walkUp : walkDown, 0);
    }

    private void SetFrame(Sprite[] frames, int frame)
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0) return;
        spriteRenderer.sprite = frames[Mathf.Abs(frame) % frames.Length];
    }
}
