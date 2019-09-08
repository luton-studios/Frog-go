using UnityEngine;
using UnityEngine.SocialPlatforms;
using System.Collections.Generic;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

public class PlayServicesController : MonoBehaviour {
    public enum LeaderboardLoadState { LoadingValues, LoadingUsers, DoneLoading };

    private const int LEADERBOARD_MAX_PAGE = 20; // Only show top 1000.
    private const int LEADERBOARD_ENTRIES_PER_PAGE = 50;
    private const long MAX_LEADERBOARD_SCORE = int.MaxValue; // Global maximum: Clamp to int maximum.

    public GameSettings settingsController;
    public LeaderboardItemUI leaderboardItemPrefab;
    public Transform leaderboardListStart;
    public Transform leaderboardCurPlayerStart;
    public UILabel leaderboardTypeLabel;
    public UILabel leaderboardListMessageLabel;
    public UIPopupList leaderboardTimeFrame;
    public UIPopupList leaderboardCollection;
    public UIButton prevPageButton;
    public UIButton nextPageButton;
    public float leaderboardListSpacing = 50f;
    public float leaderboardButtonHeight = 45f;
    public LeaderboardSelection[] leaderboards;
    
    private bool leaderboardIsLoading {
        get {
            return (leaderboardListLoadState != LeaderboardLoadState.DoneLoading || leaderboardUserLoadState != LeaderboardLoadState.DoneLoading);
        }
    }

    private bool leaderboardHasPrevPage {
        get {
            return (leaderboardListLoadState == LeaderboardLoadState.DoneLoading && leaderboardCurPage > 1 && leaderboardData != null);
        }
    }

    private bool leaderboardHasNextPage {
        get {
            return (leaderboardListLoadState == LeaderboardLoadState.DoneLoading && leaderboardCurPage < leaderboardPageCount && leaderboardData != null);
        }
    }

    private Dictionary<string, long> highestSubmittedScores; // Highest submitted scores so far for leaderboards.
    private int curLeaderboardIndex;
    private LeaderboardLoadState leaderboardListLoadState;
    private LeaderboardLoadState leaderboardUserLoadState;
    private LeaderboardCollection leaderboardCollectionSetting;
    private LeaderboardTimeSpan leaderboardTimeSpanSetting;
    private LeaderboardScoreData leaderboardData;
    private LeaderboardResult[] leaderboardResults;
    private LeaderboardItemUI[] leaderboardListUi;
    private LeaderboardItemUI curUserLeaderboardItem;
    private ILeaderboard curUserLeaderboard; // Leaderboard containing only the current logged in user.
    private string[] curUserFilter;
    private bool initPlayServices;
    private bool userSignedIn;
    private bool waitingForAuth;
    private bool canLoadLeaderboards;
    private int leaderboardCurPage;
    private int leaderboardPageCount;
    private int leaderboardCurPageEntryCount;

    private void Awake() {
        userSignedIn = false;
        waitingForAuth = false;
        
        // Configurate and activate Google Play Games services.
        PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
            .EnableSavedGames()
            .Build();

        PlayGamesPlatform.InitializeInstance(config);
        PlayGamesPlatform.Activate();
        
        int signInState = PlayerPrefs.GetInt("GPGS_SignInState", 1);

        if(signInState == 1) {
            // Automatically sign into Google Play Games.
            PerformSignIn();
        }
    }

