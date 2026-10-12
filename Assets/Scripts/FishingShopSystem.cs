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

    public bool IsOpen => isOpen;
    public bool IsPlayerInRange => isPlayerInRange;
    public int ProductCount => rodCatalog?.Count ?? 0;
    public string StatusMessage => statusMessage;

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

    private void Update()
    {
        RefreshRange();

        if (!isOpen && inputBlocked != null && inputBlocked()) return;

        if (isOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseShop();
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
        statusMessage = "상품을 선택해 주세요.";
        return true;
    }

    public void CloseShop()
    {
        isOpen = false;
        statusMessage = "상품을 선택해 주세요.";
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
            if (isPlayerInRange && !blocked) DrawInteractionPrompt();
            GUI.matrix = previousMatrix;
            return;
        }

        DrawShopWindow();
        GUI.matrix = previousMatrix;
    }

    private void DrawInteractionPrompt()
    {
        var prompt = new Rect(logicalScreenWidth * .5f - 145f, logicalScreenHeight - 94f, 290f, 46f);
        FishingUiTheme.DrawPanel(prompt, true);
        GUI.Label(prompt, "F  -  낚시 상점 열기", FishingUiTheme.BodyCentered);
    }

    private void DrawShopWindow()
    {
        FishingUiTheme.DrawDim(new Rect(0f, 0f, logicalScreenWidth, logicalScreenHeight));

        float width = Mathf.Min(720f, logicalScreenWidth - 36f);
        float height = Mathf.Min(500f, logicalScreenHeight - 36f);
        var panel = new Rect((logicalScreenWidth - width) * .5f, (logicalScreenHeight - height) * .5f, width, height);
        FishingUiTheme.DrawPanel(panel);

        GUI.Label(new Rect(panel.x + 24f, panel.y + 15f, panel.width - 96f, 42f), "낚시 상점", FishingUiTheme.Title);
        if (FishingUiTheme.StyledButton(new Rect(panel.xMax - 55f, panel.y + 14f, 38f, 34f), "×", danger: true)) CloseShop();
        FishingUiTheme.DrawDivider(new Rect(panel.x + 22f, panel.y + 60f, panel.width - 44f, 2f));

        int balance = wallet != null ? wallet.Balance : 0;
        GUI.Label(new Rect(panel.x + 25f, panel.y + 66f, panel.width - 50f, 28f), $"보유 금액  {balance:N0} G", FishingUiTheme.Heading);

        var viewRect = new Rect(panel.x + 20f, panel.y + 102f, panel.width - 40f, panel.height - 168f);
        int productCount = rodCatalog != null ? rodCatalog.Count : 0;
        float contentHeight = Mathf.Max(viewRect.height - 4f, productCount * 126f);
        scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, new Rect(0f, 0f, viewRect.width - 18f, contentHeight));
        if (rodCatalog != null)
            for (int i = 0; i < rodCatalog.Count; i++)
                DrawProduct(rodCatalog[i], new Rect(2f, i * 126f, viewRect.width - 24f, 114f));
        GUI.EndScrollView();

        FishingUiTheme.ColoredLabel(new Rect(panel.x + 22f, panel.yMax - 58f, panel.width - 44f, 26f),
            statusMessage, FishingUiTheme.BodyCentered, FishingUiTheme.Gold);
        GUI.Label(new Rect(panel.x + 22f, panel.yMax - 32f, panel.width - 44f, 22f), "ESC 또는 오른쪽 위 × 버튼으로 닫기", FishingUiTheme.SmallCentered);
    }

    private void DrawProduct(FishingRodDefinition product, Rect row)
    {
        if (product == null) return;
        FishingUiTheme.DrawCard(row, false);
        Rect iconRect = new(row.x + 14f, row.y + 18f, 64f, 64f);
        FishingUiTheme.DrawTextureOrSwatch(iconRect, product.Icon, product.RarityColor);
        GUI.Label(new Rect(row.x + 92f, row.y + 8f, row.width - 252f, 28f), product.DisplayName, FishingUiTheme.Heading);
        FishingUiTheme.ColoredLabel(new Rect(row.x + 92f, row.y + 37f, 160f, 24f), product.RarityName, FishingUiTheme.Body, product.RarityColor);
        GUI.Label(new Rect(row.x + 92f, row.y + 63f, row.width - 252f, 40f), product.StatSummary, FishingUiTheme.Small);
        FishingUiTheme.ColoredLabel(new Rect(row.xMax - 138f, row.y + 12f, 120f, 24f),
            $"{product.Price:N0} G", FishingUiTheme.BodyCentered, FishingUiTheme.Gold);

        bool owned = inventory != null && inventory.Owns(product.Id);
        bool equipped = inventory != null && inventory.EquippedRodId == product.Id;
        string buttonText = equipped ? "장착 중" : owned ? "보유 중" : product.Price <= 0 ? "기본 지급" : "구매";
        if (FishingUiTheme.StyledButton(new Rect(row.xMax - 138f, row.y + 52f, 120f, 40f), buttonText,
            primary: !owned && product.Price > 0, enabled: !owned && product.Price > 0))
            TryPurchase(product.Id);
    }
}
