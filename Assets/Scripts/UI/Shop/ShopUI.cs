using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance;

    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI shopNameText;
    [SerializeField] private TextMeshProUGUI moneyText;

    [Header("List")]
    [SerializeField] private Transform itemsRoot;
    [SerializeField] private GameObject itemRowPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Info")]
    [SerializeField] private TextMeshProUGUI messageText;

    private ShopCatalog currentCatalog;
    private System.Func<ItemData, string> currentPurchaseObjectiveResolver;

    private Coroutine refreshScrollRoutine;

    public bool IsOpen => panel != null && panel.activeSelf;

    /// <summary>Khi gameplay không có PauseMenuController vẫn đóng shop bằng ESC.</summary>
    static int EscapeClosedShopConsumedFrameId = int.MinValue;

    public static void NotifyEscapeConsumedForShopClose()
    {
        EscapeClosedShopConsumedFrameId = Time.frameCount;
    }

    public static bool WasEscapeConsumedClosingShopOnThisFrame()
    {
        return EscapeClosedShopConsumedFrameId == Time.frameCount;
    }

    /// <summary>Instance có thể chưa gán nếu GameObject bắt đầu inactive (Awake chưa chạy).</summary>
    public static ShopUI ResolveInstance()
    {
        if (Instance != null) return Instance;

        var found = FindObjectsByType<ShopUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return found != null && found.Length > 0 ? found[0] : null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panel == null)
        {
            panel = gameObject;
        }
    }

    private void Start()
    {
        Hide();
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            NotifyEscapeConsumedForShopClose();
            return;
        }

        if (IsOpen)
        {
            RefreshMoney();
        }
    }

    public void Show(ShopCatalog catalog)
    {
        Show(catalog, (System.Func<ItemData, string>)null);
    }

    public void Show(ShopCatalog catalog, string purchaseObjectiveId)
    {
        string trimmed = string.IsNullOrWhiteSpace(purchaseObjectiveId) ? null : purchaseObjectiveId.Trim();
        Show(catalog, trimmed == null ? (System.Func<ItemData, string>)null : (_ => trimmed));
    }

    public void Show(ShopCatalog catalog, System.Func<ItemData, string> objectiveResolver)
    {
        currentCatalog = catalog;
        currentPurchaseObjectiveResolver = objectiveResolver;

        if (panel != null)
        {
            panel.SetActive(true);
        }

        if (shopNameText != null)
        {
            string title = catalog != null && !string.IsNullOrWhiteSpace(catalog.shopName)
                ? catalog.shopName
                : "Cửa hàng";
            shopNameText.text = title;
        }

        SetMessage(string.Empty);
        RefreshMoney();
        BuildList();

        if (refreshScrollRoutine != null)
        {
            StopCoroutine(refreshScrollRoutine);
        }

        refreshScrollRoutine = StartCoroutine(RefreshScrollNextFrame());
    }

    public void Hide()
    {
        currentCatalog = null;
        currentPurchaseObjectiveResolver = null;
        SetMessage(string.Empty);

        if (panel != null && panel != gameObject)
        {
            panel.SetActive(false);
        }
        else if (panel == gameObject)
        {
            gameObject.SetActive(false);
        }
    }

    public void TryBuy(ShopItemEntry entry)
    {
        if (entry == null || entry.item == null)
        {
            SetMessage("Vật phẩm không hợp lệ.");
            return;
        }

        int price = Mathf.Max(0, entry.price);
        int amount = Mathf.Max(1, entry.amount);

        var stats = StatManager.Instance;
        if (stats == null)
        {
            SetMessage("Không tìm thấy StatManager.");
            return;
        }

        if (stats.money < price)
        {
            SetMessage("Không đủ tiền.");
            return;
        }

        var inv = InventorySystem.Instance;
        if (inv == null)
        {
            SetMessage("Không tìm thấy InventorySystem.");
            return;
        }

        if (!inv.CanAddItem(entry.item, amount))
        {
            SetMessage("Túi đồ đầy.");
            return;
        }

        bool added = inv.TryAddItem(entry.item, amount);
        if (!added)
        {
            SetMessage("Không thể mua (túi đồ đầy).");
            return;
        }

        stats.money -= price;
        if (stats.money < 0f) stats.money = 0f;

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }

        SetMessage($"Đã mua {entry.item.itemName} ×{amount}.");
        RefreshMoney();

        if (currentPurchaseObjectiveResolver != null && StoryEventManager.Instance != null)
        {
            string oid = currentPurchaseObjectiveResolver(entry.item);
            if (!string.IsNullOrWhiteSpace(oid))
            {
                StoryEventManager.Instance.TryCompleteObjective(oid.Trim(), out _);
            }
        }
    }

    private void BuildList()
    {
        if (itemsRoot == null || itemRowPrefab == null) return;

        for (int i = itemsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(itemsRoot.GetChild(i).gameObject);
        }

        if (currentCatalog == null || currentCatalog.items == null) return;

        for (int i = 0; i < currentCatalog.items.Count; i++)
        {
            ShopItemEntry entry = currentCatalog.items[i];
            if (entry == null || entry.item == null) continue;

            GameObject go = Instantiate(itemRowPrefab, itemsRoot);
            var row = go.GetComponent<ShopItemRowUI>();
            if (row != null)
            {
                row.Setup(entry, this);
            }
        }

        // Layout will be rebuilt in RefreshScrollNextFrame() after Unity has processed the new objects.
    }

    private System.Collections.IEnumerator RefreshScrollNextFrame()
    {
        yield return null;

        var content = itemsRoot as RectTransform;

        if (scrollRect != null && content != null)
        {
            if (scrollRect.content != content)
            {
                scrollRect.content = content;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();

            scrollRect.verticalNormalizedPosition = 1f;
        }

        refreshScrollRoutine = null;
    }

    private void RefreshMoney()
    {
        if (moneyText == null) return;

        var stats = StatManager.Instance;
        if (stats == null) return;

        moneyText.text = $"Tiền: {stats.money:0}";
    }

    private void SetMessage(string text)
    {
        if (messageText != null)
        {
            messageText.text = text ?? string.Empty;
        }
    }
}
