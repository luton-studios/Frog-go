using UnityEngine;
using System.Collections.Generic;
using TrinarySoftware;

public class GameUI : MonoBehaviour {
    public enum PauseMenuWindow { Main, Settings };

    public UIRoot root;
    public GameController game;
    public ShopController shop;
    public AchievementController achieve;
    public GameObject hudPanelGo;
    public Transform[] adOffsets;
    public UILabel scoreLabel;
    public UILabel perfStreakLabel;
    public Color perfJumpFlashColor = new Color(1f, 0f, 0f);
    public GameObject topLeftHud;
    public GameObject topRightHud;
    public UILabel addScoreLabel;
    public Color addScorePerfectColor = Color.yellow;
    public AnimationCurve addScoreAnimCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public GameObject cancelArea;
    public UISprite cancelAreaBG;
    public Color cancelHighlightColor = Color.red;
    public GameObject scoreScreen;
    public Transform scoreScreenTransform;
    public AnimationCurve scoreScreenAnimCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float scoreScreenAnimSpeed = 1.8f;
    public UILabel scoreScreenTitle;
    public UILabel scoreScreenStats;
    public UILabel scoreScreenFrogPoints;
    public UILabel scoreScreenBreakdown;
    public GameObject pauseScreen;
    public GameObject[] pauseMenuRoots;
    public Transform pauseScreenTransform;
    public AnimationCurve pauseScreenAnimCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float pauseScreenAnimSpeed = 1.8f;

    public bool inMenu { get; private set; }
    public float curBannerAdHeight { get; private set; }

    private bool paused {
        get {
            return pauseScreen.activeInHierarchy;
        }
    }

    private Vector3 addScoreLabelPosition {
        get {
            return scoreLabel.cachedTrans.localPosition + new Vector3(scoreLabel.width, 0f, 0f);
        }
    }

    private CoroutineHandle scoreScreenRoutine;
    private Vector3 defaultScoreScreenPos;
    private Vector3 defaultPauseScreenPos;
    private float scoreRewardTime;

    private void Awake() {
        ToggleGameUI(false);
        inMenu = false;

        defaultScoreScreenPos = scoreScreenTransform.localPosition;
        defaultPauseScreenPos = pauseScreenTransform.localPosition;
    }

    private void Update() {
        // Score reward positioning.
        float rewardAnimTime = Time.time - scoreRewardTime;

        if(rewardAnimTime >= 0f && rewardAnimTime <= 1f) {
            addScoreLabel.enabled = true;
            Vector3 addScoreLabelPos = addScoreLabelPosition;
            float anim = addScoreAnimCurve.Evaluate(rewardAnimTime);
            addScoreLabelPos.y += anim * 25f;
            addScoreLabel.cachedTrans.localPosition = addScoreLabelPos;
            addScoreLabel.alpha = 1f - anim;
        }
        else {
            addScoreLabel.enabled = false;
        }

        if(perfStreakLabel.enabled)
            perfStreakLabel.color = Color.Lerp(perfStreakLabel.color, Color.white, Time.deltaTime * 4f);
    }

    public void UpdateScoreLabels(int score, int reward, bool perfectJump) {
        scoreLabel.text = "Score: " + score;
        addScoreLabel.text = "+" + reward;
        scoreRewardTime = Time.time;

        if(perfectJump)
            addScoreLabel.color = addScorePerfectColor; // yellow
        else
            addScoreLabel.color = addScoreLabel.defaultColor; // green
    }

    public void UpdateLeapStreak(int streak, bool streakEnded) {
        if(streakEnded) {
            perfStreakLabel.enabled = false;
        }
        else {
            perfStreakLabel.enabled = true;
            perfStreakLabel.text = "Leap Streak: " + streak;
            perfStreakLabel.color = perfJumpFlashColor; // Flash streak label.
        }
    }

    public void TogglePauseMenu() {
        if(GameController.inTransitionOrBusy)
            return;

        if(!paused) {
            if(!inMenu)
                Timing.RunCoroutine(PauseRoutine());
        }
        else {
            if(inMenu)
                Timing.RunCoroutine(ResumeRoutine());
        }
    }

