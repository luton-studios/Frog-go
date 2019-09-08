using UnityEngine;

public class InsectEntity : MonoBehaviour {
    private const float DISAPPEAR_TIME = 3f;
    private const float FLYING_Y = 0.8f;
    private const float MAX_LEAP_STREAK_CHANCE_MULTIPLIER = 2f; // x2 chance maximum. (e.g. firefly 2.1% can only go up to 4.2% at 100 leap streak)

    public TileEntity parentTile;
    public Transform cachedTrans;
    public InsectSettings[] insectSettings;
    public InsectSettings[] specialInsects;
    public float fireflyChance = 0.02f;
    
    public InsectSettings curInsect { get; private set; }

    private int frameCount;
    private float frameIndex;
    private float frameSizeX;
    private float frameSizeY;
    private bool disappearing;
    private float disappearStartTime;
    private float defaultY;

    private void Awake() {
        defaultY = cachedTrans.localPosition.y;
        curInsect = null;
    }

    public void Initialize() {
        gameObject.SetActive(true);
        int tileIndex = (int)parentTile.tileType;

        // Reset stuff
        disappearing = false;
        cachedTrans.localPosition = new Vector3(cachedTrans.localPosition.x, defaultY, cachedTrans.localPosition.z);
        cachedTrans.localRotation = Quaternion.identity;

        InsectSettings.SpecialType specialType = GetRandomSpecialInsectType();

        if(specialType == InsectSettings.SpecialType.None) {
            SetNormalInsect(tileIndex);
        }
        else {
            SetSpecialInsect(specialType);
        }

        frameIndex = Random.Range(0f, frameCount);
        DisplayAnimationFrame(frameIndex);

        // Randomize insect rotation.
        int rotateAmount = Random.Range(0, 4);

        if(rotateAmount > 0)
            cachedTrans.Rotate(0f, rotateAmount * 90f, 0f, Space.World);
    }

    private void Update() {
        if(frameCount > 1) {
            frameIndex += Time.deltaTime * curInsect.animCycleRate * frameCount;
            DisplayAnimationFrame(frameIndex);

            if(frameIndex >= frameCount)
                frameIndex %= frameCount;
        }

        if(disappearing) {
            if(Time.time - disappearStartTime <= DISAPPEAR_TIME) {
                if(curInsect.fly) {
                    cachedTrans.Translate(0f, 0f, 5f * Time.deltaTime);
                    cachedTrans.Rotate(0f, Random.Range(-150f, 150f) * Time.deltaTime, 0f);
                }
                else {
                    cachedTrans.Translate(0f, 0f, 3f * Time.deltaTime);
                    cachedTrans.Rotate(0f, Random.Range(-30f, 30f) * Time.deltaTime, 0f);

                    float distX = Mathf.Abs(cachedTrans.position.x - parentTile.cachedTrans.position.x);
                    float distZ = Mathf.Abs(cachedTrans.position.z - parentTile.cachedTrans.position.z);
                    bool onBox = (distX < parentTile.hitboxExtent.x - curInsect.radius && distZ < parentTile.hitboxExtent.z - curInsect.radius);

                    if(!onBox) {
                        // Disable after going out of hitbox.
                        FinishDisappearing();
                    }
                }
            }
            else {
                // Auto-disable after time.
                FinishDisappearing();
            }
        }
    }

    private InsectSettings.SpecialType GetRandomSpecialInsectType() {
        float chance = Random.value;
        float leapStreakChanceBonus = Mathf.Min(1f + (GameController.instance.curLeapStreak * 0.01f), MAX_LEAP_STREAK_CHANCE_MULTIPLIER); // Maximum of x2 multiplier. Each leap streak increases the original chance by 1%.

        if(chance < fireflyChance * leapStreakChanceBonus)
            return InsectSettings.SpecialType.Firefly;

        return InsectSettings.SpecialType.None;
    }

    private void SetNormalInsect(int index) {
        // Disable all special insects.
        for(int i = 0; i < specialInsects.Length; i++) {
            UpdateInsectRenderer(specialInsects[i], false);
        }

        // Enable tile-specific insect.
        for(int i = 0; i < insectSettings.Length; i++) {
            UpdateInsectRenderer(insectSettings[i], i == index);
        }
    }

    private void SetSpecialInsect(InsectSettings.SpecialType type) {
        // Disable all regular insects.
        for(int i = 0; i < insectSettings.Length; i++) {
            UpdateInsectRenderer(insectSettings[i], false);
        }

        // Enable special insect.
        int index = (int)type - 1;

        for(int i = 0; i < specialInsects.Length; i++) {
            UpdateInsectRenderer(specialInsects[i], i == index);
        }
    }

    private void UpdateInsectRenderer(InsectSettings insect, bool visible) {
        if(visible) {
            curInsect = insect;
            frameCount = insect.frameCountX * insect.frameCountY;
            frameSizeX = 1f / insect.frameCountX;
            frameSizeY = 1f / insect.frameCountY;
            frameIndex = Random.Range(0f, frameCount);

            insect.renderer.enabled = true;
            insect.renderer.material.mainTextureScale = new Vector2(frameSizeX, frameSizeY);
        }
        else {
            if(curInsect == insect)
                curInsect = null;

            insect.renderer.enabled = false;
        }
    }

    private void DisplayAnimationFrame(float frame) {
        int i = (int)frame;
        int x = i % curInsect.frameCountX;
        int y = i / curInsect.frameCountX;

        curInsect.renderer.material.mainTextureOffset = new Vector2(x * frameSizeX, y * frameSizeY);
    }

    public void Disappear(Vector3 fromPos, bool eaten) {
        if(!eaten) {
            disappearing = true;

            if(curInsect.fly) {
                cachedTrans.localPosition = new Vector3(cachedTrans.localPosition.x, FLYING_Y, cachedTrans.localPosition.z);

                Vector3 posDifference = cachedTrans.position - GameController.instance.frog.position;
                posDifference.y = 0f;
                cachedTrans.localRotation = Quaternion.LookRotation(posDifference, Vector3.up);
            }
            else {

            }

            disappearStartTime = Time.time;
        }
        else {
            gameObject.SetActive(false);
        }
    }

    private void FinishDisappearing() {
        disappearing = false;
        cachedTrans.localPosition = new Vector3(cachedTrans.localPosition.x, defaultY, cachedTrans.localPosition.z);
        gameObject.SetActive(false);
    }
}

[System.Serializable]
public class InsectSettings {
    public enum SpecialType { None = 0, Firefly = 1 };

    public SpecialType specialType = SpecialType.None;
    public Renderer renderer;
    public int frameCountX = 1;
    public int frameCountY = 1;
    public float animCycleRate = 1f;
    public float radius = 0.1f;
    public bool fly = false;
}