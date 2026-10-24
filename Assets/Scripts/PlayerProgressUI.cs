using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared temporary IMGUI for inventory, fish codex, equipment, pause, and main menu.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class PlayerProgressUI : MonoBehaviour
{
    private enum WindowMode { None, Inventory, Codex, Pause, MainMenu, ResetConfirm }
    private enum InventoryTab { Fish, Rods }

    private PlayerWallet wallet;
    private FishInventory fishInventory;
    private FishingRodInventory rodInventory;
    private List<FishDefinition> fishCatalog;
    private List<FishingRodDefinition> rodCatalog;
    private GameSaveSystem saveSystem;
    private FishingGameController fishingController;
    private FishingShopSystem shop;
    private FishMarketSystem market;
    private EarlyGameQuestSystem earlyGameProgress;
    private PlayerDebt playerDebt;
    private WindowMode windowMode;
    private InventoryTab inventoryTab;
    private Vector2 scrollPosition;
    private string statusMessage = "";
    private int lastBalance;
    private int balanceDelta;
    private float balanceFeedbackTimer;
    private FishingAudioDirector audioDirector;
    private float modalReveal;
    private bool modalClosing;

    public bool IsBlockingGameplay => windowMode != WindowMode.None;
    public bool IsInventoryOpen => windowMode == WindowMode.Inventory;
    public bool IsCodexOpen => windowMode == WindowMode.Codex;
    public bool IsPauseOpen => windowMode == WindowMode.Pause || windowMode == WindowMode.MainMenu || windowMode == WindowMode.ResetConfirm;

    public void Setup(
        PlayerWallet playerWallet,
        FishInventory playerFishInventory,
        FishingRodInventory playerRodInventory,
        List<FishDefinition> availableFish,
        List<FishingRodDefinition> availableRods,
        GameSaveSystem gameSaveSystem,
        FishingGameController controller,
        FishingShopSystem shopSystem,
        FishMarketSystem marketSystem,
        EarlyGameQuestSystem questSystem,
        PlayerDebt debt)
    {
        wallet = playerWallet;
        fishInventory = playerFishInventory;
        rodInventory = playerRodInventory;
        fishCatalog = availableFish;
        rodCatalog = availableRods;
        saveSystem = gameSaveSystem;
        fishingController = controller;
        shop = shopSystem;
        market = marketSystem;
        earlyGameProgress = questSystem;
        playerDebt = debt;
        lastBalance = wallet != null ? wallet.Balance : 0;
        if (wallet != null) wallet.BalanceChanged += OnBalanceChanged;
    }

    public void SetAudio(FishingAudioDirector audio) => audioDirector = audio;

    private void OnBalanceChanged(int newBalance)
    {
        balanceDelta = newBalance - lastBalance;
        lastBalance = newBalance;
        if (balanceDelta != 0) balanceFeedbackTimer = 1.25f;
    }

    private void Update()
    {
        if (balanceFeedbackTimer > 0f) balanceFeedbackTimer -= Time.unscaledDeltaTime;
        if (windowMode != WindowMode.None)
        {
            modalReveal = Mathf.MoveTowards(modalReveal, modalClosing ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            if (modalClosing && modalReveal <= 0f)
            {
                windowMode = WindowMode.None;
                modalClosing = false;
                Time.timeScale = 1f;
            }
        }
        if (fishingController != null && fishingController.IsCinematicActive) return;
        if (modalClosing) return;
        bool externalWindowOpen = (shop != null && shop.IsOpen) || (market != null && market.IsOpen);
        bool fishingActive = fishingController != null && fishingController.IsFishingActive;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (windowMode == WindowMode.ResetConfirm) OpenWindow(WindowMode.Pause);
            else if (windowMode != WindowMode.None) CloseWindow();
            else if (!externalWindowOpen && !fishingActive) OpenWindow(WindowMode.Pause);
            return;
        }

        if (externalWindowOpen || fishingActive || windowMode == WindowMode.Pause ||
            windowMode == WindowMode.MainMenu || windowMode == WindowMode.ResetConfirm) return;
        if (Input.GetKeyDown(KeyCode.I)) ToggleWindow(WindowMode.Inventory);
        else if (Input.GetKeyDown(KeyCode.C)) ToggleWindow(WindowMode.Codex);
    }

    private void ToggleWindow(WindowMode mode)
    {
        if (windowMode == mode) CloseWindow();
        else OpenWindow(mode);
    }

    private void OpenWindow(WindowMode mode)
    {
        audioDirector?.PlayMenuOpen();
        windowMode = mode;
        modalClosing = false;
        modalReveal = 0f;
        scrollPosition = Vector2.zero;
        statusMessage = "";
        if (mode == WindowMode.Pause || mode == WindowMode.MainMenu || mode == WindowMode.ResetConfirm) Time.timeScale = 0f;
        if (mode == WindowMode.Inventory) earlyGameProgress?.NotifyInventoryOpened();
    }

    private void CloseWindow()
    {
        if (windowMode == WindowMode.None || modalClosing) return;
        audioDirector?.PlayMenuClose();
        modalClosing = true;
    }

    private void OnDisable()
    {
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (wallet != null) wallet.BalanceChanged -= OnBalanceChanged;
    }

    private void OnGUI()
    {
        if (fishingController != null && fishingController.IsCinematicActive) return;
        float uiScale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float screenWidth = Screen.width / uiScale;
        float screenHeight = Screen.height / uiScale;
        FishingUiTheme.Ensure();

        if (windowMode == WindowMode.None)
        {
            DrawShortcutHint(screenWidth, screenHeight);
            GUI.matrix = previousMatrix;
            return;
        }

        float reveal = Mathf.SmoothStep(0f, 1f, modalReveal);
        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, reveal);
        FishingUiTheme.DrawDim(new Rect(0f, 0f, screenWidth, screenHeight));

        if (windowMode == WindowMode.Inventory) DrawInventory(screenWidth, screenHeight);
        else if (windowMode == WindowMode.Codex) DrawCodex(screenWidth, screenHeight);
        else if (windowMode == WindowMode.Pause) DrawPause(screenWidth, screenHeight);
        else if (windowMode == WindowMode.MainMenu) DrawMainMenu(screenWidth, screenHeight);
        else DrawResetConfirm(screenWidth, screenHeight);

        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    private void DrawShortcutHint(float screenWidth, float screenHeight)
    {
        if ((shop != null && shop.IsOpen) || (market != null && market.IsOpen) ||
            (fishingController != null && (fishingController.IsFishingActive || fishingController.IsResultVisible))) return;

        FishingHudSettings hud = FishingUiTheme.Settings;
        FishingRodDefinition equipped = rodInventory?.GetEquipped(rodCatalog);
        float sideWidth = Mathf.Min(286f, (screenWidth - hud.ScreenMargin * 4f) * .25f);
        var statusPanel = new Rect(hud.ScreenMargin, hud.ScreenMargin, sideWidth, 82f);
        Color rodColor = equipped != null ? equipped.RarityColor : FishingUiTheme.Muted;
        FishingUiTheme.DrawHudPanel(statusPanel, "", rodColor);
        FishingUiTheme.DrawMoneyChip(new Rect(statusPanel.x + 14f, statusPanel.y + 11f, 154f, 30f),
            wallet != null ? wallet.Balance : 0);
        if (balanceFeedbackTimer > 0f && balanceDelta != 0)
        {
            float alpha = Mathf.Clamp01(balanceFeedbackTimer * 2f);
            Color color = balanceDelta > 0 ? FishingUiTheme.Gold : FishingUiTheme.Danger;
            color.a *= alpha;
            FishingUiTheme.ColoredLabel(new Rect(statusPanel.x + 174f, statusPanel.y + 12f, statusPanel.width - 184f, 28f),
                $"{(balanceDelta > 0 ? "+" : "")}{balanceDelta:N0} G", FishingUiTheme.Heading, color);
        }

        var rodChip = new Rect(statusPanel.x + 14f, statusPanel.y + 45f, statusPanel.width - 28f, 28f);
        FishingUiTheme.DrawCard(rodChip, true);
        FishingUiTheme.DrawHudIcon(new Rect(rodChip.x + 7f, rodChip.y + 3f, 22f, 22f), FishingUiTheme.HudIcon.Rod, rodColor);
        FishingUiTheme.ColoredLabel(new Rect(rodChip.x + 38f, rodChip.y, rodChip.width - 48f, rodChip.height),
            equipped != null ? equipped.DisplayName : "낚싯대 없음", FishingUiTheme.Body, rodColor);

        const float buttonWidth = 64f;
        const float buttonHeight = 66f;
        const float gap = 8f;
        float menuY = screenHeight - hud.ScreenMargin - buttonHeight;
        float menuX = hud.ScreenMargin;
        if (FishingUiTheme.DrawShortcutButton(new Rect(menuX, menuY, buttonWidth, buttonHeight),
                FishingUiTheme.HudIcon.Bag, "I"))
        {
            audioDirector?.PlayUiClick();
            OpenWindow(WindowMode.Inventory);
        }
        if (FishingUiTheme.DrawShortcutButton(new Rect(menuX + buttonWidth + gap, menuY, buttonWidth, buttonHeight),
                FishingUiTheme.HudIcon.Book, "C"))
        {
            audioDirector?.PlayUiClick();
            OpenWindow(WindowMode.Codex);
        }
        if (FishingUiTheme.DrawShortcutButton(new Rect(menuX + (buttonWidth + gap) * 2f, menuY, buttonWidth, buttonHeight),
                FishingUiTheme.HudIcon.Menu, "ESC"))
        {
            audioDirector?.PlayUiClick();
            OpenWindow(WindowMode.Pause);
        }
    }

    private Rect BeginLargePanel(float screenWidth, float screenHeight, string title)
    {
        float width = Mathf.Min(920f, screenWidth - 34f);
        float height = Mathf.Min(610f, screenHeight - 34f);
        var panel = new Rect((screenWidth - width) * .5f, (screenHeight - height) * .5f, width, height);
        FishingUiTheme.DrawPanel(panel);
        float plateWidth = Mathf.Min(430f, panel.width - 180f);
        FishingUiTheme.DrawTitlePlate(new Rect(panel.x + (panel.width - plateWidth) * .5f, panel.y - 14f, plateWidth, 56f), title);
        if (FishingUiTheme.StyledButton(new Rect(panel.xMax - 59f, panel.y + 15f, 40f, 36f), "×", danger: true))
        {
            audioDirector?.PlayUiClick();
            CloseWindow();
        }
        return panel;
    }

    private void DrawInventory(float screenWidth, float screenHeight)
    {
        Rect panel = BeginLargePanel(screenWidth, screenHeight, "인벤토리");
        FishingUiTheme.DrawMoneyChip(new Rect(panel.x + 24f, panel.y + 58f, 190f, 40f), wallet != null ? wallet.Balance : 0);
        if (GUI.Button(new Rect(panel.x + 24f, panel.y + 106f, 170f, 40f), "물고기 가방", inventoryTab == InventoryTab.Fish ? FishingUiTheme.ActiveTab : FishingUiTheme.Tab))
        {
            audioDirector?.PlayUiClick();
            inventoryTab = InventoryTab.Fish;
            scrollPosition = Vector2.zero;
        }
        if (GUI.Button(new Rect(panel.x + 202f, panel.y + 106f, 170f, 40f), "낚싯대 장비", inventoryTab == InventoryTab.Rods ? FishingUiTheme.ActiveTab : FishingUiTheme.Tab))
        {
            audioDirector?.PlayUiClick();
            inventoryTab = InventoryTab.Rods;
            scrollPosition = Vector2.zero;
        }

        var view = new Rect(panel.x + 22f, panel.y + 156f, panel.width - 44f, panel.height - 212f);
        int rowCount = inventoryTab == InventoryTab.Fish ? GetOwnedFishTypeCount() : rodInventory?.OwnedRodCount ?? 0;
        float contentHeight = inventoryTab == InventoryTab.Fish
            ? CalculateFishGridHeight(view.width - 24f, rowCount)
            : rowCount * 126f;
        contentHeight = Mathf.Max(view.height - 4f, contentHeight);
        scrollPosition = GUI.BeginScrollView(view, scrollPosition, new Rect(0f, 0f, view.width - 18f, contentHeight));

        if (inventoryTab == InventoryTab.Fish) DrawFishInventoryRows(view.width - 24f);
        else DrawRodInventoryRows(view.width - 24f);
        GUI.EndScrollView();

        if (!string.IsNullOrEmpty(statusMessage))
            FishingUiTheme.ColoredLabel(new Rect(panel.x + 24f, panel.yMax - 48f, panel.width - 48f, 28f),
                statusMessage, FishingUiTheme.BodyCentered, FishingUiTheme.Gold);
    }

    private void DrawFishInventoryRows(float width)
    {
        int ownedTypeCount = GetOwnedFishTypeCount();
        if (fishCatalog == null || ownedTypeCount == 0)
        {
            float emptyWidth = Mathf.Min(520f, width - 30f);
            var emptyCard = new Rect((width - emptyWidth) * .5f, 72f, emptyWidth, 190f);
            FishingUiTheme.DrawCard(emptyCard, true);
            GUI.Label(new Rect(emptyCard.x + 20f, emptyCard.y + 24f, emptyCard.width - 40f, 52f), "가방이 비어 있습니다", FishingUiTheme.Title);
            GUI.Label(new Rect(emptyCard.x + 45f, emptyCard.y + 82f, emptyCard.width - 90f, 70f),
                "낚시로 잡아서 현재 보유 중인 물고기만\n이 인벤토리에 표시됩니다.", FishingUiTheme.BodyCentered);
            return;
        }

        const float gap = 12f;
        const float preferredWidth = 164f;
        const float slotHeight = 154f;
        int columns = Mathf.Max(2, Mathf.FloorToInt((width + gap) / (preferredWidth + gap)));
        float slotWidth = (width - gap * (columns - 1)) / columns;
        int slotIndex = 0;
        int totalFish = fishInventory != null ? fishInventory.TotalCount : 0;
        GUI.Label(new Rect(4f, 0f, width - 8f, 30f),
            $"보유 물고기  {totalFish}마리   ·   사용 슬롯  {ownedTypeCount}개", FishingUiTheme.Small);

        for (int i = 0; i < fishCatalog.Count; i++)
        {
            FishDefinition fish = fishCatalog[i];
            int count = fish != null && fishInventory != null ? fishInventory.GetCount(fish.Id) : 0;
            if (fish == null || count <= 0) continue;

            int column = slotIndex % columns;
            int rowIndex = slotIndex / columns;
            Rect slot = new(column * (slotWidth + gap), 38f + rowIndex * (slotHeight + gap), slotWidth, slotHeight);
            FishingUiTheme.DrawSlot(slot, fish.RarityColor);

            float iconSize = Mathf.Min(76f, slotWidth - 34f);
            Rect icon = new(slot.x + (slot.width - iconSize) * .5f, slot.y + 12f, iconSize, iconSize);
            FishingUiTheme.DrawTextureOrSwatch(icon, fish.Icon, fish.RarityColor);
            GUI.Label(new Rect(slot.x + 8f, slot.y + 94f, slot.width - 16f, 24f), fish.DisplayName, FishingUiTheme.BodyCentered);
            FishingUiTheme.ColoredLabel(new Rect(slot.x + 8f, slot.y + 119f, slot.width - 16f, 22f),
                fish.RarityName, FishingUiTheme.SmallCentered, fish.RarityColor);

            var countBadge = new Rect(slot.xMax - 54f, slot.y + 58f, 43f, 28f);
            FishingUiTheme.DrawBadge(countBadge, "×" + count, FishingUiTheme.Gold);
            slotIndex++;
        }

        // Keep a visible bag grid even when only a few fish are owned. This makes the
        // screen read as an inventory instead of another encyclopedia-style list.
        int visibleSlots = Mathf.Max(columns * 2, slotIndex);
        while (slotIndex < visibleSlots)
        {
            int column = slotIndex % columns;
            int rowIndex = slotIndex / columns;
            Rect slot = new(column * (slotWidth + gap), 38f + rowIndex * (slotHeight + gap), slotWidth, slotHeight);
            FishingUiTheme.DrawCard(slot, true);
            FishingUiTheme.ColoredLabel(new Rect(slot.x + 10f, slot.y + 54f, slot.width - 20f, 44f),
                "빈 슬롯", FishingUiTheme.SmallCentered, FishingUiTheme.Muted);
            slotIndex++;
        }
    }

    private int GetOwnedFishTypeCount()
    {
        if (fishCatalog == null || fishInventory == null) return 0;
        int count = 0;
        foreach (FishDefinition fish in fishCatalog)
            if (fish != null && fishInventory.GetCount(fish.Id) > 0) count++;
        return count;
    }

    private static float CalculateFishGridHeight(float width, int itemCount)
    {
        if (itemCount <= 0) return 310f;
        const float gap = 12f;
        const float preferredWidth = 164f;
        const float slotHeight = 154f;
        int columns = Mathf.Max(2, Mathf.FloorToInt((width + gap) / (preferredWidth + gap)));
        int visibleItems = Mathf.Max(columns * 2, itemCount);
        int rows = Mathf.CeilToInt(visibleItems / (float)columns);
        return 38f + rows * slotHeight + Mathf.Max(0, rows - 1) * gap + 4f;
    }

    private void DrawRodInventoryRows(float width)
    {
        if (rodCatalog == null || rodInventory == null) return;
        int rowIndex = 0;
        foreach (FishingRodDefinition rod in rodCatalog)
        {
            if (rod == null || !rodInventory.Owns(rod.Id)) continue;
            Rect row = new(2f, rowIndex * 126f, width, 114f);
            bool equipped = rodInventory.EquippedRodId == rod.Id;
            FishingUiTheme.DrawSlot(row, rod.RarityColor, equipped);
            FishingUiTheme.DrawTextureOrSwatch(new Rect(row.x + 14f, row.y + 22f, 68f, 68f), rod.Icon, rod.RarityColor);
            GUI.Label(new Rect(row.x + 98f, row.y + 9f, 300f, 28f), rod.DisplayName, FishingUiTheme.Heading);
            FishingUiTheme.DrawBadge(new Rect(row.x + 98f, row.y + 38f, 120f, 25f), rod.RarityName, rod.RarityColor);
            GUI.Label(new Rect(row.x + 98f, row.y + 65f, row.width - 250f, 38f), rod.StatSummary, FishingUiTheme.Small);

            if (FishingUiTheme.StyledButton(new Rect(row.xMax - 135f, row.y + 37f, 115f, 42f), equipped ? "장착 중" : "장착", primary: !equipped, enabled: !equipped))
            {
                audioDirector?.PlayUiClick();
                rodInventory.TryEquip(rod.Id);
                statusMessage = rod.DisplayName + " 장착 완료";
            }
            rowIndex++;
        }
    }

    private void DrawCodex(float screenWidth, float screenHeight)
    {
        Rect panel = BeginLargePanel(screenWidth, screenHeight, "물고기 도감");
        int discovered = 0;
        if (fishCatalog != null && fishInventory != null)
            foreach (FishDefinition fish in fishCatalog) if (fishInventory.IsDiscovered(fish.Id)) discovered++;
        int catalogCount = fishCatalog != null ? fishCatalog.Count : 0;
        FishingUiTheme.DrawBadge(new Rect(panel.x + 24f, panel.y + 60f, 170f, 36f),
            $"발견  {discovered} / {catalogCount}", FishingUiTheme.WaterBright);
        FishingUiTheme.DrawBar(new Rect(panel.x + 210f, panel.y + 63f, panel.width - 240f, 30f),
            catalogCount > 0 ? (float)discovered / catalogCount : 0f, FishingUiTheme.AccentBright,
            "도감 완성도");

        var view = new Rect(panel.x + 22f, panel.y + 108f, panel.width - 44f, panel.height - 138f);
        int count = fishCatalog?.Count ?? 0;
        scrollPosition = GUI.BeginScrollView(view, scrollPosition, new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height - 4f, count * 112f)));
        for (int i = 0; i < count; i++) DrawCodexRow(fishCatalog[i], new Rect(2f, i * 112f, view.width - 24f, 100f));
        GUI.EndScrollView();
    }

    private void DrawCodexRow(FishDefinition fish, Rect row)
    {
        if (fish == null) return;
        bool discovered = fishInventory != null && fishInventory.IsDiscovered(fish.Id);
        FishingUiTheme.DrawSlot(row, discovered ? fish.RarityColor : FishingUiTheme.Muted);
        if (discovered) FishingUiTheme.DrawTextureOrSwatch(new Rect(row.x + 12f, row.y + 12f, 76f, 76f), fish.Icon, fish.RarityColor);
        else
        {
            FishingUiTheme.DrawTextureOrSwatch(new Rect(row.x + 12f, row.y + 12f, 76f, 76f), null, new Color(.16f, .19f, .18f));
            GUI.Label(new Rect(row.x + 12f, row.y + 12f, 76f, 76f), "?", FishingUiTheme.Title);
        }
        GUI.Label(new Rect(row.x + 104f, row.y + 12f, 300f, 30f), discovered ? fish.DisplayName : "???", FishingUiTheme.Heading);
        if (discovered)
        {
            FishingUiTheme.DrawBadge(new Rect(row.x + 104f, row.y + 45f, 120f, 25f), fish.RarityName, fish.RarityColor);
            GUI.Label(new Rect(row.xMax - 260f, row.y + 34f, 235f, 30f), $"총 포획  {fishInventory.GetLifetimeCaught(fish.Id)}회", FishingUiTheme.Heading);
        }
        else GUI.Label(new Rect(row.x + 104f, row.y + 47f, 300f, 25f), "아직 발견하지 못했습니다.", FishingUiTheme.Small);
    }

    private void DrawPause(float screenWidth, float screenHeight)
    {
        Rect panel = CenterPanel(screenWidth, screenHeight, 500f, 730f, "일시정지");
        float x = panel.x + 75f;
        float y = panel.y + 82f;
        float width = panel.width - 150f;
        var debtCard = new Rect(panel.x + 45f, y, panel.width - 90f, 118f);
        FishingUiTheme.DrawCard(debtCard, true);
        GUI.Label(new Rect(debtCard.x + 18f, debtCard.y + 10f, 120f, 28f), "남은 빚", FishingUiTheme.Small);
        FishingUiTheme.ColoredLabel(new Rect(debtCard.x + 130f, debtCard.y + 6f, debtCard.width - 148f, 34f),
            $"{(playerDebt != null ? playerDebt.RemainingDebt : 0):N0} G", FishingUiTheme.Heading, FishingUiTheme.Gold);
        bool oldEnabled = GUI.enabled;
        GUI.enabled = playerDebt != null && !playerDebt.IsFullyRepaid;
        if (FishingUiTheme.StyledButton(new Rect(debtCard.x + 18f, debtCard.y + 53f, 174f, 46f), "100 G 상환"))
            TryRepayDebt(100);
        if (FishingUiTheme.StyledButton(new Rect(debtCard.xMax - 192f, debtCard.y + 53f, 174f, 46f), "500 G 상환", primary: true))
            TryRepayDebt(500);
        GUI.enabled = oldEnabled;

        float menuY = y + 142f;
        if (FishingUiTheme.StyledButton(new Rect(x, menuY, width, 52f), "게임 계속하기", primary: true))
        {
            audioDirector?.PlayUiClick();
            CloseWindow();
        }
        if (FishingUiTheme.StyledButton(new Rect(x, menuY + 62f, width, 52f), "저장"))
        {
            audioDirector?.PlayUiClick();
            statusMessage = saveSystem != null && saveSystem.Save() ? "게임을 저장했습니다." : "저장하지 못했습니다.";
        }
        if (FishingUiTheme.StyledButton(new Rect(x, menuY + 124f, width, 52f), "메인 메뉴"))
        {
            audioDirector?.PlayUiClick();
            OpenWindow(WindowMode.MainMenu);
        }
        if (FishingUiTheme.StyledButton(new Rect(x, menuY + 186f, width, 52f), "게임 초기화", danger: true))
        {
            audioDirector?.PlayUiClick();
            OpenWindow(WindowMode.ResetConfirm);
        }
        if (FishingUiTheme.StyledButton(new Rect(x, menuY + 248f, width, 52f), "게임 종료", danger: true))
        {
            audioDirector?.PlayUiClick();
            QuitGame();
        }
        GUI.Label(new Rect(panel.x + 30f, panel.yMax - 92f, panel.width - 60f, 54f), statusMessage, FishingUiTheme.BodyCentered);
        GUI.Label(new Rect(panel.x + 30f, panel.yMax - 42f, panel.width - 60f, 24f), "ESC로 게임에 돌아가기", FishingUiTheme.SmallCentered);
    }

    private void TryRepayDebt(int amount)
    {
        if (playerDebt == null || wallet == null)
        {
            statusMessage = "빚 정보를 불러오지 못했습니다.";
            return;
        }

        int required = Mathf.Min(amount, playerDebt.RemainingDebt);
        int paid = playerDebt.TryRepay(wallet, amount);
        if (paid > 0)
        {
            audioDirector?.PlayDebtRepay();
            statusMessage = playerDebt.IsFullyRepaid
                ? $"{paid:N0} G를 상환해 빚을 모두 갚았습니다."
                : $"빚 {paid:N0} G를 상환했습니다.";
        }
        else statusMessage = required > wallet.Balance ? "상환할 돈이 부족합니다." : "더 이상 갚을 빚이 없습니다.";
    }

    private void DrawMainMenu(float screenWidth, float screenHeight)
    {
        Rect panel = CenterPanel(screenWidth, screenHeight, 560f, 430f, "픽셀 낚시 마을");
        GUI.Label(new Rect(panel.x + 50f, panel.y + 76f, panel.width - 100f, 34f), "현재 진행 상황은 그대로 유지됩니다.", FishingUiTheme.BodyCentered);
        if (FishingUiTheme.StyledButton(new Rect(panel.x + 105f, panel.y + 135f, panel.width - 210f, 54f), "이어하기", primary: true))
        {
            audioDirector?.PlayUiClick();
            CloseWindow();
        }
        if (FishingUiTheme.StyledButton(new Rect(panel.x + 105f, panel.y + 205f, panel.width - 210f, 54f), "저장 데이터 다시 불러오기"))
        {
            audioDirector?.PlayUiClick();
            statusMessage = saveSystem != null && saveSystem.TryLoad() ? saveSystem.LastStatus : "불러올 저장 데이터가 없습니다.";
        }
        if (FishingUiTheme.StyledButton(new Rect(panel.x + 105f, panel.y + 275f, panel.width - 210f, 54f), "게임 종료", danger: true))
        {
            audioDirector?.PlayUiClick();
            QuitGame();
        }
        GUI.Label(new Rect(panel.x + 30f, panel.yMax - 50f, panel.width - 60f, 28f), statusMessage, FishingUiTheme.BodyCentered);
    }

    private void DrawResetConfirm(float screenWidth, float screenHeight)
    {
        Rect panel = CenterPanel(screenWidth, screenHeight, 560f, 360f, "게임 초기화");
        GUI.Label(new Rect(panel.x + 55f, panel.y + 76f, panel.width - 110f, 36f),
            "저장된 진행 상황을 모두 지울까요?", FishingUiTheme.Heading);
        FishingUiTheme.ColoredLabel(new Rect(panel.x + 55f, panel.y + 118f, panel.width - 110f, 72f),
            "돈 · 물고기 · 도감 · 낚싯대 · 퀘스트 · 위치가\n처음 상태로 돌아갑니다.",
            FishingUiTheme.BodyCentered, FishingUiTheme.Gold);

        float buttonY = panel.y + 222f;
        if (FishingUiTheme.StyledButton(new Rect(panel.x + 55f, buttonY, 210f, 54f), "취소"))
        {
            audioDirector?.PlayUiClick();
            OpenWindow(WindowMode.Pause);
        }
        if (FishingUiTheme.StyledButton(new Rect(panel.xMax - 265f, buttonY, 210f, 54f), "초기화", danger: true))
        {
            audioDirector?.PlayUiClick();
            bool reset = saveSystem != null && saveSystem.ResetGame();
            statusMessage = reset ? "게임을 처음 상태로 초기화했습니다." : "게임을 초기화하지 못했습니다.";
            if (reset) CloseWindow();
        }
        GUI.Label(new Rect(panel.x + 40f, panel.yMax - 48f, panel.width - 80f, 26f),
            "ESC를 누르면 취소합니다.", FishingUiTheme.SmallCentered);
    }

    private Rect CenterPanel(float screenWidth, float screenHeight, float width, float height, string title)
    {
        width = Mathf.Min(width, screenWidth - 30f);
        height = Mathf.Min(height, screenHeight - 30f);
        var panel = new Rect((screenWidth - width) * .5f, (screenHeight - height) * .5f, width, height);
        FishingUiTheme.DrawPanel(panel);
        FishingUiTheme.DrawTitlePlate(new Rect(panel.x + 65f, panel.y - 16f, panel.width - 130f, 60f), title);
        return panel;
    }

    private void QuitGame()
    {
        saveSystem?.Save();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void DrawRarity(Rect rect, string label, Color color)
        => FishingUiTheme.ColoredLabel(rect, "등급  " + label, FishingUiTheme.Body, color);
}
