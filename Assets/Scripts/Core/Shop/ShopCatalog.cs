using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Scriptable Objects/ShopCatalog")]
public class ShopCatalog : ScriptableObject
{
    public string shopName;
    public List<ShopItemEntry> items = new List<ShopItemEntry>();
}

[Serializable]
public class ShopItemEntry
{
    public ItemData item;
    public int price;
    public int amount = 1;
}
