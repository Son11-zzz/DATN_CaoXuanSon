using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;

    public ItemType type;

    // Có thể giữ lại để dùng về sau, hiện tại combine sẽ dùng ItemCombineRecipe
    public List<ItemData> combineWith;
}

public enum ItemType
{
    Material,
    KeyItem,
    Consumable
}