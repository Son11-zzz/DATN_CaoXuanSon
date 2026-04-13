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
        DontDestroyOnLoad(gameObject);
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
