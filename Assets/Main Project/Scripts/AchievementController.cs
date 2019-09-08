using UnityEngine;
using System.Collections.Generic;

public enum AchievementID { SpicyFood = 0, BreathingFire, Hopper, LeapExpert, BugBuffet, MovieNight, Stacked, Capitalism, MakeItRain,
    GiantLeap, ComboBreaker, LivingOnTheEdge, QuickFeet, TooLateToTurnBack, Efficient, GotPlacesToGo, SupportUs, SixthSense, Asleep,
    IPreferTheBeach, BornToLeap, AreWeThereYet = 21 };

public class AchievementController : MonoBehaviour {
    public static AchievementController instance { get; private set; }

    public GameUI gameUi;
    public ShopController shop;
    public Achievement[] allAchievements;
    public GameObject notifyAchvGo;
    public Transform notifyAchvTrans;
    public UILabel notifyAchvNameLabel;
    public AnimationCurve notifyAnimCurve = new AnimationCurve();
    public float notifyAnimDuration = 3f;
    public float notifyAnimStartY = 420f;
    public float notifyAnimShiftAmount = 70f;

    private Queue<string> unlockedAchvNames;
    private bool didInit;
    private bool displayingNotification;
    private float notifyAnimTimer;

    private void Awake() {
        instance = this;

        didInit = false;
        unlockedAchvNames = new Queue<string>();
        displayingNotification = false;
        notifyAchvGo.SetActive(false);
        notifyAnimTimer = 0f;

        UpdateProgressAll(true);
        didInit = true;
    }

    private void Update() {
        if(displayingNotification) {
            notifyAnimTimer += Time.unscaledDeltaTime / notifyAnimDuration;

            Vector3 pos = notifyAchvTrans.localPosition;
            pos.y = notifyAnimStartY + (notifyAnimCurve.Evaluate(notifyAnimTimer) * (notifyAnimShiftAmount + gameUi.curBannerAdHeight));
            notifyAchvTrans.localPosition = pos;

            if(notifyAnimTimer > 1f) {
                displayingNotification = false;
                notifyAchvGo.SetActive(false);
            }
        }
        else {
            if(unlockedAchvNames.Count > 0) {
                notifyAchvNameLabel.text = unlockedAchvNames.Dequeue();
                displayingNotification = true;
                notifyAchvGo.SetActive(true);
                notifyAnimTimer = 0f;
            }
        }
    }

    public Achievement GetAchievementData(AchievementID achv) {
        for(int i = 0; i < allAchievements.Length; i++) {
            if(allAchievements[i].id == achv)
                return allAchievements[i];
        }

        return null;
    }

    public void AwardEventAchievement(AchievementID achv, bool awardShop) {
        Achievement data = GetAchievementData(achv);

        if(!data.isEventAchievement) {
            Debug.LogError("Tried to award an achievement that's not an event! " + achv);
            return; // Not an event achievement (either you got it or not).
        }

        PlayerPrefs.SetInt(data.playerPrefsKey, 0); // The key exists, you got the achievement.
        UpdateProgressAll(awardShop);
    }

    public void UpdateProgressAll(bool awardShop) {
        for(int i = 0; i < allAchievements.Length; i++) {
            allAchievements[i].UpdateCurrentValue();

            if(!didInit) {
                // Ignore notifications from previous sessions.
                allAchievements[i].notifiedThisSession = allAchievements[i].achieved;
            }
            else if(allAchievements[i].achieved && !allAchievements[i].notifiedThisSession) {
                // We did not notify user that they have gotten this achievement yet.
                unlockedAchvNames.Enqueue(allAchievements[i].displayName);
                allAchievements[i].notifiedThisSession = true;
            }
        }

        if(awardShop) {
            shop.AwardAchievementSkins();
        }
    }
}

[System.Serializable]
public class Achievement {
    public string displayName;
    public AchievementID id;
    public string description;
    public string playerPrefsKey; // to load current value.
    public bool isMysteryAchievement;
    public bool isEventAchievement;
    public int targetValue;

    public bool notifiedThisSession { get; set; }
    public int currentValue { get; private set; }

    public bool achieved {
        get {
            if(isEventAchievement) {
                return PlayerPrefs.HasKey(playerPrefsKey);
            }

            return (currentValue == targetValue);
        }
    }

    public float progress {
        get {
            if(isEventAchievement) {
                return (achieved) ? 1f : 0f;
            }

            return currentValue / (float)targetValue;
        }
    }

    public void UpdateCurrentValue() {
        currentValue = Mathf.Min(PlayerPrefs.GetInt(playerPrefsKey, 0), targetValue);
    }
}