    // Initialize Play Services variables upon login.
    private void InitializeServicesVariables() {
        if(initPlayServices)
            return;

        initPlayServices = true;
        highestSubmittedScores = new Dictionary<string, long>();
        leaderboardData = null;
        curLeaderboardIndex = 0;
        leaderboardListLoadState = LeaderboardLoadState.DoneLoading;
        leaderboardUserLoadState = LeaderboardLoadState.DoneLoading;
        leaderboardCollectionSetting = LeaderboardCollection.Public;
        leaderboardTimeSpanSetting = LeaderboardTimeSpan.AllTime;

        // Initialize data instances for each entry in a page.
        leaderboardResults = new LeaderboardResult[LEADERBOARD_ENTRIES_PER_PAGE];
        leaderboardListUi = new LeaderboardItemUI[LEADERBOARD_ENTRIES_PER_PAGE];
        leaderboardPageCount = 0;
        curUserFilter = new string[1] { PlayGamesPlatform.Instance.localUser.id };

        // Configure dropdowns for leaderboards.
        leaderboardTimeFrame.ForceItems("Today", "This Week", "All Time");
        leaderboardCollection.ForceItems("Global", "Friends");
        leaderboardTimeFrame.SelectItem(2); // All Time.
        leaderboardCollection.SelectItem(0); // Global.

        for(int i = 0; i < LEADERBOARD_ENTRIES_PER_PAGE; i++) {
            leaderboardResults[i] = new LeaderboardResult();

            leaderboardListUi[i] = SetupLeaderboardListItem(leaderboardListStart);
            leaderboardListUi[i].cachedGo.SetActive(false);
        }

        curUserLeaderboardItem = SetupLeaderboardListItem(leaderboardCurPlayerStart);
        curUserLeaderboardItem.cachedGo.SetActive(false);
    }

    private LeaderboardItemUI SetupLeaderboardListItem(Transform targetParent) {
        LeaderboardItemUI newInst = Instantiate(leaderboardItemPrefab);
        newInst.cachedTrans.parent = targetParent;
        newInst.cachedTrans.localPosition = Vector3.zero;
        newInst.cachedTrans.localScale = Vector3.one;

        return newInst;
    }
    
    public void Button_SignInOrOut() {
        if(userSignedIn) {
            // Prompt to sign out.
            DialogBoxUI.instance.Display("CONFIRM SIGN OUT", "Are you sure? You will not be able to save progress or use services such as leaderboards.", PerformSignOut, null);
        }
        else {
            PerformSignIn();
        }
    }

    public void SelectLeaderboardType(int index) {
        curLeaderboardIndex = Mathf.Clamp(index, 0, leaderboards.Length - 1);
    }

    public void ReloadCurrentLeaderboard() {
        if(leaderboardIsLoading)
            return; // Do not reload while leaderboard is loading.

        canLoadLeaderboards = true;
        leaderboardTypeLabel.text = leaderboards[curLeaderboardIndex].displayName;

        if(!userSignedIn) {
            leaderboardListMessageLabel.enabled = true;
            ShowLeaderboardListMessage("Not signed into Google Play Games");
            return;
        }
        
        string id = leaderboards[curLeaderboardIndex].leaderboardID;
        FetchLeaderboardFromTop(id, leaderboardCollectionSetting, leaderboardTimeSpanSetting);
        FetchCurrentUserScore(id, leaderboardTimeSpanSetting);
    }

    private void FetchLeaderboardFromTop(string id, LeaderboardCollection collection, LeaderboardTimeSpan timeSpan) {
        if(!userSignedIn)
            return;

        leaderboardListLoadState = LeaderboardLoadState.LoadingValues;
        leaderboardCurPage = 1;
        leaderboardPageCount = 1;
        SetLeaderboardListLoading(true);
        PlayGamesPlatform.Instance.LoadScores(id, LeaderboardStart.TopScores, LEADERBOARD_ENTRIES_PER_PAGE, collection, timeSpan, OnLoadedTopLeaderboards);
    }

