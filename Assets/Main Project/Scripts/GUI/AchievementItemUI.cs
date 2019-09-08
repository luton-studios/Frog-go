using UnityEngine;

public class AchievementItemUI : MonoBehaviour {
    public Transform cachedTrans;
    public UISprite backgroundSprite;
    public UISprite completedBackgroundSprite;
    public UILabel nameLabel;
    public UILabel descriptionLabel;
    public UISprite statusIcon;
    public UILabel completePercentLabel;
    public string lockedSpriteName;
    public string unlockedSpriteName;
    
    public void Refresh(Achievement data) {
        if(data.achieved || !data.isMysteryAchievement) {
            // Should the achievement be revealed?
            nameLabel.text = data.displayName;
            descriptionLabel.text = data.description;
        } 
        else if(data.isMysteryAchievement) {
            nameLabel.text = "Mystery";
            descriptionLabel.text = "Revealed once unlocked";
        }

        if(data.achieved) {
            completedBackgroundSprite.enabled = true;
            completedBackgroundSprite.width = backgroundSprite.width;
            statusIcon.spriteName = unlockedSpriteName;
            completePercentLabel.text = "DONE";
        }
        else {
            // Update icon to locked.
            statusIcon.spriteName = lockedSpriteName;

            if(data.isEventAchievement) {
                // Event achievement not completed.
                completePercentLabel.text = "0%";
                completedBackgroundSprite.enabled = false;
            }
            else {
                // Percentage until completion.
                completePercentLabel.text = Mathf.FloorToInt(data.progress * 100f) + "%";

                if(data.progress < 0.01f) {
                    // Hide completed background at 0 width (bug with NGUI sprites).
                    completedBackgroundSprite.enabled = false;
                }
                else {
                    // Update completed background width as percentage.
                    completedBackgroundSprite.enabled = true;
                    completedBackgroundSprite.width = Mathf.FloorToInt(backgroundSprite.width * data.progress);
                }
            }
        }
    }
}