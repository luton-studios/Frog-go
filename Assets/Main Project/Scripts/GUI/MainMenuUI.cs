using UnityEngine;
using System.Collections.Generic;
using TrinarySoftware;

public class MainMenuUI : MonoBehaviour {
    public enum MenuWindow { Main, Settings, StatsAndAchievements, Leaderboards, Shop, Credits };

    public GameController game;
    public ShopController shop;
    public AchievementController achieve;
    public PlayServicesController gpgs;
    public Camera menuBgCam;
    public UICamera uiCamEvents;
    public GameObject menuPanelGo;
    public Transform menuTransform;
    public UITexture menuBackground;
    public float transitionSpeed = 2f;
    public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0f, 0f, 1f, 720f);
    public AnimationCurve fadeInCurve = AnimationCurve.Linear(1f, 720f, 0f, 0f);
    public UILabel headerLabel;
    public string[] headerTexts = new string[1];
    public UILabel versionLabel;
    public UILabel statsLabel;
    public AchievementItemUI achievementUiPrefab;
    public Transform achievementsListStart;
    public UILabel achievedCountLabel;
    public float achievementItemSpacing = 75f;
    public GameObject[] menuRoots;

    private RenderTexture bgTexture;
    private AchievementItemUI[] achieveItemUiList;
    private MenuWindow currentMenu;
    private Vector3 defaultMenuPos;
    private bool inTransition;

    private void Awake() {
        // Cache instance at the start.
        Timing init = Timing.Instance;

        game.ToggleGameState(false);
        defaultMenuPos = menuTransform.localPosition;
        versionLabel.text = "Version " + Application.version;

        // Setup achievements list.
        achieveItemUiList = new AchievementItemUI[achieve.allAchievements.Length];

        for(int i = 0; i < achieveItemUiList.Length; i++) {
            AchievementItemUI itemInst = Instantiate(achievementUiPrefab);
            itemInst.cachedTrans.parent = achievementsListStart;
            itemInst.cachedTrans.localPosition = new Vector3(0f, -i * achievementItemSpacing, 0f);
            itemInst.cachedTrans.localScale = Vector3.one;
            achieveItemUiList[i] = itemInst;
        }
        
        SelectNewHeader();
        SelectMenu(MenuWindow.Main);
    }

    private void Start() {
        // Setup menu background.
        bgTexture = new RenderTexture(menuBgCam.pixelWidth, menuBgCam.pixelHeight, 16, RenderTextureFormat.ARGB32);
        bgTexture.hideFlags = HideFlags.HideAndDontSave;
        bgTexture.filterMode = FilterMode.Point;
        bgTexture.autoGenerateMips = false;
        menuBgCam.targetTexture = bgTexture;
        menuBgCam.ResetAspect();
        menuBackground.mainTexture = bgTexture;
    }

    private void Update() {
        if(Input.GetKeyDown(KeyCode.Escape) && !DialogBoxUI.instance.isVisible) {
            if(game.enabled) {
                // In game.
                game.OnPhoneBackButton();
            }
            else {
                // In menu.
                if(currentMenu == MenuWindow.Main)
                    ExitGame();
                else
                    SelectMenu(MenuWindow.Main);
            }
        }
    }

    public void StartGame() {
        if(inTransition)
            return;

        Timing.RunCoroutine(StartGameRoutine());
    }

    private IEnumerator<float> StartGameRoutine() {
        inTransition = true;
        uiCamEvents.enabled = false;
        menuBgCam.enabled = false;

        AdController.instance.StartBannerSession();
        game.ToggleGameState(true);
        yield return 0f; // Wait a frame to avoid hitches during animation.

        float transitionTime = 0f;

        while(transitionTime < 1f) {
            transitionTime += Time.unscaledDeltaTime * transitionSpeed;
            float y = fadeOutCurve.Evaluate(transitionTime);
            menuTransform.localPosition = defaultMenuPos + new Vector3(0f, y, 0f);
            yield return 0f;
        }
        
        menuPanelGo.SetActive(false);
        uiCamEvents.enabled = true;
        inTransition = false;
    }

    public void ShowMenu() {
        if(inTransition)
            return;
        
        Timing.RunCoroutine(ShowMenuRoutine());
    }

    private IEnumerator<float> ShowMenuRoutine() {
        inTransition = true;
        Time.timeScale = 1f;

        // Resume music.
        AudioListener.pause = false;

        if(!game.musicSource.isPlaying)
            game.musicSource.Play();

        menuBgCam.enabled = true;
        uiCamEvents.enabled = false;
        SelectMenu(MenuWindow.Main);
        SelectNewHeader();

        AdController.instance.EndBannerSession();
        menuPanelGo.SetActive(true);
        yield return 0f; // Wait a frame to avoid hitches during animation.

        float transitionTime = 0f;

        while(transitionTime < 1f) {
            transitionTime += Time.unscaledDeltaTime * transitionSpeed;
            float y = fadeInCurve.Evaluate(transitionTime);
            menuTransform.localPosition = defaultMenuPos + new Vector3(0f, y, 0f);
            yield return 0f;
        }

        game.ToggleGameState(false);
        uiCamEvents.enabled = true;
        inTransition = false;
    }

    public void MainMenu() {
        SelectMenu(MenuWindow.Main);
    }

    public void SettingsMenu() {
        SelectMenu(MenuWindow.Settings);
    }

    public void CreditsMenu() {
        SelectMenu(MenuWindow.Credits);
    }

    public void ShopMenu() {
        shop.RefreshUI();
        SelectMenu(MenuWindow.Shop);
    }

    public void StatsAndAchievementsMenu() {
        // Update UI.
        string statsText = "FROG POINTS GATHERED: [FFF723]" + PlayerPrefs.GetInt("TotalPointsGathered", 0).ToString("N0") + "[-]";
        statsText += "\nHIGH SCORE: [FFF723]" + PlayerPrefs.GetInt("HighScore", 0).ToString("N0") + "[-]";
        statsText += "\nHIGHEST DIFFICULTY: [FFF723]LEVEL " + PlayerPrefs.GetInt("RecordDifficultyReached", 0).ToString("N0") + "[-]";
        statsText += "\nRECORD LEAP STREAK: [FFF723]" + PlayerPrefs.GetInt("RecordLeapStreak", 0).ToString("N0") + "[-]";
        statsText += "\nRECORD LEAP SCORE: [FFF723]" + PlayerPrefs.GetInt("RecordLeapScore", 0).ToString("N0") + "[-]";
        statsText += "\nTOTAL BUGS EATEN: [FFF723]" + PlayerPrefs.GetInt("TotalBugsEaten", 0).ToString("N0") + "[-]";
        statsText += "\nFIREFLIES EATEN: [FFF723]" + PlayerPrefs.GetInt("FirefliesEaten", 0).ToString("N0") + "[-]";
        statsLabel.text = statsText;

        achieve.UpdateProgressAll(true);
        int numAchieved = 0;

        for(int i = 0; i < achieveItemUiList.Length; i++) {
            achieveItemUiList[i].Refresh(achieve.allAchievements[i]);

            if(achieve.allAchievements[i].achieved)
                numAchieved++;
        }

        achievedCountLabel.text = numAchieved + "/" + achieve.allAchievements.Length + " ACHIEVED";

        SelectMenu(MenuWindow.StatsAndAchievements);
    }

    public void LeaderboardsMenu() {
        SelectMenu(MenuWindow.Leaderboards);
        gpgs.SelectLeaderboardType(0); // Reset to first leaderboard.
        gpgs.ReloadCurrentLeaderboard();
    }

    public void ExitGame() {
        DialogBoxUI.instance.Display("CONFIRM QUIT", "Are you sure you want to quit Frog-go?", Application.Quit, null);
    }
    
    public void OpenLutonStudiosYouTube_Confirm() {
        DialogBoxUI.instance.Display("OPENING YOUTUBE", "Do you want to leave the app to visit Luton Studios YouTube Channel?", OpenLutonStudiosYouTube, null);
    }

    private void OpenLutonStudiosYouTube() {
        AchievementController.instance.AwardEventAchievement(AchievementID.SupportUs, true);
        Application.OpenURL("https://youtube.com/LutonStudios");
    }
    
    private void SelectNewHeader() {
        int randomIndex = Random.Range(0, headerTexts.Length);
        headerLabel.text = headerTexts[randomIndex];
    }

    private void SelectMenu(MenuWindow window) {
        if(currentMenu != MenuWindow.Shop && window == MenuWindow.Shop)
            shop.OnEnterShop();
        else if(currentMenu == MenuWindow.Shop && window != MenuWindow.Shop)
            shop.OnExitShop();

        currentMenu = window;
        int windowIndex = (int)window;

        for(int i = 0; i < menuRoots.Length; i++) {
            menuRoots[i].SetActive(i == windowIndex);
        }
    }
}