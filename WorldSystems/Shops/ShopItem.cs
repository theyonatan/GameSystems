using UnityEngine;

[CreateAssetMenu(menuName ="Zero/ShopItem", fileName ="ShopItem")]
public class ShopItem : ScriptableObject
{
    public string ItemName;
    public int ItemPrice;
    public Sprite ItemDisplayImage;
    public Sprite ItemPreviewImage;
    public bool Sold = false;
}
