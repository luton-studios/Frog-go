using UnityEngine;
using System.Collections.Generic;

public class ShopController : MonoBehaviour {
    public GameController game;
    public AchievementController achieve;
    public UISprite frogPointsIcon;
    public UILabel frogPointsLabel;
    public AnimatedSkinPreview skinPreviewPrefab;
    public ShopItemUI shopItemPrefab;
    public float spacing = 100f;
    public Transform skinGroupTrans;
    public Transform skinListStart;
    public Transform selectedSkinTrans;
    public SkinItem[] purchaseableSkins; // Default skin is first index.
    public SkinItem[] achievementSkins;
    public float mapThemeListOffsetY = -50f;
    public Transform mapThemeGroupTrans;
    public Transform mapThemeListStart;
    public Transform selectedMapThemeTrans;
    public MapTheme[] purchaseableMapThemes; // Default map theme is first index.
    public MapTheme[] achievementMapThemes;

    public int currentFrogPoints { get; private set; }

    private Queue<string> unlockedPopupsQueue;
    private AnimatedSkinPreview[] skinPreviewInstances;
    private ShopItemUI[] skinUiList;
    private ShopItemUI[] mapThemeUiList;
    private int indexToBuy;

    private void Awake() {
        unlockedPopupsQueue = new Queue<string>();
        
        SetupList();
        LoadShopState();
        RefreshUI();

        // Setup realtime skin previews.
        skinPreviewInstances = new AnimatedSkinPreview[purchaseableSkins.Length + achievementSkins.Length];

        for(int i = 0; i < purchaseableSkins.Length; i++) {
            SetupRealtimeSkinPreview(purchaseableSkins[i], i);
        }

        for(int i = 0; i < achievementSkins.Length; i++) {
            SetupRealtimeSkinPreview(achievementSkins[i], i + purchaseableSkins.Length);
        }
    }

    private void Update() {
        // Achievement popups queued when theres no dialog displaying.
        if(unlockedPopupsQueue.Count > 0) {
            if(!DialogBoxUI.instance.isDisplaying) {
                DialogBoxUI.instance.Display("CONGRATULATIONS" , unlockedPopupsQueue.Dequeue(), null);
            }
        }

        // Toggle rendering of animated previews depending whether the UI is visible or not.
        for(int i = 0; i < skinUiList.Length; i++) {
            SkinItem targetSkin = GetSkin(i);
            
            if(targetSkin.animatedPreview) {
                skinPreviewInstances[i].previewCamera.enabled = skinUiList[i].backgroundSprite.isVisible;
            }
        }
    }

    public void RefreshUI() {
        achieve.UpdateProgressAll(true);
        bool appliedEquippedSkin = false;

        for(int i = 0; i < skinUiList.Length; i++) {
            // Update equipped/owned status.
            skinUiList[i].Refresh();

            if(!appliedEquippedSkin && skinUiList[i].curStatus == ShopItemUI.Status.Equipped) {
                // Skin selection outline.
                selectedSkinTrans.parent = skinUiList[i].cachedTrans;
                selectedSkinTrans.localPosition = Vector3.zero;
                selectedSkinTrans.localScale = Vector3.one;

                game.UpdateFrogSkin(GetSkin(i).skinProperties);
                appliedEquippedSkin = true;
            }
        }

        bool appliedEquippedMapTheme = false;

        for(int i = 0; i < mapThemeUiList.Length; i++) {
            // Update equipped/owned status.
            mapThemeUiList[i].Refresh();

            if(!appliedEquippedMapTheme && mapThemeUiList[i].curStatus == ShopItemUI.Status.Equipped) {
                // Skin selection outline.
                selectedMapThemeTrans.parent = mapThemeUiList[i].cachedTrans;
                selectedMapThemeTrans.localPosition = Vector3.zero;
                selectedMapThemeTrans.localScale = Vector3.one;

                game.UpdateMapTheme(GetMapTheme(i));
                appliedEquippedMapTheme = true;
            }
        }

        frogPointsLabel.text = currentFrogPoints.ToString("N0");

        Vector3 pos = frogPointsIcon.transform.localPosition;
        pos.x = frogPointsLabel.transform.localPosition.x - ((frogPointsLabel.width + frogPointsIcon.width) * 0.5f) - 4f;
        frogPointsIcon.transform.localPosition = pos;
    }
    
