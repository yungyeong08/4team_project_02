using System.Collections.Generic;
using UnityEngine;

/// <summary>Fish-sale shop proximity, UI, individual selling, and sell-all behavior.</summary>
public sealed class FishMarketSystem : MonoBehaviour
{
    [SerializeField, Min(.5f)] private float interactionRange = 1.8f;

    private PixelCharacter player;
    private Transform marketTarget;
    private PlayerWallet wallet;
    private FishInventory inventory;
    private List<FishDefinition> catalog;
    private Vector2 scrollPosition;
    private bool isOpen;
    private bool isPlayerInRange;
    private string statusMessage = "판매할 물고기를 선택해 주세요.";
    private GUIStyle titleStyle;
    private GUIStyle centeredStyle;
    private GUIStyle nameStyle;
    private GUIStyle messageStyle;
    private float logicalScreenWidth;
    private float logicalScreenHeight;

    public bool IsOpen => isOpen;
    public bool IsPlayerInRange => isPlayerInRange;
    public string StatusMessage => statusMessage;
    public int TotalSaleValue => CalculateTotalSaleValue();

    public void Setup(
        PixelCharacter playerCharacter, Transform target, PlayerWallet playerWallet,
        FishInventory fishInventory, List<FishDefinition> fishCatalog)
    {
        player = playerCharacter;
        marketTarget = target;
        wallet = playerWallet;
        inventory = fishInventory;
        catalog = fishCatalog;
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
        isPlayerInRange = player != null && marketTarget != null &&
            Vector2.Distance(player.transform.position, marketTarget.position) <= interactionRange;
    }

    public bool OpenShop()
    {
        if (isOpen || !isPlayerInRange) return false;
        isOpen = true;
        statusMessage = inventory != null && inventory.TotalCount > 0
            ? "판매할 물고기를 선택해 주세요."
            : "판매할 물고기가 없습니다.";
        return true;
    }

    public void CloseShop()
    {
        isOpen = false;
        statusMessage = "판매할 물고기를 선택해 주세요.";
    }

    public bool SellFish(string fishId)
    {
        FishDefinition fish = catalog?.Find(candidate => candidate.Id == fishId);
        if (fish == null || inventory == null || wallet == null)
        {
            statusMessage = "물고기 정보를 불러오지 못했습니다.";
            return false;
        }

        // Remove first so a second click cannot pay for the same stack again.
        int soldCount = inventory.RemoveAll(fish.Id);
        if (soldCount <= 0)
        {
            statusMessage = "판매할 물고기가 없습니다.";
            return false;
        }

        int earned = soldCount * fish.SalePrice;
        wallet.AddMoney(earned);
        statusMessage = $"{fish.DisplayName} {soldCount}마리 판매 완료!  +{earned:N0} G";
        return true;
    }

    public int SellAllFish()
    {
        if (catalog == null || inventory == null || wallet == null)
        {
            statusMessage = "판매 정보를 불러오지 못했습니다.";
            return 0;
        }

        int earned = 0;
        foreach (FishDefinition fish in catalog)
        {
            int soldCount = inventory.RemoveAll(fish.Id);
            earned += soldCount * fish.SalePrice;
        }

        if (earned <= 0)
        {
            statusMessage = "판매할 물고기가 없습니다.";
            return 0;
        }

        wallet.AddMoney(earned);
        statusMessage = $"모든 물고기 판매 완료!  +{earned:N0} G";
        return earned;
    }

    private int CalculateTotalSaleValue()
    {
        if (catalog == null || inventory == null) return 0;
        int total = 0;
        foreach (FishDefinition fish in catalog)
            total += inventory.GetCount(fish.Id) * fish.SalePrice;
        return total;
    }

    private void OnGUI()
    {
        if (player == null || marketTarget == null) return;
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

        DrawMarketWindow();
        GUI.matrix = previousMatrix;
    }

    private void DrawInteractionPrompt()
    {
        var prompt = new Rect(logicalScreenWidth * .5f - 150f, logicalScreenHeight - 94f, 300f, 46f);
        GUI.Box(prompt, "");
        GUI.Label(prompt, "F - 물고기 판매", centeredStyle);
    }

