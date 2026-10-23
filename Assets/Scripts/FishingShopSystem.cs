using System.Collections.Generic;
using UnityEngine;

/// <summary>Handles fishing-shop proximity, input, product presentation, and purchases.</summary>
public sealed class FishingShopSystem : MonoBehaviour
{
    [SerializeField, Min(.5f)] private float interactionRange = 1.8f;

    private PixelCharacter player;
    private Transform shopTarget;
    private PlayerWallet wallet;
    private FishingRodInventory inventory;
    private List<FishingRodDefinition> rodCatalog;
    private System.Func<bool> inputBlocked;
    private Vector2 scrollPosition;
    private bool isOpen;
    private bool isPlayerInRange;
    private string statusMessage = "상품을 선택해 주세요.";
    private float logicalScreenWidth;
    private float logicalScreenHeight;
    private FishingAudioDirector audioDirector;
    private float promptVisibility;
    private float windowReveal;
    private bool windowClosing;

    public bool IsOpen => isOpen;
    public bool IsPlayerInRange => isPlayerInRange;
    public int ProductCount => rodCatalog?.Count ?? 0;
    public string StatusMessage => statusMessage;
    public event System.Action<string> RodPurchased;

    public void Setup(
        PixelCharacter playerCharacter,
        Transform target,
        PlayerWallet playerWallet,
        FishingRodInventory rodInventory,
        List<FishingRodDefinition> catalog)
    {
        player = playerCharacter;
        shopTarget = target;
        wallet = playerWallet;
        inventory = rodInventory;
        rodCatalog = catalog ?? FishingRodCatalog.CreateDefault();
        inventory?.EnsureDefaultRod();

        RefreshRange();
    }

    public void SetInputBlocker(System.Func<bool> blocker) => inputBlocked = blocker;
    public void SetAudio(FishingAudioDirector audio) => audioDirector = audio;