    private void OnLoadedTopLeaderboards(LeaderboardScoreData lbData) {
        if(!userSignedIn || leaderboardListLoadState != LeaderboardLoadState.LoadingValues || !canLoadLeaderboards)
            return; // Abort. Something went horribly wrong.

        if(!lbData.Valid || lbData.Scores.Length > LEADERBOARD_ENTRIES_PER_PAGE) {
            ShowLeaderboardListMessage("Invalid leaderboard data");
            return;
        }

        leaderboardData = lbData; // The full leaderboard data.
        leaderboardCurPageEntryCount = Mathf.Min(lbData.Scores.Length, LEADERBOARD_ENTRIES_PER_PAGE);
        leaderboardPageCount = Mathf.Min(Mathf.CeilToInt(lbData.ApproximateCount / (float)LEADERBOARD_ENTRIES_PER_PAGE), LEADERBOARD_MAX_PAGE);
        Debug.Log("Loaded leaderboard has approximately: " + lbData.ApproximateCount + " entries (" + leaderboardPageCount + " pages)");

        string[] userIDs = new string[leaderboardCurPageEntryCount];

        for(int i = 0; i < leaderboardCurPageEntryCount; i++) {
            // We do not know their names, yet. Fill in their data.
            userIDs[i] = lbData.Scores[i].userID;
            leaderboardResults[i].rank = lbData.Scores[i].rank;
            leaderboardResults[i].score = lbData.Scores[i].value;
        }

        leaderboardListLoadState = LeaderboardLoadState.LoadingUsers;
        PlayGamesPlatform.Instance.LoadUsers(userIDs, OnLoadedLeaderboardUsers);
    }

    private void OnLoadedLeaderboardUsers(IUserProfile[] profiles) {
        if(!userSignedIn || leaderboardListLoadState != LeaderboardLoadState.LoadingUsers || !canLoadLeaderboards || profiles.Length != leaderboardCurPageEntryCount)
            return; // Abort. Something went horribly wrong.

        // Finished list.
        leaderboardListLoadState = LeaderboardLoadState.DoneLoading;
        SetLeaderboardListLoading(false);

        if(leaderboardCurPageEntryCount > 0) {
            // Update and layout list UI.
            float y = 0f;

            if(leaderboardHasPrevPage) {
                prevPageButton.gameObject.SetActive(true);
                prevPageButton.transform.localPosition = new Vector3(0f, y, 0f);
                y -= leaderboardButtonHeight;
            }

            for(int i = 0; i < LEADERBOARD_ENTRIES_PER_PAGE; i++) {
                bool active = (i < leaderboardCurPageEntryCount);
                leaderboardListUi[i].cachedGo.SetActive(active);

                if(active) {
                    leaderboardResults[i].username = profiles[i].userName;

                    leaderboardListUi[i].cachedTrans.localPosition = new Vector3(0f, y, 0f);
                    leaderboardListUi[i].Refresh(leaderboardResults[i]);
                    y -= leaderboardListSpacing;
                }
            }

            if(leaderboardHasNextPage) {
                nextPageButton.gameObject.SetActive(true);
                nextPageButton.transform.localPosition = new Vector3(0f, y, 0f);
            }
        }
        else {
            ShowLeaderboardListMessage("No results");
        }
    }

    public void LoadPreviousLeaderboardPage() {
        if(!leaderboardHasPrevPage || leaderboardIsLoading || !canLoadLeaderboards)
            return;

        Debug.Log("Loading previous page: " + leaderboardData.Title);
        leaderboardCurPage--;
        leaderboardListLoadState = LeaderboardLoadState.LoadingValues;
        PlayGamesPlatform.Instance.LoadMoreScores(leaderboardData.PrevPageToken, LEADERBOARD_ENTRIES_PER_PAGE, OnLoadedTopLeaderboards);
    }

    public void LoadNextLeaderboardPage() {
        if(!leaderboardHasNextPage || leaderboardIsLoading || !canLoadLeaderboards)
            return;
        
        Debug.Log("Loading next page: " + leaderboardData.Title);
        leaderboardCurPage++;
        leaderboardListLoadState = LeaderboardLoadState.LoadingValues;
        PlayGamesPlatform.Instance.LoadMoreScores(leaderboardData.NextPageToken, LEADERBOARD_ENTRIES_PER_PAGE, OnLoadedTopLeaderboards);
    }

    public void SubmitScoreToLeaderboard(string id, long score) {
        if(!userSignedIn || score <= 0 || score > MAX_LEADERBOARD_SCORE)
            return;

        long highestSubmitted;
        highestSubmittedScores.TryGetValue(id, out highestSubmitted); // Get the highest score. Otherwise, return 0.

        if(score > highestSubmitted) {
            Social.ReportScore(score, id, OnLeaderboardScoreSubmitted);
            highestSubmittedScores[id] = score;
        }
    }