    private IEnumerator<float> PauseRoutine() {
        GameController.inTransitionOrBusy = true;
        inMenu = true;

        pauseScreen.SetActive(true);
        SelectPauseMenuWindow(PauseMenuWindow.Main);
        ToggleGameHUD(false);
        Time.timeScale = 0f;
        AudioListener.pause = true;

        float animTime = 0f;

        while(animTime < 1f) {
            animTime += Time.unscaledDeltaTime * pauseScreenAnimSpeed;
            float x = pauseScreenAnimCurve.Evaluate(animTime);
            pauseScreenTransform.localPosition = defaultPauseScreenPos + new Vector3(x, 0f, 0f);
            yield return 0f;
        }

        GameController.inTransitionOrBusy = false;
    }

    private IEnumerator<float> ResumeRoutine() {
        GameController.inTransitionOrBusy = true;
        float animTime = 1f;
        inMenu = false;

        while(animTime > 0f) {
            animTime -= Time.unscaledDeltaTime * pauseScreenAnimSpeed;
            float x = pauseScreenAnimCurve.Evaluate(animTime);
            pauseScreenTransform.localPosition = defaultPauseScreenPos + new Vector3(x, 0f, 0f);
            yield return 0f;
        }

        ToggleGameHUD(true);
        pauseScreen.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameController.inTransitionOrBusy = false;
    }

    public void ShowScoreScreen(GameSessionStats stats, bool drowned) {
        if(GameController.inTransitionOrBusy)
            return;

        scoreScreenRoutine = Timing.RunCoroutine(ScoreScreenRoutine(stats, drowned));
    }

