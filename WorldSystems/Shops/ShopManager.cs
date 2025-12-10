using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager instance;
    
    [Header("Spawning")]
    [SerializedDictionary("string", "Shop")]
    public Dictionary<string, Shop> Shops = new();

    [Header("Displaying")]
    [SerializeField] private GameObject ShopUI;
    [SerializeField] private GameObject ShopPreviewUI;
    [SerializeField] private GameObject ShopCardsHolder;
    [SerializeField] private GameObject ShopCard;
    [SerializeField] private GameObject ShopItemPrice;

    private string currentlyOpenShop;

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
            instance = this;

        DontDestroyOnLoad(gameObject);

        SetupShops();
    }

    private void SetupShops()
    {
        Shop[] foundShops = FindObjectsByType<Shop>(FindObjectsSortMode.None);

        Shops.Clear();

        foreach (Shop shop in foundShops)
        {
            if (!Shops.ContainsKey(shop.ShopName))
                Shops.Add(shop.ShopName, shop);
            else
                Debug.LogError("Duplicate shop: " + shop.ShopName);
        }
    }

    public void OpenShop(string shopName)
    {
        // Disable Player
        InputDirector.Instance.DisableInput();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Enable Shop UI
        ShopUI.SetActive(true);
        currentlyOpenShop = shopName;

        // Clean UI
        foreach (Transform item in ShopCardsHolder.transform)
        {
            Debug.Log(item.name);
            item.parent = null;
            Destroy(item.gameObject);
        }

        // Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(ShopCardsHolder.GetComponent<RectTransform>());

        Image previewImage = ShopPreviewUI.GetComponent<Image>();
        previewImage.sprite = null;
        Color imageColor = previewImage.color;
        imageColor.a = 0f;
        previewImage.color = imageColor;
        ShopItemPrice.GetComponent<TextMeshProUGUI>().text = "-";

        // Place new shop cards
        Shop shopToPopulate;
        if (!Shops.TryGetValue(shopName, out shopToPopulate))
            return;

        foreach (ShopItem item in shopToPopulate.ShopItems)
        {
            GameObject newCard = Instantiate(ShopCard, ShopCardsHolder.transform);

            newCard.GetComponentsInChildren<Image>()[1].sprite = item.ItemDisplayImage;
            newCard.GetComponentInChildren<TextMeshProUGUI>().text = item.ItemName;

            Button cardButton = newCard.GetComponent<Button>();
            if (cardButton != null)
                cardButton.onClick.AddListener(() => SelectItem(item));
        }
    }

    public void SelectItem(ShopItem item)
    {
        Image previewImage = ShopPreviewUI.GetComponent<Image>();
        previewImage.sprite = item.ItemPreviewImage;
        Color imageColor = previewImage.color;
        imageColor.a = 255f;
        previewImage.color = imageColor;

        ShopItemPrice.GetComponent<TextMeshProUGUI>().text = item.ItemPrice.ToString();
    }

    public void CloseShop()
    {
        ShopUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        InputDirector.Instance.EnableInput();
        Shops[currentlyOpenShop].ShopOpen = false;
    }
}
