using UnityEngine;

public class LeaderboardItemUI : MonoBehaviour {
    public GameObject cachedGo;
    public Transform cachedTrans;
    public UISprite backgroundSprite;
    public UISprite edgeSprite;
    public UILabel rankLabel;
    public UILabel nameLabel;
    public UILabel scoreLabel;
    public UISprite trophyIcon;
    public Color goldColor = Color.yellow;
    public Color silverColor = Color.gray;
    public Color bronzeColor = Color.red;

    public void Refresh(LeaderboardResult item) {
        Refresh(item.rank, item.username, item.score);
    }

    public void Refresh(int rank, string username, long score) {
        rankLabel.text = (rank > 0) ? rank.ToString() : "N/A";
        nameLabel.text = username;
        scoreLabel.text = score.ToString("N0"); // Add commas.

        if(rank >= 1 && rank <= 3) {
            Color c = goldColor; // rank == 1

            if(rank == 2)
                c = silverColor;
            else if(rank == 3)
                c = bronzeColor;

            trophyIcon.enabled = true;
            trophyIcon.color = c;
            edgeSprite.enabled = true;
            edgeSprite.color = c;
        }
        else {
            trophyIcon.enabled = false;
            edgeSprite.enabled = false;
        }
    }
}