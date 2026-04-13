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

    private readonly List<InventorySlotUI> slotViews = new List<InventorySlotUI>();
    private readonly List<InventorySlotUI> selectedSlots = new List<InventorySlotUI>();

    private void Start()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnInventoryChanged += Refresh;
        }
    }

    private void OnDestroy()
    {
        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnInventoryChanged -= Refresh;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }

        bool ctrlPressed = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
        if (ctrlPressed && inventoryPanel != null && inventoryPanel.activeSelf)
        {
            TryCombineSelected();
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
            SetMessage("Chọn 2 item, nhấn Ctrl để combine.");
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
            SetMessage("Chỉ chọn tối đa 2 item.");
            return;
        }

        selectedSlots.Add(slot);
        slot.SetSelected(true);
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
            SetMessage("Combine thành công: " + result.itemName);
            Refresh();
        }
        else
        {
            SetMessage("Không có công thức combine phù hợp.");
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