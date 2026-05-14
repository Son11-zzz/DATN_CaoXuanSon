using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryStack
{
    public ItemData item;
    public int amount;

    public InventoryStack(ItemData item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }
}

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Instance;

    [Header("Capacity")]
    [SerializeField] private int maxSlots = 20;

    [Header("Runtime Inventory")]
    public List<InventoryStack> stacks = new List<InventoryStack>();

    [Header("Combine Recipes")]
    public List<ItemCombineRecipe> combineRecipes = new List<ItemCombineRecipe>();

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }
        DontDestroyOnLoad(gameObject);
    }

    public int MaxSlots => maxSlots;

    public bool CanAddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        // If already exists, allow stacking regardless of capacity.
        InventoryStack stack = stacks.Find(s => s != null && s.item == item);
        if (stack != null) return true;

        // Otherwise, require a free slot.
        return maxSlots <= 0 || stacks.Count < maxSlots;
    }

    public bool TryAddItem(ItemData item, int amount = 1)
    {
        if (!CanAddItem(item, amount)) return false;
        AddItem(item, amount);
        return true;
    }

    public void AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return;

        InventoryStack stack = stacks.Find(s => s.item == item);
        if (stack != null)
        {
            stack.amount += amount;
        }
        else
        {
            stacks.Add(new InventoryStack(item, amount));
        }

        Debug.Log("Added: " + item.itemName + " x" + amount);
        OnInventoryChanged?.Invoke();
    }

    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        InventoryStack stack = stacks.Find(s => s.item == item);
        if (stack == null || stack.amount < amount) return false;

        stack.amount -= amount;
        if (stack.amount <= 0)
        {
            stacks.Remove(stack);
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public int GetAmount(ItemData item)
    {
        InventoryStack stack = stacks.Find(s => s.item == item);
        return stack != null ? stack.amount : 0;
    }

    public void ClearStacksForSave()
    {
        stacks.Clear();
        OnInventoryChanged?.Invoke();
    }

    public void Persist_ApplyInventoryStacks(IReadOnlyList<InventoryPayload> payloads, Func<string, ItemData> resolveByKeyOrNull)
    {
        stacks.Clear();

        if (payloads != null && resolveByKeyOrNull != null)
        {
            for (int i = 0; i < payloads.Count; i++)
            {
                InventoryPayload slot = payloads[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.itemKey)) continue;

                ItemData resolved = resolveByKeyOrNull(slot.itemKey.Trim());
                if (resolved == null || slot.amount <= 0) continue;

                stacks.Add(new InventoryStack(resolved, Mathf.Max(0, slot.amount)));
            }
        }

        OnInventoryChanged?.Invoke();
    }

    /// <summary>Collects referenced items for resolving save keys at load time.</summary>
    public void Persist_CollectDistinctItems(ICollection<ItemData> into)
    {
        if (into == null) return;

        for (int i = 0; i < stacks.Count; i++)
        {
            InventoryStack stack = stacks[i];
            if (stack != null && stack.item != null)
            {
                into.Add(stack.item);
            }
        }

        foreach (var recipe in combineRecipes)
        {
            if (recipe == null || recipe.resultItem == null) continue;
            into.Add(recipe.resultItem);
            foreach (ItemData ingredient in GatherRecipeIngredients(recipe))
            {
                if (ingredient != null)
                {
                    into.Add(ingredient);
                }
            }
        }
    }

    static IEnumerable<ItemData> GatherRecipeIngredients(ItemCombineRecipe recipe)
    {
        if (recipe.itemA != null)
        {
            yield return recipe.itemA;
        }

        if (recipe.itemB != null)
        {
            yield return recipe.itemB;
        }
    }

    public bool TryConsumeSingle(ItemData item, out string reason)
    {
        reason = null;

        if (item == null)
        {
            reason = "Item null.";
            return false;
        }

        if (item.type != ItemType.Consumable)
        {
            reason = "Khong phai do tieu thu.";
            return false;
        }

        if (item.category != ItemCategory.Food && item.category != ItemCategory.Drink)
        {
            reason = "Chi do an/uong moi dung duoc.";
            return false;
        }

        if (item.restoreHealth <= 0f && item.restoreEnergy <= 0f && item.damageHealth <= 0f)
        {
            reason = "Chua gan gia tri hoi phuc hoac sat thuong.";
            return false;
        }

        if (!RemoveItem(item, 1))
        {
            reason = "Khong lay duoc trong tui.";
            return false;
        }

        ApplyConsumableEffects(item);
        return true;
    }

    static void ApplyConsumableEffects(ItemData item)
    {
        if (StatManager.Instance == null) return;

        const float clampTop = 100f;

        if (item.restoreHealth > 0f)
        {
            StatManager.Instance.health = Mathf.Clamp(
                StatManager.Instance.health + item.restoreHealth,
                0f,
                clampTop);
        }

        if (item.damageHealth > 0f)
        {
            StatManager.Instance.health = Mathf.Clamp(
                StatManager.Instance.health - item.damageHealth,
                0f,
                clampTop);
        }

        if (item.restoreEnergy > 0f)
        {
            StatManager.Instance.energy = Mathf.Clamp(
                StatManager.Instance.energy + item.restoreEnergy,
                0f,
                clampTop);
        }

        EventManager.Instance?.NotifyStatChanged();

        // Nếu health về 0 sau khi dùng consumable → xử lý nhập viện
        StatManager.Instance.CheckHealthZero();
    }

    public bool TryCombine(ItemData first, ItemData second, out ItemData resultItem)
    {
        resultItem = null;

        if (first == null || second == null) return false;
        if (GetAmount(first) <= 0 || GetAmount(second) <= 0) return false;

        ItemCombineRecipe matched = null;
        foreach (var recipe in combineRecipes)
        {
            if (recipe == null) continue;
            if (recipe.Matches(first, second))
            {
                matched = recipe;
                break;
            }
        }

        if (matched == null || matched.resultItem == null) return false;

        bool removedFirst = RemoveItem(first, 1);
        bool removedSecond = RemoveItem(second, 1);

        if (!removedFirst || !removedSecond)
        {
            if (removedFirst) AddItem(first, 1);
            if (removedSecond) AddItem(second, 1);
            return false;
        }

        AddItem(matched.resultItem, Math.Max(1, matched.resultAmount));

        resultItem = matched.resultItem;
        Debug.Log("Combine success: " + matched.resultItem.itemName);
        OnInventoryChanged?.Invoke();
        return true;
    }
}
