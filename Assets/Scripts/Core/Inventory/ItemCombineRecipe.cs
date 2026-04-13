using UnityEngine;

[CreateAssetMenu(fileName = "ItemCombineRecipe", menuName = "Scriptable Objects/ItemCombineRecipe")]
public class ItemCombineRecipe : ScriptableObject
{
    [Header("Ingredients")]
    public ItemData itemA;
    public ItemData itemB;

    [Header("Result")]
    public ItemData resultItem;
    public int resultAmount = 1;

    public bool Matches(ItemData first, ItemData second)
    {
        if (first == null || second == null) return false;

        bool direct = first == itemA && second == itemB;
        bool reverse = first == itemB && second == itemA;

        return direct || reverse;
    }
}