    private void SetupRealtimeSkinPreview(SkinItem skin, int uiIndex) {
        if(!skin.animatedPreview)
            return;

        AnimatedSkinPreview previewInst = Instantiate(skinPreviewPrefab);
        previewInst.transform.position = new Vector3(uiIndex * 3f, -40f, 0f);
        previewInst.Init(skin.skinProperties);

        skinUiList[uiIndex].itemIconSprite.enabled = false;
        skinUiList[uiIndex].realtimePreviewTexture.cachedGameObject.SetActive(true);
        skinUiList[uiIndex].realtimePreviewTexture.mainTexture = previewInst.previewTexture;

        skinPreviewInstances[uiIndex] = previewInst;
    }

    private void SetupList() {
        // SKINS
        skinUiList = new ShopItemUI[purchaseableSkins.Length + achievementSkins.Length];
        
        for(int i = 0; i < skinUiList.Length; i++) {
            ShopItemUI skinUiInst = Instantiate(shopItemPrefab);
            skinUiList[i] = skinUiInst;

            skinUiInst.cachedTrans.parent = skinListStart;
            skinUiInst.cachedTrans.localPosition = new Vector3(0f, -i * spacing, 0f);
            skinUiInst.cachedTrans.localScale = Vector3.one;
            
            skinUiInst.Init(this, ShopItemUI.Type.FrogSkin, i, GetSkin(i));
        }

        // Automatically own default skin.
        skinUiList[0].SetStatus(ShopItemUI.Status.Owned);

        // MAP THEMES
        mapThemeGroupTrans.localPosition = new Vector3(0f, (-skinUiList.Length * spacing) + skinGroupTrans.localPosition.y + mapThemeListOffsetY, 0f);
        mapThemeUiList = new ShopItemUI[purchaseableMapThemes.Length + achievementMapThemes.Length];

        for(int i = 0; i < mapThemeUiList.Length; i++) {
            ShopItemUI mapThemeUiInst = Instantiate(shopItemPrefab);
            mapThemeUiList[i] = mapThemeUiInst;

            mapThemeUiInst.cachedTrans.parent = mapThemeListStart;
            mapThemeUiInst.cachedTrans.localPosition = new Vector3(0f, -i * spacing, 0f);
            mapThemeUiInst.cachedTrans.localScale = Vector3.one;

            mapThemeUiInst.Init(this, ShopItemUI.Type.MapTheme, i, GetMapTheme(i));
        }

        // Automatically own default map theme.
        mapThemeUiList[0].SetStatus(ShopItemUI.Status.Owned);
    }

    public void OnClickedSkin(int index) {
        ShopItemUI.Status status = skinUiList[index].curStatus;

        if(status == ShopItemUI.Status.Owned) {
            SelectAndApplySkin(index);
        }
        else if(status == ShopItemUI.Status.Locked) {
            OpenBuyPrompt(ShopItemUI.Type.FrogSkin, index);
        }
    }

    private void SelectAndApplySkin(int index) {
        if(skinUiList[index].curStatus != ShopItemUI.Status.Owned) {
            return;
        }

        // Clear previously equipped skin's status.
        for(int i = 0; i < skinUiList.Length; i++) {
            if(skinUiList[i].curStatus == ShopItemUI.Status.Equipped) {
                skinUiList[i].SetStatus(ShopItemUI.Status.Owned);
            }
        }

        // Set new equipped skin.
        skinUiList[index].SetStatus(ShopItemUI.Status.Equipped);
        PlayerPrefs.SetInt("SelectedSkin", index);

        // Update UI and save state.
        RefreshUI();
        SaveShopState();
    }

    public void OnClickedMapTheme(int index) {
        ShopItemUI.Status status = mapThemeUiList[index].curStatus;

        if(status == ShopItemUI.Status.Owned) {
            SelectAndApplyMapTheme(index);
        }
        else if(status == ShopItemUI.Status.Locked) {
            OpenBuyPrompt(ShopItemUI.Type.MapTheme, index);
        }
    }

    private void SelectAndApplyMapTheme(int index) {
        if(mapThemeUiList[index].curStatus != ShopItemUI.Status.Owned) {
            return;
        }

        // Clear previously equipped skin's status.
        for(int i = 0; i < mapThemeUiList.Length; i++) {
            if(mapThemeUiList[i].curStatus == ShopItemUI.Status.Equipped) {
                mapThemeUiList[i].SetStatus(ShopItemUI.Status.Owned);
            }
        }

        // Set new equipped skin.
        mapThemeUiList[index].SetStatus(ShopItemUI.Status.Equipped);
        PlayerPrefs.SetInt("SelectedMapTheme", index);

        // Update UI and save state.
        RefreshUI();
        SaveShopState();
    }