    private IEnumerator<float> ScoreScreenRoutine(GameSessionStats gameStats, bool drowned) {
        GameController.inTransitionOrBusy = true;
        inMenu = true;

        pauseScreen.SetActive(false);
        scoreScreen.SetActive(true);
        ToggleGameHUD(false);
        scoreScreenBreakdown.alpha = 0f;
        scoreScreenTitle.text = (drowned) ? "YOU DROWNED" : "YOU GAVE UP";

        int displayedFrogPoints = shop.currentFrogPoints;
        scoreScreenFrogPoints.text = displayedFrogPoints.ToString();

        int highScore = PlayerPrefs.GetInt("HighScore", 0);
        int recordLeapStreak = PlayerPrefs.GetInt("RecordLeapStreak", 0);
        int recordLeapScore = PlayerPrefs.GetInt("RecordLeapScore", 0);
        int recordDifficulty = PlayerPrefs.GetInt("RecordDifficultyReached", 0);
        int recordNearMissStreak = PlayerPrefs.GetInt("RecordNearMissStreak", 0);
        int totalBugsEaten = PlayerPrefs.GetInt("TotalBugsEaten", 0);
        int totalFirefliesEaten = PlayerPrefs.GetInt("FirefliesEaten", 0);

        string highScoreColor = "21CE52";

        // Set highscores.
        if(gameStats.finalScore > highScore) {
            highScore = gameStats.finalScore;
            PlayerPrefs.SetInt("HighScore", highScore);
            highScoreColor = "F2C637"; // Glow orange when achieved a new highscore.
        }

        if(gameStats.highestNearMissStreak > recordNearMissStreak) {
            recordNearMissStreak = gameStats.highestNearMissStreak;
            PlayerPrefs.SetInt("RecordNearMissStreak", recordNearMissStreak);
        }

        totalBugsEaten += gameStats.bugsEaten;
        PlayerPrefs.SetInt("TotalBugsEaten", totalBugsEaten);

        totalFirefliesEaten += gameStats.firefliesEaten;
        PlayerPrefs.SetInt("FirefliesEaten", totalFirefliesEaten);

        string stats = "Score: [21CE52]" + gameStats.finalScore + "[-]";
        stats += "\nHigh Score: [" + highScoreColor + "]" + highScore + "[-]";
        stats += "\nHighest Leap Streak: [21CE52]" + gameStats.highestLeapStreak + "[-]";
        stats += "\nHighest Leap Score: [21CE52]" + gameStats.highestLeapScore + "[-]";
        scoreScreenStats.text = stats;

        // Post values to leaderboard.
        game.gpgs.SubmitScoreToLeaderboard(GPGSIds.leaderboard_high_scores, gameStats.finalScore);
        game.gpgs.SubmitScoreToLeaderboard(GPGSIds.leaderboard_leap_streak, gameStats.highestLeapStreak);
        game.gpgs.SubmitScoreToLeaderboard(GPGSIds.leaderboard_leap_score, gameStats.highestLeapScore);
        game.gpgs.SubmitScoreToLeaderboard(GPGSIds.leaderboard_highest_difficulty, gameStats.highestDifficulty);

        // Award achievements and skins.
        achieve.UpdateProgressAll(true);

        // Awards points and returns score breakdown as a dictionary.
        Dictionary<string, int> breakdown = GetBreakdownAndAwardPoints(gameStats);
        
        float animTime = 0f;

        while(animTime < 1f) {
            animTime += Time.unscaledDeltaTime * scoreScreenAnimSpeed;
            float x = scoreScreenAnimCurve.Evaluate(animTime);
            scoreScreenTransform.localPosition = defaultScoreScreenPos + new Vector3(x, 0f, 0f);
            yield return 0f;
        }

        GameController.inTransitionOrBusy = false;

        // Score breakdown animation (can be skipped)
        int curBreakdownIndex = 0;

        foreach(KeyValuePair<string, int> pair in breakdown) {
            scoreScreenBreakdown.text = pair.Value + " - " + pair.Key;
            animTime = 0f;

            const float TRANSLATE_DIST = 8f;
            Vector3 pos = scoreScreenBreakdown.cachedTrans.localPosition;
            pos.y = -TRANSLATE_DIST;

            // Fade in, translate bottom -> middle.
            while(animTime < 1f) {
                animTime = Mathf.Min(animTime + (Time.unscaledDeltaTime * 4f), 1f);
                pos.y = (1f - animTime) * -TRANSLATE_DIST;
                scoreScreenBreakdown.cachedTrans.localPosition = pos;
                scoreScreenBreakdown.alpha = animTime;
                yield return 0f;
            }

            // Wait 0.5 seconds.
            pos.y = 0f;
            scoreScreenBreakdown.cachedTrans.localPosition = pos;
            scoreScreenBreakdown.alpha = 1f;
            animTime = 1f;
            yield return Timing.WaitForSeconds(0.5f);

            // Fade out, translate middle -> top
            while(animTime < 2f) {
                animTime = Mathf.Min(animTime + (Time.unscaledDeltaTime * 4f), 2f);
                pos.y = (animTime - 1f) * TRANSLATE_DIST;
                scoreScreenBreakdown.cachedTrans.localPosition = pos;
                scoreScreenBreakdown.alpha = 2f - animTime;
                yield return 0f;
            }

            // Animate total frog points.
            displayedFrogPoints += pair.Value;
            scoreScreenFrogPoints.text = displayedFrogPoints.ToString();

            // Delay for next score breakdown if there is another one after this.
            if(curBreakdownIndex < breakdown.Count - 1)
                yield return Timing.WaitForSeconds(0.25f);

            curBreakdownIndex++;
        }
    }

