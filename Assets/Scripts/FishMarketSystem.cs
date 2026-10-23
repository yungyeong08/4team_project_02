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
    private System.Func<bool> inputBlocked;
    private Vector2 scrollPosition;
    private bool isOpen;
    private bool isPlayerInRange;
    private string statusMessage = "판매할 물고기를 선택해 주세요.";
    private float logicalScreenWidth;
    private float logicalScreenHeight;
    private FishingAudioDirector audioDirector;
    private float promptVisibility;
    private float windowReveal;
    private bool windowClosing;

    public bool IsOpen => isOpen;
    public bool IsPlayerInRange => isPlayerInRange;
    public string StatusMessage => statusMessage;
    public int TotalSaleValue => CalculateTotalSaleValue();
    public event System.Action<int, int> FishSold;

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
                statusMessage = "판매할 물고기를 선택해 주세요.";
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
        isPlayerInRange = player != null && marketTarget != null &&
            Vector2.Distance(player.transform.position, marketTarget.position) <= interactionRange;
    }

    public bool OpenShop()
    {
        if (isOpen || !isPlayerInRange) return false;
        isOpen = true;
        windowClosing = false;
        windowReveal = 0f;
        statusMessage = inventory != null && inventory.TotalCount > 0
            ? "판매할 물고기를 선택해 주세요."
            : "판매할 물고기가 없습니다.";
        audioDirector?.PlayMenuOpen();
        return true;
    }

    public void CloseShop()
    {
        if (!isOpen || windowClosing) return;
        audioDirector?.PlayMenuClose();
        windowClosing = true;
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

        int earned = SafeSaleValue(soldCount, fish.SalePrice);
        wallet.AddMoney(earned);
        audioDirector?.PlaySell(false);
        statusMessage = $"{fish.DisplayName} {soldCount}마리 판매 완료!  +{earned:N0} G";
        FishSold?.Invoke(soldCount, earned);
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
        int totalSold = 0;
        foreach (FishDefinition fish in catalog)
        {
            if (fish == null) continue;
            int soldCount = inventory.RemoveAll(fish.Id);
            totalSold += soldCount;
            earned = (int)System.Math.Min(int.MaxValue, (long)earned + SafeSaleValue(soldCount, fish.SalePrice));
        }

        if (earned <= 0)
        {
            statusMessage = "판매할 물고기가 없습니다.";
            return 0;
        }

        wallet.AddMoney(earned);
        audioDirector?.PlaySell(true);
        statusMessage = $"모든 물고기 판매 완료!  +{earned:N0} G";
        FishSold?.Invoke(totalSold, earned);
        return earned;
    }

    private int CalculateTotalSaleValue()
    {
        if (catalog == null || inventory == null) return 0;
        int total = 0;
        foreach (FishDefinition fish in catalog)
        {
            if (fish == null) continue;
            total = (int)System.Math.Min(int.MaxValue,
                (long)total + SafeSaleValue(inventory.GetCount(fish.Id), fish.SalePrice));
        }
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
        DrawMarketWindow();
        GUI.enabled = previousEnabled;
        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    private void DrawInteractionPrompt()
    {
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, promptVisibility);
        var prompt = new Rect(logicalScreenWidth * .5f - 158f, logicalScreenHeight - 104f, 316f, 56f);
        FishingUiTheme.DrawInteractionPrompt(prompt, "F", "물고기 판매");
        GUI.color = old;
    }

    private void DrawMarketWindow()
    {
        FishingUiTheme.DrawDim(new Rect(0f, 0f, logicalScreenWidth, logicalScreenHeight));

        float width = Mathf.Min(900f, logicalScreenWidth - 30f);
        float height = Mathf.Min(650f, logicalScreenHeight - 30f);
        var panel = new Rect((logicalScreenWidth - width) * .5f, (logicalScreenHeight - height) * .5f, width, height);
        FishingUiTheme.DrawPanel(panel);

        FishingUiTheme.DrawTitlePlate(new Rect(panel.x + 230f, panel.y - 14f, panel.width - 460f, 56f), "물고기 판매소");
        if (FishingUiTheme.StyledButton(new Rect(panel.xMax - 57f, panel.y + 15f, 40f, 36f), "×", danger: true))
        {
            audioDirector?.PlayUiClick();
            CloseShop();
        }
        FishingUiTheme.DrawMoneyChip(new Rect(panel.x + 24f, panel.y + 59f, 200f, 40f), wallet != null ? wallet.Balance : 0);
        GUI.Label(new Rect(panel.x + 240f, panel.y + 61f, panel.width - 264f, 36f),
            "잡은 물고기를 골드로 바꿔 장비를 마련하세요", FishingUiTheme.Small);

        var viewRect = new Rect(panel.x + 20f, panel.y + 108f, panel.width - 40f, panel.height - 221f);
        int visibleCount = 0;
        if (catalog != null)
            foreach (FishDefinition fish in catalog)
                if (fish != null && inventory != null && inventory.GetCount(fish.Id) > 0) visibleCount++;

        float contentHeight = Mathf.Max(viewRect.height - 4f, visibleCount * 112f);
        scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, new Rect(0f, 0f, viewRect.width - 18f, contentHeight));
        int rowIndex = 0;
        if (catalog != null)
        {
            foreach (FishDefinition fish in catalog)
            {
                if (fish == null) continue;
                int count = inventory != null ? inventory.GetCount(fish.Id) : 0;
                if (count <= 0) continue;
                DrawFishRow(fish, count, new Rect(2f, rowIndex * 112f, viewRect.width - 24f, 100f));
                rowIndex++;
            }
        }
        if (rowIndex == 0) GUI.Label(new Rect(0f, 50f, viewRect.width - 20f, 40f), "판매할 물고기가 없습니다.", FishingUiTheme.BodyCentered);
        GUI.EndScrollView();

        int total = CalculateTotalSaleValue();
        GUI.Label(new Rect(panel.x + 24f, panel.yMax - 102f, panel.width - 210f, 32f), $"전체 판매 금액  {total:N0} G", FishingUiTheme.Heading);
        if (FishingUiTheme.StyledButton(new Rect(panel.xMax - 170f, panel.yMax - 106f, 145f, 40f), "모두 팔기", primary: true, enabled: total > 0)) SellAllFish();
        FishingUiTheme.ColoredLabel(new Rect(panel.x + 22f, panel.yMax - 64f, panel.width - 44f, 26f),
            statusMessage, FishingUiTheme.BodyCentered, FishingUiTheme.Gold);
        GUI.Label(new Rect(panel.x + 22f, panel.yMax - 34f, panel.width - 44f, 22f), "ESC 또는 오른쪽 위 × 버튼으로 닫기", FishingUiTheme.SmallCentered);
    }

    private void DrawFishRow(FishDefinition fish, int count, Rect row)
    {
        if (fish == null) return;
        FishingUiTheme.DrawSlot(row, fish.RarityColor);
        FishingUiTheme.DrawTextureOrSwatch(new Rect(row.x + 12f, row.y + 12f, 76f, 76f), fish.Icon, fish.RarityColor);
        GUI.Label(new Rect(row.x + 102f, row.y + 8f, 230f, 27f), fish.DisplayName, FishingUiTheme.Heading);
        FishingUiTheme.DrawBadge(new Rect(row.x + 102f, row.y + 36f, 118f, 25f), fish.RarityName, fish.RarityColor);
        GUI.Label(new Rect(row.x + 102f, row.y + 62f, 180f, 24f), $"보유  {count}마리", FishingUiTheme.Small);
        GUI.Label(new Rect(row.x + 315f, row.y + 17f, 190f, 24f), $"개당  {fish.SalePrice:N0} G", FishingUiTheme.Body);
        FishingUiTheme.ColoredLabel(new Rect(row.x + 315f, row.y + 52f, 190f, 24f),
            $"총액  {SafeSaleValue(count, fish.SalePrice):N0} G", FishingUiTheme.Heading, FishingUiTheme.Gold);
        if (FishingUiTheme.StyledButton(new Rect(row.xMax - 112f, row.y + 30f, 94f, 42f), "판매", primary: true)) SellFish(fish.Id);
    }

    private static int SafeSaleValue(int count, int price)
        => (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, (long)Mathf.Max(0, count) * Mathf.Max(0, price)));
}