    private void OpenBuyPrompt(ShopItemUI.Type itemType, int index) {
        if(itemType == ShopItemUI.Type.FrogSkin) {
            if(index >= purchaseableSkins.Length) {
                DisplayAchievementError(achievementSkins[index - purchaseableSkins.Length]);
                return;
            }

            if(currentFrogPoints < purchaseableSkins[index].price) {
                DisplayPurchaseError(purchaseableSkins[index]);
                return;
            }

            indexToBuy = index;
            DisplayPurchasePrompt(purchaseableSkins[index], OnBoughtSkin);
        }
        else if(itemType == ShopItemUI.Type.MapTheme) {
            if(index >= purchaseableMapThemes.Length) {
                int achvIndex = index - purchaseableMapThemes.Length;
                DisplayAchievementError(achievementMapThemes[achvIndex]);
                return;
            }

            if(currentFrogPoints < purchaseableMapThemes[index].price) {
                DisplayPurchaseError(purchaseableMapThemes[index]);
                return;
            }

            indexToBuy = index;
            DisplayPurchasePrompt(purchaseableMapThemes[index], OnBoughtMapTheme);
        }
    }
    
    private void DisplayAchievementError(ShopItem item) {
        Achievement achv = achieve.GetAchievementData(item.achievementTag);
        string progress = string.Empty;

        if(!achv.isEventAchievement) {
            progress = "\n[FFF723]" + achv.currentValue + "/" + achv.targetValue;

            if(item.achievementTag == AchievementID.SpicyFood) {
                progress += "[-] Eaten";
            }
            else if(item.achievementTag == AchievementID.BugBuffet) {
                progress += "[-] Streak Attained";
            }
            else if(item.achievementTag == AchievementID.MovieNight) {
                progress += "[-] Watched";
            }
        }

        DialogBoxUI.instance.Display("ERROR", "Requires achievement\n[FFF723]" + achv.displayName + "[-]\n" + achv.description + progress, null);
    }

    private void DisplayPurchaseError(ShopItem item) {
        DialogBoxUI.instance.Display("ERROR", "Not enough Frog Points!\nYou currently have [FFF723]" + currentFrogPoints + "[-]\n" +
            item.name + " needs [FFF723]" + item.price + "[-]", null);
    }

    private void DisplayPurchasePrompt(ShopItem item, DialogBoxAction confirmCallback) {
        DialogBoxUI.instance.Display("CONFIRM", "Would you like to spend [FFF723]" + item.price +
            " Frog Points[-] to get [FFF723]" + item.name + "[-]?", confirmCallback, null);
    }

    public bool AwardAchievementSkins() {
        bool unlockedSomething = false;

        for(int i = 0; i < achievementSkins.Length; i++) {
            bool hasAchievementForSkin = achieve.GetAchievementData(achievementSkins[i].achievementTag).achieved;

            if(hasAchievementForSkin && skinUiList[purchaseableSkins.Length + i].curStatus == ShopItemUI.Status.Locked) {
                skinUiList[purchaseableSkins.Length + i].SetStatus(ShopItemUI.Status.Owned);
                unlockedPopupsQueue.Enqueue(GetAchievementUnlockText(achievementSkins[i]));
                unlockedSomething = true;
            }
        }

        for(int i = 0; i < achievementMapThemes.Length; i++) {
            bool hasAchievementForMapTheme = achieve.GetAchievementData(achievementMapThemes[i].achievementTag).achieved;

            if(hasAchievementForMapTheme && mapThemeUiList[purchaseableMapThemes.Length + i].curStatus == ShopItemUI.Status.Locked) {
                mapThemeUiList[purchaseableMapThemes.Length + i].SetStatus(ShopItemUI.Status.Owned);
                unlockedPopupsQueue.Enqueue(GetAchievementUnlockText(achievementMapThemes[i]));
                unlockedSomething = true;
            }
        }

        if(unlockedSomething) {
            RefreshUI();
            SaveShopState();
        }

        return unlockedSomething;
    }

    private string GetAchievementUnlockText(ShopItem item) {
        Achievement achv = achieve.GetAchievementData(item.achievementTag);
        return "Achievement [FFF723]" + achv.displayName + "[-] unlocked!\nYou can now equip [FFF723]" + item.name + "[-] in the shop!";
    }

