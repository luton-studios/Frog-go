using UnityEngine;

public class ShopItemUI : MonoBehaviour {
    public enum Type { FrogSkin, MapTheme };
    public enum Status { Locked, Owned, Equipped };

    public Transform cachedTrans;
    public UISprite backgroundSprite;
    public UILabel itemNameLabel;
    public UISprite itemIconSprite;
    public UITexture realtimePreviewTexture;
    public UISprite statusIcon;
    public UILabel statusLabel;
    public StatusProperties locked;
    public StatusProperties unlockable;
    public string cannotPurchaseSprite;
    public StatusProperties owned;
    public StatusProperties equipped;

    public ShopController shop { get; private set; }
    public ShopController.ShopItem shopItemData { get; private set; }
    public Type itemType { get; private set; }
    public int itemIndex { get; private set; }
    public Status curStatus { get; private set; }

    public void Init(ShopController shop, Type type, int itemIndex, ShopController.ShopItem item) {
        this.shop = shop;
        itemType = type;
        shopItemData = item;
        this.itemIndex = itemIndex;
        itemNameLabel.text = item.name;
        itemIconSprite.spriteName = item.spriteName;
        curStatus = Status.Locked;
    }

    public void Refresh() {
        if(curStatus == Status.Equipped) {
            backgroundSprite.color = equipped.backgroundColor;
            statusIcon.spriteName = equipped.statusSpriteName;
            statusLabel.text = "EQUIPPED";
        }
        else if(curStatus == Status.Owned) {
            backgroundSprite.color = owned.backgroundColor;
            statusIcon.spriteName = owned.statusSpriteName;
            statusLabel.text = "OWNED";
        }
        else if(curStatus == Status.Locked) {
            bool canUnlock = false;

            if(shopItemData.unlockWithAchievement) {
                backgroundSprite.color = locked.backgroundColor;
                statusIcon.spriteName = cannotPurchaseSprite;
                Achievement achv = shop.achieve.GetAchievementData(shopItemData.achievementTag);
                statusLabel.text = achv.displayName;
            }
            else {
                canUnlock = (shopItemData.price <= shop.currentFrogPoints);

                if(canUnlock) {
                    backgroundSprite.color = unlockable.backgroundColor;
                    statusIcon.spriteName = unlockable.statusSpriteName;
                }
                else {
                    backgroundSprite.color = locked.backgroundColor;
                    statusIcon.spriteName = locked.statusSpriteName;
                }

                statusLabel.text = shopItemData.price.ToString("N0");
            }
        }
    }

    public void SetStatus(Status status) {
        curStatus = status;
    }

    private void OnClick() {
        if(itemType == Type.FrogSkin) {
            shop.OnClickedSkin(itemIndex);
        }
        else if(itemType == Type.MapTheme) {
            shop.OnClickedMapTheme(itemIndex);
        }
    }

    [System.Serializable]
    public class StatusProperties {
        public Color backgroundColor;
        public string statusSpriteName;
    }
}