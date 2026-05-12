using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("List")]
    [SerializeField] private Transform slotsRoot;
    [SerializeField] private GameObject slotPrefab;

    [Header("Info")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Use")]
    [SerializeField] private KeyCode useConsumableKey = KeyCode.U;

    private readonly List<InventorySlotUI> slotViews = new List<InventorySlotUI>();
    private readonly List<InventorySlotUI> selectedSlots = new List<InventorySlotUI>();

    private void Awake()
    {
        ResolvePanelSafely();
    }

    private void OnEnable()
    {
        ResolvePanelSafely();

        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnInventoryChanged -= Refresh;
            InventorySystem.Instance.OnInventoryChanged += Refresh;
        }

        HidePanel();
    }

    private void OnDisable()
    {
        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnInventoryChanged -= Refresh;
        }
    }

    private void Update()
    {
        ResolvePanelSafely();

        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }

        bool ctrlPressed = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
        if (ctrlPressed && inventoryPanel != null && inventoryPanel.activeSelf)
        {
            TryCombineSelected();
        }

        if (inventoryPanel != null && inventoryPanel.activeSelf && Input.GetKeyDown(useConsumableKey))
        {
            TryUseSingleSelectedConsumable();
        }
    }

    public void HidePanel()
    {
        if (inventoryPanel == null) return;
        if (inventoryPanel == gameObject) return; // không bao giờ disable object đang chạy script
        inventoryPanel.SetActive(false);
    }

    private void ResolvePanelSafely()
    {
        if (inventoryPanel == gameObject && transform.childCount > 0)
        {
            inventoryPanel = transform.GetChild(0).gameObject;
        }
    }

    private void ToggleInventory()
    {
        if (inventoryPanel == null) return;

        bool active = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(active);

        if (active)
        {
            Refresh();
            SetMessage("Chọn 2 ô vật phẩm, nhấn Ctrl để ghép.");
        }
    }

    public void Refresh()
    {
        if (slotsRoot == null || slotPrefab == null || InventorySystem.Instance == null) return;

        for (int i = slotsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(slotsRoot.GetChild(i).gameObject);
        }

        slotViews.Clear();
        selectedSlots.Clear();

        foreach (var stack in InventorySystem.Instance.stacks)
        {
            if (stack == null || stack.item == null || stack.amount <= 0) continue;

            GameObject go = Instantiate(slotPrefab, slotsRoot);
            InventorySlotUI view = go.GetComponent<InventorySlotUI>();
            if (view == null) continue;

            view.Setup(stack.item, stack.amount, this);
            slotViews.Add(view);
        }
    }

    public void ToggleSelection(InventorySlotUI slot)
    {
        if (slot == null) return;

        if (selectedSlots.Contains(slot))
        {
            selectedSlots.Remove(slot);
            slot.SetSelected(false);
            return;
        }

        if (selectedSlots.Count >= 2)
        {
            SetMessage("Chỉ chọn tối đa 2 ô vật phẩm.");
            return;
        }

        selectedSlots.Add(slot);
        slot.SetSelected(true);
    }

    public void ShowItemInfo(InventorySlotUI slot)
    {
        if (slot == null || slot.ItemData == null) return;

        var item = slot.ItemData;
        string description = string.IsNullOrWhiteSpace(item.description)
            ? string.Empty
            : "\n" + item.description;

        SetMessage($"{item.itemName} ×{slot.Amount}{description}\nĐồ tiêu thụ (ăn/uống): nhấn phím {useConsumableKey}.");
    }

    private void TryUseSingleSelectedConsumable()
    {
        if (selectedSlots.Count != 1 || InventorySystem.Instance == null)
        {
            SetMessage("Hãy chọn một ô trong túi đồ rồi nhấn phím " + useConsumableKey + ".");
            return;
        }

        ItemData item = selectedSlots[0].ItemData;
        if (InventorySystem.Instance.TryConsumeSingle(item, out string fail))
        {
            SetMessage("Đã sử dụng.");
            Refresh();
            return;
        }

        SetMessage(fail);
    }

    private void TryCombineSelected()
    {
        if (selectedSlots.Count != 2)
        {
            SetMessage("Cần chọn đúng 2 item.");
            return;
        }

        ItemData first = selectedSlots[0].ItemData;
        ItemData second = selectedSlots[1].ItemData;

        if (InventorySystem.Instance.TryCombine(first, second, out ItemData result))
        {
            SetMessage("Ghép thành công: " + result.itemName);
            Refresh();
        }
        else
        {
            SetMessage("Không có công thức ghép phù hợp.");
        }
    }

    private void SetMessage(string text)
    {
        if (messageText != null)
        {
            messageText.text = text;
        }
    }
}