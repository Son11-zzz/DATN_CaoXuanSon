using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;

    public ItemType type;

    [Tooltip("Higher-level category used by gameplay systems (e.g. shops mapping Food → BuyMeal objective). Leave None to opt out.")]
    public ItemCategory category;

    [Tooltip("HP restored when this food/drink consumable is used from inventory.")]
    public float restoreHealth;

    [Tooltip("Energy restored when this consumable food/drink is used from inventory.")]
    public float restoreEnergy;

    // Có thể giữ lại để dùng về sau, hiện tại combine sẽ dùng ItemCombineRecipe
    public List<ItemData> combineWith;
}

public enum ItemType
{
    Material,
    KeyItem,
    Consumable
}

public enum ItemCategory
{
    None,
    Food,
    Drink,
    Supplies,
    Other
}