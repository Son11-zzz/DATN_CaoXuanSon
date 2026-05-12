using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemRowUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button buyButton;

    private ShopItemEntry entry;
    private ShopUI owner;

    public void Setup(ShopItemEntry entry, ShopUI owner)
    {
        this.entry = entry;
        this.owner = owner;

        if (iconImage != null && entry != null && entry.item != null)
        {
            iconImage.sprite = entry.item.icon;
        }

        if (nameText != null)
        {
            string itemName = entry != null && entry.item != null ? entry.item.itemName : "(None)";
            int amt = entry != null ? Mathf.Max(1, entry.amount) : 1;
            nameText.text = amt > 1 ? $"{itemName} x{amt}" : itemName;
        }

        if (priceText != null)
        {
            int p = entry != null ? Mathf.Max(0, entry.price) : 0;
            priceText.text = $"{p}";
        }

        if (buyButton == null)
        {
            buyButton = GetComponent<Button>();
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() =>
            {
                if (this.owner != null)
                {
                    this.owner.TryBuy(this.entry);
                }
            });
        }
    }
}