    private Dictionary<string, int> GetBreakdownAndAwardPoints(GameSessionStats stats) {
        Dictionary<string, int> result = new Dictionary<string, int>();
        int awardSum = 0;

        // Linear award for bugs eaten.
        if(stats.bugsEaten > 0) {
            result.Add("BUGS EATEN", stats.bugsEaten);
            awardSum += stats.bugsEaten;
        }

        // Bonus points for rare bugs.
        if(stats.firefliesEaten > 0) {
            result.Add("FIREFLIES EATEN", stats.firefliesEaten);
            awardSum += stats.firefliesEaten;
        }

        // Difficulty/survival multiplier.
        if(stats.highestDifficulty >= 3) {
            // Bonus (scales with bugs eaten) starts with 5% at difficulty level 3, and increases by 2% every 2 levels.
            float multiplier = 0.05f + ((stats.highestDifficulty - 3) / 2) * 0.02f;

            int bugBonus = Mathf.CeilToInt(stats.bugsEaten * multiplier);
            int standardBonus = (stats.highestDifficulty - 2);
            int bonus = Mathf.Max(bugBonus, standardBonus);

            result.Add("SURVIVAL BONUS", bonus);
            awardSum += bonus;
        }

        // Leap streak bonuses.
        if(stats.highestLeapStreak >= 30) {
            result.Add("FROGTASTIC STREAK", 45);
            awardSum += 45;
        }
        else if(stats.highestLeapStreak >= 20) {
            result.Add("INSANE STREAK", 30);
            awardSum += 30;
        }
        else if(stats.highestLeapStreak >= 15) {
            result.Add("AMAZING STREAK", 20);
            awardSum += 20;
        }
        else if(stats.highestLeapStreak >= 10) {
            result.Add("GREAT STREAK", 10);
            awardSum += 10;
        }

        // Sum up breakdown.
        shop.AwardFrogPoints(awardSum);
        return result;
    }

    public void AdjustForBannerAd(AdController adsCtrl, bool bannerIsActive) {
        if(bannerIsActive) {
            curBannerAdHeight = root.activeHeight * adsCtrl.bannerHeightPercent; // Banner height in NGUI units.
        }
        else {
            curBannerAdHeight = 0f;
        }

        for(int i = 0; i < adOffsets.Length; i++) {
            adOffsets[i].localPosition = new Vector3(0f, -curBannerAdHeight, 0f);
        }
    }

    public void RestartGame_Confirm() {
        if(GameController.inTransitionOrBusy)
            return;

        DialogBoxUI.instance.Display("ATTENTION", "Are you sure you want to start over? Your current game will be lost!", RestartGame, null);
    }

    public void RestartGame() {
        if(GameController.inTransitionOrBusy)
            return;

        Timing.RunCoroutine(RestartGameRoutine());
    }

    private IEnumerator<float> RestartGameRoutine() {
        GameController.inTransitionOrBusy = true;
        inMenu = false;

        float animTime = 1f;

        while(animTime > 0f) {
            if(scoreScreen.activeInHierarchy) {
                animTime -= Time.unscaledDeltaTime * scoreScreenAnimSpeed;
                float x = scoreScreenAnimCurve.Evaluate(animTime);
                scoreScreenTransform.localPosition = defaultScoreScreenPos + new Vector3(x, 0f, 0f);
            }
            else if(pauseScreen.activeInHierarchy) {
                animTime -= Time.unscaledDeltaTime * pauseScreenAnimSpeed;
                float x = pauseScreenAnimCurve.Evaluate(animTime);
                pauseScreenTransform.localPosition = defaultPauseScreenPos + new Vector3(x, 0f, 0f);
            }

            yield return 0f;
        }

        game.ResetVariablesAndGameState();
        GameController.inTransitionOrBusy = false;
    }

    public void BackToPauseMenu() {
        SelectPauseMenuWindow(PauseMenuWindow.Main);
    }

    public void PauseMenu_Settings() {
        SelectPauseMenuWindow(PauseMenuWindow.Settings);
    }

    private void SelectPauseMenuWindow(PauseMenuWindow window) {
        int windowIndex = (int)window;

        for(int i = 0; i < pauseMenuRoots.Length; i++) {
            pauseMenuRoots[i].SetActive(i == windowIndex);
        }
    }

    private void ToggleGameHUD(bool on) {
        topLeftHud.SetActive(on);
        topRightHud.SetActive(on);
    }

    public void ToggleGameUI(bool on) {
        hudPanelGo.SetActive(on);
    }
    
    public void ResetGameHUDState() {
        cancelArea.SetActive(false);
        scoreLabel.text = "Score: 0";
        addScoreLabel.enabled = false;
        perfStreakLabel.enabled = false;
        inMenu = false;

        if(scoreScreenRoutine.IsValid) {
            // Interrupt score screen animations if they are active.
            Timing.KillCoroutines(scoreScreenRoutine);
        }

        scoreScreen.SetActive(false);
        pauseScreen.SetActive(false);

        ToggleGameHUD(true);
        scoreRewardTime = -100f;
    }
}