    private void DrawMarketWindow()
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, .64f);
        GUI.Box(new Rect(0f, 0f, logicalScreenWidth, logicalScreenHeight), "");
        GUI.color = oldColor;

        float width = Mathf.Min(900f, logicalScreenWidth - 30f);
        float height = Mathf.Min(650f, logicalScreenHeight - 30f);
        var panel = new Rect((logicalScreenWidth - width) * .5f, (logicalScreenHeight - height) * .5f, width, height);
        GUI.Box(panel, "");

        GUI.Label(new Rect(panel.x + 24f, panel.y + 14f, panel.width - 100f, 42f), "물고기 판매 상점", titleStyle);
        if (GUI.Button(new Rect(panel.xMax - 52f, panel.y + 14f, 36f, 32f), "X")) CloseShop();
        GUI.Label(new Rect(panel.x + 25f, panel.y + 58f, 250f, 28f), $"보유 금액: {(wallet != null ? wallet.Balance : 0):N0} G", nameStyle);

        var viewRect = new Rect(panel.x + 20f, panel.y + 92f, panel.width - 40f, panel.height - 205f);
        int visibleCount = 0;
        if (catalog != null)
            foreach (FishDefinition fish in catalog)
                if (inventory != null && inventory.GetCount(fish.Id) > 0) visibleCount++;

        float contentHeight = Mathf.Max(viewRect.height - 4f, visibleCount * 112f);
        scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, new Rect(0f, 0f, viewRect.width - 18f, contentHeight));
        int rowIndex = 0;
        if (catalog != null)
        {
            foreach (FishDefinition fish in catalog)
            {
                int count = inventory != null ? inventory.GetCount(fish.Id) : 0;
                if (count <= 0) continue;
                DrawFishRow(fish, count, new Rect(2f, rowIndex * 112f, viewRect.width - 24f, 100f));
                rowIndex++;
            }
        }
        if (rowIndex == 0) GUI.Label(new Rect(0f, 50f, viewRect.width - 20f, 40f), "판매할 물고기가 없습니다.", centeredStyle);
        GUI.EndScrollView();

        int total = CalculateTotalSaleValue();
        GUI.Label(new Rect(panel.x + 24f, panel.yMax - 102f, panel.width - 210f, 32f), $"전체 판매 금액: {total:N0} G", nameStyle);
        bool oldEnabled = GUI.enabled;
        GUI.enabled = total > 0;
        if (GUI.Button(new Rect(panel.xMax - 170f, panel.yMax - 106f, 145f, 38f), "모두 팔기")) SellAllFish();
        GUI.enabled = oldEnabled;
        GUI.Label(new Rect(panel.x + 22f, panel.yMax - 64f, panel.width - 44f, 26f), statusMessage, messageStyle);
        GUI.Label(new Rect(panel.x + 22f, panel.yMax - 34f, panel.width - 44f, 22f), "ESC 또는 오른쪽 위 X 버튼으로 닫기", centeredStyle);
    }

    private void DrawFishRow(FishDefinition fish, int count, Rect row)
    {
        GUI.Box(row, "");
        if (fish.Icon != null) GUI.DrawTexture(new Rect(row.x + 12f, row.y + 12f, 76f, 76f), fish.Icon, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(row.x + 102f, row.y + 8f, 230f, 27f), fish.DisplayName, nameStyle);
        GUI.Label(new Rect(row.x + 102f, row.y + 36f, 230f, 24f), $"등급: {fish.RarityName}");
        GUI.Label(new Rect(row.x + 102f, row.y + 62f, 180f, 24f), $"보유: {count}마리");
        GUI.Label(new Rect(row.x + 315f, row.y + 17f, 190f, 24f), $"개당: {fish.SalePrice:N0} G");
        GUI.Label(new Rect(row.x + 315f, row.y + 52f, 190f, 24f), $"총액: {count * fish.SalePrice:N0} G", nameStyle);
        if (GUI.Button(new Rect(row.xMax - 112f, row.y + 30f, 94f, 40f), "판매")) SellFish(fish.Id);
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
        nameStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        messageStyle = new GUIStyle(centeredStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(.95f, .82f, .3f) }
        };
    }
}
