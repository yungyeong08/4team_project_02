using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Handles fishing-shop proximity, input, product presentation, and purchases.</summary>
public sealed class FishingShopSystem : MonoBehaviour
{
    [Serializable]
    public sealed class ShopProduct
    {
        public string Id;
        public string DisplayName;
        [Min(0)] public int Price;
        [TextArea] public string Description;

        public ShopProduct(string id, string displayName, int price, string description)
        {
            Id = id;
            DisplayName = displayName;
            Price = price;
            Description = description;
        }
    }

    [SerializeField, Min(.5f)] private float interactionRange = 1.8f;
    [SerializeField] private List<ShopProduct> products = new();

    private PixelCharacter player;
    private Transform shopTarget;
    private PlayerWallet wallet;
    private FishingRodInventory inventory;
    private Vector2 scrollPosition;
    private bool isOpen;
    private bool isPlayerInRange;
    private string statusMessage = "상품을 선택해 주세요.";
    private GUIStyle titleStyle;
    private GUIStyle centeredStyle;
    private GUIStyle productNameStyle;
    private GUIStyle messageStyle;
    private float logicalScreenWidth;
    private float logicalScreenHeight;

    public bool IsOpen => isOpen;
    public bool IsPlayerInRange => isPlayerInRange;
    public int ProductCount => products.Count;
    public string StatusMessage => statusMessage;

    public void Setup(PixelCharacter playerCharacter, Transform target, PlayerWallet playerWallet, FishingRodInventory rodInventory)
    {
        player = playerCharacter;
        shopTarget = target;
        wallet = playerWallet;
        inventory = rodInventory;

        if (products.Count == 0)
        {
            products.Add(new ShopProduct(
                "sturdy_rod",
                "튼튼한 낚싯대",
                500,
                "기본 낚싯대보다 안정적으로 사용할 수 있는 낚싯대입니다."));
        }

        RefreshRange();
    }

    private void Update()
    {
        RefreshRange();

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
        ShopProduct product = products.Find(candidate => candidate.Id == productId);
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
        EnsureStyles();

        if (!isOpen)
        {
            if (isPlayerInRange) DrawInteractionPrompt();
            GUI.matrix = previousMatrix;
            return;
        }

        DrawShopWindow();
        GUI.matrix = previousMatrix;
    }

    private void DrawInteractionPrompt()
    {
        var prompt = new Rect(logicalScreenWidth * .5f - 145f, logicalScreenHeight - 94f, 290f, 46f);
        GUI.Box(prompt, "");
        GUI.Label(prompt, "F - 상점 열기", centeredStyle);
    }

    private void DrawShopWindow()
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, .62f);
        GUI.Box(new Rect(0f, 0f, logicalScreenWidth, logicalScreenHeight), "");
        GUI.color = previousColor;

        float width = Mathf.Min(720f, logicalScreenWidth - 36f);
        float height = Mathf.Min(500f, logicalScreenHeight - 36f);
        var panel = new Rect((logicalScreenWidth - width) * .5f, (logicalScreenHeight - height) * .5f, width, height);
        GUI.Box(panel, "");

        GUI.Label(new Rect(panel.x + 24f, panel.y + 15f, panel.width - 96f, 42f), "낚시 상점", titleStyle);
        if (GUI.Button(new Rect(panel.xMax - 52f, panel.y + 14f, 36f, 32f), "X")) CloseShop();

        int balance = wallet != null ? wallet.Balance : 0;
        GUI.Label(new Rect(panel.x + 25f, panel.y + 58f, panel.width - 50f, 28f), $"보유 금액: {balance:N0} G", productNameStyle);

        var viewRect = new Rect(panel.x + 20f, panel.y + 94f, panel.width - 40f, panel.height - 160f);
        float contentHeight = Mathf.Max(viewRect.height - 4f, products.Count * 112f);
        scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, new Rect(0f, 0f, viewRect.width - 18f, contentHeight));
        for (int i = 0; i < products.Count; i++) DrawProduct(products[i], new Rect(2f, i * 112f, viewRect.width - 24f, 100f));
        GUI.EndScrollView();

        GUI.Label(new Rect(panel.x + 22f, panel.yMax - 58f, panel.width - 44f, 26f), statusMessage, messageStyle);
        GUI.Label(new Rect(panel.x + 22f, panel.yMax - 32f, panel.width - 44f, 22f), "ESC 또는 오른쪽 위 X 버튼으로 닫기", centeredStyle);
    }

    private void DrawProduct(ShopProduct product, Rect row)
    {
        GUI.Box(row, "");
        GUI.Label(new Rect(row.x + 15f, row.y + 10f, row.width - 160f, 28f), product.DisplayName, productNameStyle);
        GUI.Label(new Rect(row.x + 15f, row.y + 40f, row.width - 160f, 45f), product.Description);
        GUI.Label(new Rect(row.xMax - 138f, row.y + 12f, 120f, 24f), $"{product.Price:N0} G", centeredStyle);

        bool owned = inventory != null && inventory.Owns(product.Id);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = !owned;
        if (GUI.Button(new Rect(row.xMax - 138f, row.y + 48f, 120f, 36f), owned ? "구매 완료" : "구매"))
            TryPurchase(product.Id);
        GUI.enabled = previousEnabled;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 25,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        centeredStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            normal = { textColor = Color.white }
        };
        productNameStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        messageStyle = new GUIStyle(centeredStyle)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(.95f, .82f, .3f) }
        };
    }
}