    private void OnBoughtSkin() {
        if(indexToBuy < purchaseableSkins.Length) {
            if(currentFrogPoints < purchaseableSkins[indexToBuy].price)
                return;

            currentFrogPoints -= purchaseableSkins[indexToBuy].price;
        }

        skinUiList[indexToBuy].SetStatus(ShopItemUI.Status.Owned);
        SelectAndApplySkin(indexToBuy);

        RefreshUI();
        SaveShopState();
    }

    private void OnBoughtMapTheme() {
        if(indexToBuy < purchaseableMapThemes.Length) {
            if(currentFrogPoints < purchaseableMapThemes[indexToBuy].price)
                return;

            currentFrogPoints -= purchaseableMapThemes[indexToBuy].price;
        }

        mapThemeUiList[indexToBuy].SetStatus(ShopItemUI.Status.Owned);
        SelectAndApplyMapTheme(indexToBuy);

        RefreshUI();
        SaveShopState();
    }

    public void AwardFrogPoints(int amount) {
        if(amount <= 0) {
            return;
        }

        currentFrogPoints += amount;
        
        int totalPointsGathered = PlayerPrefs.GetInt("TotalPointsGathered", 0);
        totalPointsGathered += amount;
        PlayerPrefs.SetInt("TotalPointsGathered", totalPointsGathered);

        achieve.UpdateProgressAll(true);

        RefreshUI();
        SaveShopState();
    }

    public void PromptWatchRewardVideo() {
        DialogBoxUI.instance.Display("WATCH AD?", "Do you want to watch an advertisement video for [FFF723]20 Frog Points[-]?", AdController.instance.WatchRewardVideo, null);
    }

    public void OnEnterShop() {
        for(int i = 0; i < skinPreviewInstances.Length; i++) {
            if(skinPreviewInstances[i] != null)
                skinPreviewInstances[i].frogSkinRenderer.SetVisibility(true);
        }
    }

    public void OnExitShop() {
        for(int i = 0; i < skinPreviewInstances.Length; i++) {
            if(skinPreviewInstances[i] != null)
                skinPreviewInstances[i].frogSkinRenderer.SetVisibility(false);
        }
    }

    private void SaveShopState() {
        PlayerPrefs.SetInt("FrogPoints", currentFrogPoints);

        // Save skins status.
        for(int i = 0; i < skinUiList.Length; i++) {
            PlayerPrefs.SetInt("Skin_" + GetSkin(i).name, (int)skinUiList[i].curStatus);
        }

        // Save map theme status.
        for(int i = 0; i < mapThemeUiList.Length; i++) {
            PlayerPrefs.SetInt("Map_" + GetMapTheme(i).name, (int)mapThemeUiList[i].curStatus);
        }
    }

    private void LoadShopState() {
        currentFrogPoints = PlayerPrefs.GetInt("FrogPoints", 0);

        for(int i = 0; i < skinUiList.Length; i++) {
            string key = "Skin_" + GetSkin(i).name;

            if(PlayerPrefs.HasKey(key))
                skinUiList[i].SetStatus((ShopItemUI.Status)PlayerPrefs.GetInt(key));
        }

        SelectAndApplySkin(PlayerPrefs.GetInt("SelectedSkin", 0));
        
        for(int i = 0; i < mapThemeUiList.Length; i++) {
            string key = "Map_" + GetMapTheme(i).name;

            if(PlayerPrefs.HasKey(key))
                mapThemeUiList[i].SetStatus((ShopItemUI.Status)PlayerPrefs.GetInt(key));
        }
        
        SelectAndApplyMapTheme(PlayerPrefs.GetInt("SelectedMapTheme", 0));
    }

    private SkinItem GetSkin(int uiIndex) {
        if(uiIndex < purchaseableSkins.Length)
            return purchaseableSkins[uiIndex];
        else
            return achievementSkins[uiIndex - purchaseableSkins.Length];
    }

    private MapTheme GetMapTheme(int uiIndex) {
        if(uiIndex < purchaseableMapThemes.Length)
            return purchaseableMapThemes[uiIndex];
        else
            return achievementMapThemes[uiIndex - purchaseableMapThemes.Length];
    }

    [System.Serializable]
    public class ShopItem {
        public string name;
        public string spriteName;
        public int price;
        public bool unlockWithAchievement;
        public AchievementID achievementTag;
    }

    [System.Serializable]
    public class SkinItem : ShopItem {
        public bool animatedPreview;
        public FrogSkinData skinProperties;
    }

    [System.Serializable]
    public class MapTheme : ShopItem {
        public MapThemeData mapProperties;
    }
}