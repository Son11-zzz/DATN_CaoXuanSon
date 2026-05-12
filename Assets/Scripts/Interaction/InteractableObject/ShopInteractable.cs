using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopInteractable : InteractableBase
{
    [Header("Shop")]
    [SerializeField] private ShopCatalog catalog;

    [Header("Story Hook")]
    [Tooltip("Per-category objective mapping. When the player buys an item, its ItemData.category is matched here first. Example: { Food → BuyMeal, Drink → BuyMeal, Supplies → BuySupplies }.")]
    [SerializeField] private List<ShopCategoryObjective> categoryObjectives = new List<ShopCategoryObjective>();

    [Tooltip("Per-item overrides. Highest priority — if a purchased item appears here, its specific objective id is used instead of the category mapping. Use only for special-cased items.")]
    [SerializeField] private List<ShopItemObjectiveOverride> itemObjectiveOverrides = new List<ShopItemObjectiveOverride>();

    [Tooltip("Fallback objective id ticked for purchased items not matched by per-item overrides or category mapping. Leave blank to skip (recommended).")]
    [SerializeField] private string storyObjectiveIdOnPurchase;

    public override void Interact()
    {
        if (catalog == null)
        {
            Debug.LogWarning($"ShopInteractable '{name}': catalog đang null.");
            return;
        }

        var ui = ResolveShopUI();
        if (ui == null)
        {
            Debug.LogWarning("ShopInteractable: không tìm thấy ShopUI trong scene đang tải.");
            return;
        }

        ui.gameObject.SetActive(true);
        ui.Show(catalog, GetObjectiveIdForItem);
    }

    public string GetObjectiveIdForItem(ItemData item)
    {
        if (item != null && itemObjectiveOverrides != null)
        {
            for (int i = 0; i < itemObjectiveOverrides.Count; i++)
            {
                var entry = itemObjectiveOverrides[i];
                if (entry == null || entry.item != item) continue;
                if (string.IsNullOrWhiteSpace(entry.storyObjectiveId)) continue;
                return entry.storyObjectiveId.Trim();
            }
        }

        if (item != null && categoryObjectives != null && item.category != ItemCategory.None)
        {
            for (int i = 0; i < categoryObjectives.Count; i++)
            {
                var entry = categoryObjectives[i];
                if (entry == null) continue;
                if (entry.category != item.category) continue;
                if (string.IsNullOrWhiteSpace(entry.storyObjectiveId)) continue;
                return entry.storyObjectiveId.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(storyObjectiveIdOnPurchase) ? null : storyObjectiveIdOnPurchase.Trim();
    }

    private static ShopUI ResolveShopUI() => ShopUI.ResolveInstance();

}

[Serializable]
public class ShopItemObjectiveOverride
{
    public ItemData item;
    public string storyObjectiveId;
}

[Serializable]
public class ShopCategoryObjective
{
    public ItemCategory category;
    public string storyObjectiveId;
}
