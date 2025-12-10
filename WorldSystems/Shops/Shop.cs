using System.Collections.Generic;
using UnityEngine;

public class Shop : MonoBehaviour, Interactable
{
    public string ShopName;
    public List<ShopItem> ShopItems;
    [HideInInspector] public bool ShopOpen;

    private void Start()
    {
        ShopOpen = false;
    }

    public void MarkAsInteractable()
    {
        
    }

    public void Interact()
    {
        if (ShopOpen)
            return;

        ShopOpen = true;

        OpenShopUI();
    }

    private void OpenShopUI()
    {
        ShopManager.instance.OpenShop(ShopName);
    }


}