    private void Update()
    {
        RefreshRange();
        if (isOpen)
        {
            windowReveal = Mathf.MoveTowards(windowReveal, windowClosing ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            if (windowClosing && windowReveal <= 0f)
            {
                isOpen = false;
                windowClosing = false;
                statusMessage = "상품을 선택해 주세요.";
            }
        }
        promptVisibility = Mathf.MoveTowards(promptVisibility, isPlayerInRange && !isOpen ? 1f : 0f,
            Time.unscaledDeltaTime * 6f);

        if (!isOpen && inputBlocked != null && inputBlocked()) return;

        if (isOpen)
        {
            if (!windowClosing && Input.GetKeyDown(KeyCode.Escape)) CloseShop();
            return;
        }

        if (isPlayerInRange && Input.GetKeyDown(KeyCode.F)) OpenShop();
    }

    private void RefreshRange()
    {
        isPlayerInRange = player != null && shopTarget != null &&
            Vector2.Distance(player.transform.position, shopTarget.position) <= interactionRange;
    }

    public bool OpenShop()
    {
        if (isOpen || !isPlayerInRange) return false;
        isOpen = true;
        windowClosing = false;
        windowReveal = 0f;
        statusMessage = "상품을 선택해 주세요.";
        audioDirector?.PlayMenuOpen();
        return true;
    }

    public void CloseShop()
    {
        if (!isOpen || windowClosing) return;
        audioDirector?.PlayMenuClose();
        windowClosing = true;
    }

    public bool TryPurchase(string productId)
    {
        FishingRodDefinition product = rodCatalog?.Find(candidate => candidate.Id == productId);
        if (product == null || wallet == null || inventory == null)
        {
            statusMessage = "상품 정보를 불러오지 못했습니다.";
            return false;
        }

        if (inventory.Owns(product.Id))
        {
            statusMessage = "이미 구매한 낚싯대입니다.";
            return false;
        }

        if (!wallet.TrySpend(product.Price))
        {
            statusMessage = "돈이 부족합니다.";
            return false;
        }

        if (!inventory.TryAddAndEquip(product.Id))
        {
            wallet.AddMoney(product.Price);
            statusMessage = "구매 처리 중 문제가 발생했습니다.";
            return false;
        }

        statusMessage = product.DisplayName + " 구매 완료! 자동으로 장착했습니다.";
        audioDirector?.PlayPurchase();
        RodPurchased?.Invoke(product.Id);
        return true;
    }

    private void OnGUI()
    {
        if (player == null || shopTarget == null) return;
        float uiScale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        logicalScreenWidth = Screen.width / uiScale;
        logicalScreenHeight = Screen.height / uiScale;
        FishingUiTheme.Ensure();

        if (!isOpen)
        {
            bool blocked = inputBlocked != null && inputBlocked();
            if (promptVisibility > 0f && !blocked) DrawInteractionPrompt();
            GUI.matrix = previousMatrix;
            return;
        }

        Color previousColor = GUI.color;
        bool previousEnabled = GUI.enabled;
        GUI.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, windowReveal));
        GUI.enabled = previousEnabled && !windowClosing && windowReveal >= .85f;
        DrawShopWindow();
        GUI.enabled = previousEnabled;
        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    private void DrawInteractionPrompt()
    {
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, promptVisibility);
        var prompt = new Rect(logicalScreenWidth * .5f - 158f, logicalScreenHeight - 104f, 316f, 56f);
        FishingUiTheme.DrawInteractionPrompt(prompt, "F", "낚시 상점 열기");
        GUI.color = old;
    }

    private void DrawShopWindow()
    {
        FishingUiTheme.DrawDim(new Rect(0f, 0f, logicalScreenWidth, logicalScreenHeight));

        float width = Mathf.Min(720f, logicalScreenWidth - 36f);
        float height = Mathf.Min(500f, logicalScreenHeight - 36f);
        var panel = new Rect((logicalScreenWidth - width) * .5f, (logicalScreenHeight - height) * .5f, width, height);
        FishingUiTheme.DrawPanel(panel);

        FishingUiTheme.DrawTitlePlate(new Rect(panel.x + 180f, panel.y - 14f, panel.width - 360f, 56f), "낚시 상점");
        if (FishingUiTheme.StyledButton(new Rect(panel.xMax - 57f, panel.y + 15f, 40f, 36f), "×", danger: true)) CloseShop();

        int balance = wallet != null ? wallet.Balance : 0;
        FishingUiTheme.DrawMoneyChip(new Rect(panel.x + 24f, panel.y + 59f, 190f, 40f), balance);
        GUI.Label(new Rect(panel.x + 228f, panel.y + 61f, panel.width - 252f, 36f),
            "더 좋은 낚싯대로 어려운 물고기에 도전하세요", FishingUiTheme.Small);

        var viewRect = new Rect(panel.x + 20f, panel.y + 108f, panel.width - 40f, panel.height - 174f);
        int productCount = rodCatalog != null ? Mathf.Min(2, rodCatalog.Count) : 0;
        float contentHeight = Mathf.Max(viewRect.height - 4f, productCount * 126f);
        scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, new Rect(0f, 0f, viewRect.width - 18f, contentHeight));
        if (rodCatalog != null)
            for (int i = 0; i < productCount; i++)
                DrawProduct(rodCatalog[i], new Rect(2f, i * 126f, viewRect.width - 24f, 114f));
        GUI.EndScrollView();

        FishingUiTheme.ColoredLabel(new Rect(panel.x + 22f, panel.yMax - 58f, panel.width - 44f, 26f),
            statusMessage, FishingUiTheme.BodyCentered, FishingUiTheme.Gold);
        GUI.Label(new Rect(panel.x + 22f, panel.yMax - 32f, panel.width - 44f, 22f), "ESC 또는 오른쪽 위 × 버튼으로 닫기", FishingUiTheme.SmallCentered);
    }

    private void DrawProduct(FishingRodDefinition product, Rect row)
    {
        if (product == null) return;
        bool owned = inventory != null && inventory.Owns(product.Id);
        bool equipped = inventory != null && inventory.EquippedRodId == product.Id;
        FishingUiTheme.DrawSlot(row, product.RarityColor, equipped);
        Rect iconRect = new(row.x + 14f, row.y + 18f, 64f, 64f);
        FishingUiTheme.DrawTextureOrSwatch(iconRect, product.Icon, product.RarityColor);
        GUI.Label(new Rect(row.x + 92f, row.y + 8f, row.width - 252f, 28f), product.DisplayName, FishingUiTheme.Heading);
        FishingUiTheme.DrawBadge(new Rect(row.x + 92f, row.y + 37f, 118f, 25f), product.RarityName, product.RarityColor);
        GUI.Label(new Rect(row.x + 92f, row.y + 63f, row.width - 252f, 40f), product.StatSummary, FishingUiTheme.Small);
        FishingUiTheme.ColoredLabel(new Rect(row.xMax - 138f, row.y + 12f, 120f, 24f),
            $"{product.Price:N0} G", FishingUiTheme.BodyCentered, FishingUiTheme.Gold);

        string buttonText = equipped ? "장착 중" : owned ? "보유 중" : product.Price <= 0 ? "기본 지급" : "구매";
        if (FishingUiTheme.StyledButton(new Rect(row.xMax - 138f, row.y + 52f, 120f, 40f), buttonText,
            primary: !owned && product.Price > 0, enabled: !owned && product.Price > 0))
            TryPurchase(product.Id);
    }
}