    private void FetchCurrentUserScore(string id, LeaderboardTimeSpan timeSpan) {
        if(!userSignedIn || leaderboardUserLoadState != LeaderboardLoadState.DoneLoading)
            return;

        leaderboardUserLoadState = LeaderboardLoadState.LoadingValues;
        curUserLeaderboardItem.cachedGo.SetActive(false); // Hide current result while loading.

        curUserLeaderboard = PlayGamesPlatform.Instance.CreateLeaderboard();
        curUserLeaderboard.id = id;
        curUserLeaderboard.range = new Range(int.MaxValue, 0); // We are only getting our local value. So this range doesn't matter.
        curUserLeaderboard.SetUserFilter(curUserFilter);
        curUserLeaderboard.timeScope = TimeSpanToTimeScope(timeSpan);
        curUserLeaderboard.LoadScores(OnLoadedCurrentUserScore);
    }

    private TimeScope TimeSpanToTimeScope(LeaderboardTimeSpan timeSpan) {
        if(timeSpan == LeaderboardTimeSpan.Daily)
            return TimeScope.Today;
        if(timeSpan == LeaderboardTimeSpan.Weekly)
            return TimeScope.Week;

        return TimeScope.AllTime;
    }

    private void OnLoadedCurrentUserScore(bool success) {
        if(!userSignedIn || leaderboardUserLoadState != LeaderboardLoadState.LoadingValues || !canLoadLeaderboards || !success)
            return;

        leaderboardUserLoadState = LeaderboardLoadState.DoneLoading;
        IScore userScore = curUserLeaderboard.localUserScore;
        curUserLeaderboardItem.Refresh(userScore.rank, "You", userScore.value);
        curUserLeaderboardItem.cachedGo.SetActive(true);
    }
    
    private void OnLeaderboardScoreSubmitted(bool success) {
        // Does nothing useful so far except print an error.
        if(!success) {
            Debug.LogError("Failed to submit score to some leaderboard!");
        }
    }

    public void LoadPreviousLeaderboard() {
        if(!userSignedIn || leaderboardIsLoading)
            return;

        curLeaderboardIndex--;

        if(curLeaderboardIndex < 0)
            curLeaderboardIndex = leaderboards.Length - 1; // Wrap around.

        ReloadCurrentLeaderboard();
    }

    public void LoadNextLeaderboard() {
        if(!userSignedIn || leaderboardIsLoading)
            return;

        curLeaderboardIndex++;

        if(curLeaderboardIndex >= leaderboards.Length)
            curLeaderboardIndex = 0; // Wrap around.

        ReloadCurrentLeaderboard();
    }

    public void OnNewLeaderboardTimeFrame() {
        LeaderboardTimeSpan newSpan = (LeaderboardTimeSpan)(leaderboardTimeFrame.selectionIndex + 1); // Enum starts at 1.

        if(leaderboardTimeSpanSetting == newSpan)
            return; // No need to refresh if the setting didn't change.

        leaderboardTimeSpanSetting = newSpan;

        if(!userSignedIn || leaderboardIsLoading || leaderboardData == null)
            return;

        ReloadCurrentLeaderboard();
    }

    public void OnNewLeaderboardCollection() {
        LeaderboardCollection newCollection = (LeaderboardCollection)(leaderboardCollection.selectionIndex + 1); // Enum starts at 1.

        if(leaderboardCollectionSetting == newCollection)
            return; // No need to refresh if the setting didn't change.

        leaderboardCollectionSetting = newCollection;

        if(!userSignedIn || leaderboardIsLoading || leaderboardData == null)
            return;

        ReloadCurrentLeaderboard();
    }

    private void SetLeaderboardListLoading(bool loading) {
        if(loading) {
            leaderboardTimeFrame.GetComponent<BoxCollider>().enabled = false;
            leaderboardCollection.GetComponent<BoxCollider>().enabled = false;
            ShowLeaderboardListMessage("Loading...");
        }
        else {
            leaderboardTimeFrame.GetComponent<BoxCollider>().enabled = true;
            leaderboardCollection.GetComponent<BoxCollider>().enabled = true;
            leaderboardListMessageLabel.enabled = false;
        }
    }

    private void ShowLeaderboardListMessage(string message) {
        leaderboardListMessageLabel.enabled = true;
        leaderboardListMessageLabel.text = message;

        if(initPlayServices) {
            // Deactivate the list to display the message.
            for(int i = 0; i < LEADERBOARD_ENTRIES_PER_PAGE; i++) {
                leaderboardListUi[i].cachedGo.SetActive(false);
            }
        }
    }

    // Cancel loading when pressing the back button.
    public void CancelLeaderboardLoading() {
        leaderboardListLoadState = LeaderboardLoadState.DoneLoading;
        leaderboardUserLoadState = LeaderboardLoadState.DoneLoading;
        canLoadLeaderboards = false;
    }
    
    private void PerformSignIn() {
        if(userSignedIn || waitingForAuth)
            return;

        waitingForAuth = true;

        if(Application.isEditor) {
            Debug.Log("[Editor] Performing sign in.");
            userSignedIn = false;
        }
        else {
            PlayGamesPlatform.Instance.Authenticate(OnUserSignedIn, false);
        }

        UpdateSignedInUI();
    }

    private void PerformSignOut() {
        if(!userSignedIn || waitingForAuth)
            return;

        if(Application.isEditor) {
            Debug.Log("[Editor] Performing sign out.");
            userSignedIn = false;
            UpdateSignedInUI();
            return; // Doesn't make sense to sign out when using editor.
        }

        PlayGamesPlatform.Instance.SignOut();
        OnUserSignedOut();
    }

    private void OnUserSignedIn(bool success) {
        waitingForAuth = false;

        if(success) {
            PlayerPrefs.SetInt("GPGS_SignInState", 1); // Set to auto sign-in the next time they open the game.
            userSignedIn = PlayGamesPlatform.Instance.localUser.authenticated;

            if(userSignedIn)
                InitializeServicesVariables();
        }
        else {
            userSignedIn = false;
        }

        UpdateSignedInUI();
    }

    private void OnUserSignedOut() {
        PlayerPrefs.SetInt("GPGS_SignInState", 0); // Once signed out, do not auto sign-in next time.
        userSignedIn = false;
        UpdateSignedInUI();

        if(initPlayServices) {
            // Clear leaderboard state.
            leaderboardData = null;
            curLeaderboardIndex = 0;
            leaderboardPageCount = 0;
            leaderboardListLoadState = LeaderboardLoadState.DoneLoading;
            leaderboardUserLoadState = LeaderboardLoadState.DoneLoading;

            // Reset dropdown selections for leaderboards.
            leaderboardTimeFrame.SelectItem(2); // All Time.
            leaderboardCollection.SelectItem(0); // Global.
        }
    }

    private void UpdateSignedInUI() {
        foreach(GameSettings.UIReferences uiRef in settingsController.uiReferences) {
            if(userSignedIn) {
                uiRef.signedInAs.text = "Signed in as: " + PlayGamesPlatform.Instance.localUser.userName;
                uiRef.signInOutButtonLabel.text = "Sign Out";
            }
            else {
                uiRef.signedInAs.text = "Not signed in";
                uiRef.signInOutButtonLabel.text = "Sign In";
            }

            if(waitingForAuth) {
                uiRef.signInOutButton.isEnabled = false; // Don't allow spamming of button.
                uiRef.signInOutButtonLabel.alpha = uiRef.signInOutButtonLabel.defaultAlpha * 0.6f;
            }
            else {
                uiRef.signInOutButton.isEnabled = true;
                uiRef.signInOutButtonLabel.alpha = uiRef.signInOutButtonLabel.defaultAlpha;
            }
        }
    }

    [System.Serializable]
    public class LeaderboardSelection {
        public string displayName;
        public string leaderboardID;
    }
}

public class LeaderboardResult {
    public int rank;
    public string username;
    public long score;
}