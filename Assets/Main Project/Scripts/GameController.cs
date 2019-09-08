using UnityEngine;
using System.Collections.Generic;
using TrinarySoftware;

public enum JumpStatus { Failed, Success, Perfect, NearMiss_Perfect, NearMiss_Success };

public class GameController : MonoBehaviour {
    public const float BOT_SCREEN_CAMBOUNDS = -2.92f; // Relative to camera, object placed at water height. This is the threshold where tiles get recycled to the beginning.
    public const float HORIZONTAL_SCREEN_BUFFER = 0.1f; // The distance past the edges of the screen for horizontally repeating tiles to repeat.
    private const float JUMP_FORCE_MULTIPLIER = 4.2f;
    private const float JUMP_TIME = 0.425f;
    private const float STABILIZE_JUMP_DIST_MIN = 0.15f;
    private const float STABILIZE_JUMP_TIME = 0.2f;
    private const float STABILIZE_JUMP_DELAY = 0.25f;
    private const float NEXT_JUMP_DELAY = 0.2f;
    private const float SCORE_SCREEN_DELAY = 0.6f;
    private const float SCORE_STREAK_GROWTH_RATE = 1.09f;
    private const float SCORE_STREAK_MAX_MULTIPLIER = 12f; // Maximum bonus is x24.
    private const float FROG_SHADOW_SHRINK_AMOUNT = 0.275f;
    private const float FROG_BASE_JUMP_HEIGHT = 0.51f;
    private const float FROG_JUMP_HEIGHT_MULTIPLIER = 0.65f;
    private const float COAST_HIDE_THRESHOLD = 1.32f; // Hides shore particles once bottom camera world bounds is past this.
    
    public static int apiLevel = 24; // By default, emulate Android 7.0 in editor

    private static readonly int _ShadowStrength = Shader.PropertyToID("_ShadowStrength");

    public static GameController instance { get; private set; }
    public static bool inTransitionOrBusy;
    
    [Header("REFERENCES")]
    public MainMenuUI mainMenu;
    public GameUI gameUI;
    public PlayServicesController gpgs;
    public TileController tileController;
    public Transform frog;
    public Transform frogMeshTrans;
    public WorldReferences gameSceneryRefs;
    public WorldReferences mainMenuSceneryRefs;
    public GameObject coastGo;
    public Light directionalLight;
    public ParticleSystem frogShockwave;
    public Transform frogShadow;
    public Renderer frogShadowRenderer;
    public Transform water;
    public Camera cam;
    public Transform camTrans;
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioClip jumpSound;
    public AudioClip landSound;
    public AudioClip perfLandSound;
    public AudioClip buildUpSound;
    public AudioClip cancelJumpSound;

    [Header("SETTINGS")]
    public int increaseDifficultyInterval = 6;
    public float maxJumpChargeTime = 2f;
    public float frogRadius = 1f;
    public float waterHeight = 0f;
    public Vector4 cancelScreenArea;
    public Vector3 frogShadowOffset;
    public CoastlineHitbox[] coastlineHitboxes;
    public float coastlineHeight = 1f;

    [Header("EFFECTS")]
    public AnimationCurve jumpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float jumpPeakTime = 0.5f;
    public AnimationCurve frogJumpTiltAnim = AnimationCurve.Linear(0f, 0f, 1f, 30f);

    public float leftBounds {
        get {
            return camTrans.position.x - (cam.orthographicSize * cam.aspect);
        }
    }

    public float rightBounds {
        get {
            return camTrans.position.x + (cam.orthographicSize * cam.aspect);
        }
    }

    public float boundsWidth {
        get {
            return cam.orthographicSize * 2f * cam.aspect;
        }
    }

    private bool isPressingOnGameArea {
        get {
            float relPosY = lastTouchPos.y / Screen.height;
            float threshold = 0.86f - AdController.instance.bannerHeightPercent;

            if(relPosY > threshold)
                return false; // Pause button and banner ad.

            return true;
        }
    }

    public int curLeapStreak { get; private set; }

    private TileEntity attachedTile;
    private GameSessionStats curGameStats;
    private MapThemeData curMapTheme;
    private Plane raycastPlane;
    private Vector3 startingCamPos;
    private Vector3 defaultFrogTilt;
    private Vector3 initFrogPos;
    private Vector3 targetCamPos;
    private Vector3 frogStartPos;
    private Vector3 frogEndPos;
    private Vector2 lastTouchPos;
    private Vector3 frogShadowScale;
    private bool canJump;
    private bool chargingJump;
    private bool touching;
    private bool leftTheBeach;
    private bool exitingGameSession;
    private float clickStartTime;
    private float curJumpDist;
    private int curNearMissStreak;
    private int lastTileDepth;
    private int lastTouchCount;
    private float curScoreMultiplier;
    private float idleTimer;
    private bool asleepAchv;
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;
    private AndroidJavaClass vibrationEffect;
    private bool initVibrator;
    private bool vibratorUseAmplitude;
#endif

    private void Awake() {
        instance = this;
        CheatsForDebugging();

#if UNITY_ANDROID && !UNITY_EDITOR
        if(Application.isEditor)
            Handheld.Vibrate(); // Cheat to add vibration permission.
        
        // Override default editor API level on actual Android device.
        AndroidJavaClass androidVersion = new AndroidJavaClass("android.os.Build$VERSION");
        apiLevel = androidVersion.GetStatic<int>("SDK_INT");
#endif
        
        raycastPlane = new Plane(Vector3.up, waterHeight); // Raycastable plane at water level.
        startingCamPos = camTrans.position; // Cannot go behind starting cam Z.
        initFrogPos = frog.position;
        targetCamPos = startingCamPos;
        frogShadowScale = frogShadow.localScale;
        defaultFrogTilt = frogMeshTrans.localEulerAngles;
        
        inTransitionOrBusy = false;
        musicSource.ignoreListenerPause = true; // Continue playing in pause menu.

        asleepAchv = false;
        tileController.Initialize();
        curGameStats = new GameSessionStats();
        ResetVariablesAndGameState();
    }
    
    public void ResetVariablesAndGameState() {
        // Stop running coroutines.
        Timing.KillCoroutines("game");

        // Reset score and stats.
        curGameStats.Reset();
        curLeapStreak = 0;
        curNearMissStreak = 0;
        curScoreMultiplier = 1f;
        lastTileDepth = -1; // -1 = coast.

        // Reset achievements stuff.
        leftTheBeach = false;
        idleTimer = 0f;

        // Reset visuals/renderers.
        coastGo.SetActive(true);
        gameSceneryRefs.frogSkinner.cachedGo.SetActive(true);
        frogShadowRenderer.enabled = true;
        frogShadow.localScale = frogShadowScale;
        frogShadowRenderer.material.SetFloat(_ShadowStrength, 1f);
        
        // Reset music and SFX.
        AudioListener.pause = false;
        sfxSource.Stop();

        if(!musicSource.isPlaying)
            musicSource.Play();

        gameUI.ResetGameHUDState();
       
        // Reset other variables.
        Time.timeScale = 1f;
        exitingGameSession = false;
        inTransitionOrBusy = false;
        canJump = true;
        chargingJump = false;
        touching = false;
        lastTouchCount = 0;
        curJumpDist = 0f;

        if(attachedTile != null) {
            attachedTile.OnFrogDetach(frog, false);
            attachedTile = null;
        }
        
        // Reset camera and frog transform.
        targetCamPos = startingCamPos;
        frogStartPos = initFrogPos;
        frogEndPos = initFrogPos;
        frog.SetPositionAndRotation(initFrogPos, Quaternion.identity);
        frogShadow.position = initFrogPos + frogShadowOffset;
        ResetFrogAnimationState();

        camTrans.position = targetCamPos;
        UpdateWaterPosition();

        // Clear tiles and reset tile controller with starting platform.
        tileController.Reset(initFrogPos);
    }

    private void LateUpdate() {
        if(exitingGameSession)
            return; // Don't process game logic while returning to main menu.
        
        if(!inTransitionOrBusy) {
            // Update mouse/touch position.
            if(Application.isEditor)
                lastTouchPos = Input.mousePosition;
            else if(Input.touchCount > 0)
                lastTouchPos = Input.touches[0].position;

            if(!gameUI.inMenu && canJump) {
                // Handle asleep hidden achievement.
                if(!asleepAchv) {
                    idleTimer += Time.deltaTime;

                    if(idleTimer >= 15f) {
                        AchievementController.instance.AwardEventAchievement(AchievementID.Asleep, false);
                        asleepAchv = true;
                    }
                }

                bool tapped = (lastTouchCount == 0 && Input.touchCount > 0) || Input.GetMouseButtonDown(0);

                // Just started tapping on a valid area this frame, begin charging jump.
                if(tapped && !touching && isPressingOnGameArea) {
                    touching = true;
                    chargingJump = true;
                    gameUI.cancelArea.SetActive(true);
                    clickStartTime = Time.time;

                    sfxSource.pitch = 1f;
                    sfxSource.PlayOneShot(buildUpSound, 0.25f);
                    idleTimer = 0f;
                }
            }
        }

        if(chargingJump) {
            idleTimer = 0f;
            float relPosX = lastTouchPos.x / Screen.width;
            float relPosY = lastTouchPos.y / Screen.height;

            bool isCancelling = (relPosX > cancelScreenArea.x && relPosX < cancelScreenArea.y && relPosY > cancelScreenArea.z && relPosY < cancelScreenArea.w);
            bool releasedFinger = ((Input.touchCount == 0 && lastTouchCount > 0) || Input.GetMouseButtonUp(0));
            bool cancelledJump = false;

            if(isCancelling) {
                gameUI.cancelAreaBG.color = gameUI.cancelHighlightColor;

                if(releasedFinger) {
                    CancelJump();
                    cancelledJump = true;
                }
            }
            else {
                gameUI.cancelAreaBG.color = gameUI.cancelAreaBG.defaultColor;
            }

            if(touching && !cancelledJump) {
                float timeHeld = Time.time - clickStartTime;

                if(timeHeld >= maxJumpChargeTime) {
                    CancelJump();
                    cancelledJump = true;
                }
                else {
                    // Raycast against water level, and get the distance to water surface under mouse cursor.
                    float dist;
                    Ray rayToCursor = cam.ScreenPointToRay(lastTouchPos);
                    raycastPlane.Raycast(rayToCursor, out dist);
                    
                    Vector3 targetPt = rayToCursor.origin + (rayToCursor.direction * dist);

                    // Released touch or mouse.
                    if(releasedFinger) {
                        touching = false;
                        sfxSource.Stop(); // Stop charging jump sound.

                        // Reset frog animation.
                        ResetFrogAnimationState();

                        Jump(timeHeld, targetPt);
                    }
                    else {
                        // Rotate frog
                        Vector3 dir = targetPt - frog.position;
                        UpdateFrogRotationY(dir.x, dir.z);

                        float chargeProgress = (timeHeld / maxJumpChargeTime);

                        // Tilt frog (as charging)
                        Vector3 finalMeshRot = defaultFrogTilt;
                        finalMeshRot.x += chargeProgress * 20f;
                        frogMeshTrans.localRotation = Quaternion.Euler(finalMeshRot);

                        // Squish frog
                        float yScale = Mathf.Max(0.5f, 1f - (chargeProgress * 0.5f));
                        frog.localScale = new Vector3(1f, yScale, 1f);
                    }
                }
            }
        }

        if(attachedTile != null) {
            // Camera and frog follow attached tile.
            frog.Translate(attachedTile.moveDelta, Space.World);
            targetCamPos.x += attachedTile.moveDelta.x;
        }
        
        camTrans.position = Vector3.Lerp(camTrans.position, targetCamPos, Time.deltaTime * 8f);
        UpdateWaterPosition();

        bool showCoast = (camTrans.position.z + BOT_SCREEN_CAMBOUNDS < COAST_HIDE_THRESHOLD);
        coastGo.SetActive(showCoast);

        bool doPooling = true; // optimize if necessary (stop checking when camera stops moving.

        if(doPooling)
            PerformPoolLoop();

        lastTouchCount = Input.touchCount;
    }
    
    public void ToggleGameState(bool render) {
        // Toggles rendering and logic when game isn't visible (e.g. in main menu) for performance reasons.
        if(render) {
            enabled = true;
            cam.enabled = true;
            gameUI.ToggleGameUI(true);
            ResetVariablesAndGameState();
        }
        else {
            enabled = false;
            cam.enabled = false;
            gameUI.ToggleGameUI(false);
        }
    }

    private void Jump(float timeHeld, Vector3 targetPoint) {
        chargingJump = false;
        gameUI.cancelArea.SetActive(false);

        if(!canJump || gameUI.inMenu || timeHeld <= Mathf.Epsilon)
            return;

        // Detach from current tile.
        if(attachedTile != null) {
            attachedTile.OnFrogDetach(frog, true);
            attachedTile = null;
        }

        // Normalize direction of frog to mouse position.
        Vector3 dir = (targetPoint - frog.position);
        dir.y = 0f;
        dir.Normalize();

        // Jump distance multiplier.
        curJumpDist = timeHeld * JUMP_FORCE_MULTIPLIER;
        Vector3 moveVector = dir * curJumpDist;

        // Set jump endpoints.
        frogStartPos = frog.position;
        frogEndPos = frogStartPos + moveVector;

        sfxSource.pitch = Random.Range(0.95f, 1.05f);
        sfxSource.PlayOneShot(jumpSound, 0.25f);

        // Rotate frog towards jump direction.
        UpdateFrogRotationY(moveVector.x, moveVector.z);

        // Start moving frog and stuff.
        Timing.RunCoroutine(MoveObjects(), "game");
    }

    private void CancelJump() {
        chargingJump = false;
        touching = false;
        
        ResetFrogAnimationState();
        gameUI.cancelArea.SetActive(false);

        // Stop charging jump sound.
        sfxSource.Stop();

        // Play cancel sound.
        sfxSource.pitch = Random.Range(0.95f, 1.05f);
        sfxSource.PlayOneShot(cancelJumpSound, 0.5f);
    }

    private IEnumerator<float> MoveObjects() {
        canJump = false;
        float jumpProgress = 0f;

        // Frog jump animation.
        while(jumpProgress < 1f) {
            if(ProcessFrogJump(jumpProgress))
                break; // Jump ended early; frogEndPos modified.

            jumpProgress += Time.deltaTime / JUMP_TIME;
            yield return 0f;
        }
        
        // Snap to jump end pos.
        frog.position = frogEndPos;
        frogMeshTrans.localRotation = Quaternion.Euler(defaultFrogTilt);
        frogShadow.localPosition = frogShadowOffset;

        // Move the camera based on frog movement.
        bool landedOnCoast = WithinCoastline(frog.position);

        if(landedOnCoast) {
            if(leftTheBeach) {
                AchievementController.instance.AwardEventAchievement(AchievementID.IPreferTheBeach, false);
            }

            if(frogEndPos.y < coastlineHeight) {
                frogEndPos.y = coastlineHeight;
                frog.position = frogEndPos;
            }

            Timing.RunCoroutine(LoseRoutine(false));
        }
        else {
            // Move camera.
            Vector3 camMove = frogEndPos - frogStartPos;
            targetCamPos.x += camMove.x;
            targetCamPos.z += camMove.z;
            
            // Check if frog landed on pad.
            JumpStatus status = JumpStatus.Failed;
            int landedPosIndex = -1;
            attachedTile = GetJumpResult(out status, out landedPosIndex);

            if(attachedTile != null) {
                // Yay, the frog made it. Attach frog to this tile.
                attachedTile.OnFrogAttached(frog);

                // Achievement logic.
                leftTheBeach = true;

                if(landedPosIndex > 0) {
                    // Landed on an offscreen tile.
                    AchievementController.instance.AwardEventAchievement(AchievementID.SixthSense, false);
                }

                // Calculate score and play other effects.
                CalculateNewScore(attachedTile, status);

                // All insects before or equal to this tile should disappear regardless if the player hit it or not.
                // Also, going backwards or landing on the same tile will not award the player extra points.
                int targetTileDepth = attachedTile.mapDepth;

                foreach(TileEntity tile in tileController.activeTiles) {
                    if(tile.mapDepth <= targetTileDepth) {
                        tile.didAwardScore = true;

                        bool eaten = (tile == attachedTile && (status == JumpStatus.Perfect || status == JumpStatus.NearMiss_Perfect));
                        tile.ClearInsect(frog.position, eaten);
                    }
                }

                // If the nearly missed the tile, make the frog hop back on the "safe area"
                if(status == JumpStatus.NearMiss_Perfect || status == JumpStatus.NearMiss_Success) {
                    yield return Timing.WaitForSeconds(STABILIZE_JUMP_DELAY);

                    // Get start and end positions for stabilize jump.
                    frogStartPos = frog.position;
                    frogEndPos = attachedTile.GetStablePosition(frogStartPos, frogRadius, STABILIZE_JUMP_DIST_MIN, 0);
                    curJumpDist = Mathf.Max(STABILIZE_JUMP_DIST_MIN, (frogEndPos - frog.position).magnitude);

                    Vector3 stableDir = (frogEndPos - frog.position);
                    stableDir.y = 0f;
                    targetCamPos += stableDir;
                    jumpProgress = 0f;

                    // Rotate frog towards jump direction.
                    UpdateFrogRotationY(stableDir.x, stableDir.z);

                    // Frog jump animation.
                    while(jumpProgress < 1f) {
                        if(ProcessFrogJump(jumpProgress))
                            break;

                        jumpProgress += Time.deltaTime / STABILIZE_JUMP_TIME;
                        yield return 0f;
                    }

                    // Snap to jump end pos.
                    frogEndPos.y = attachedTile.topSurfaceY; // Force the frog to sit on the tile's top surface.
                    frog.position = frogEndPos;
                    frogMeshTrans.localRotation = Quaternion.Euler(defaultFrogTilt);
                    frogShadow.localPosition = frogShadowOffset;

                    attachedTile.PlayBobAnimation();

                    // Play small land sound.
                    sfxSource.pitch = Random.Range(1.35f, 1.45f);
                    sfxSource.PlayOneShot(landSound, 0.35f);
                }

                // Wait briefly before returning control to player.
                yield return Timing.WaitForSeconds(NEXT_JUMP_DELAY);

                // Only start sinking when player has control.
                attachedTile.StartSinking();
                canJump = true;
            }
            else {
                Timing.RunCoroutine(LoseRoutine(true));
            }
        }
    }

    public void TriggerLoss() {
        if(exitingGameSession)
            return;

        Timing.RunCoroutine(LoseRoutine(true));
    }

    private IEnumerator<float> LoseRoutine(bool drowned) {
        if(exitingGameSession)
            yield break;

        // Stop music.
        musicSource.Stop();

        // Stop jumping.
        if(chargingJump)
            CancelJump();

        if(drowned) {
            // Play drown sound.
            sfxSource.pitch = Random.Range(0.95f, 1.05f);
            sfxSource.PlayOneShot(curMapTheme.frogDrownSound, 0.5f);

            yield return Timing.WaitForSeconds(NEXT_JUMP_DELAY);

            // Froggo splash and drown.
            Timing.RunCoroutine(FrogDrown(), "game");
            Instantiate(curMapTheme.frogDrownParticles, frog.position, Quaternion.identity);
        }
        else {
            // Play land sound.
            sfxSource.pitch = Random.Range(0.95f, 1.05f);
            sfxSource.PlayOneShot(landSound, 0.75f);
        }

        // Some stats are only updated at the end of the game.
        curGameStats.ProcessExtraStats(tileController);

        yield return Timing.WaitForSeconds(SCORE_SCREEN_DELAY);
        gameUI.ShowScoreScreen(curGameStats, drowned);
    }

    // Return true when the jump ended early due tile height.
    private bool ProcessFrogJump(float jumpProgress) {
        Vector3 animatedPos = Vector3.LerpUnclamped(frogStartPos, frogEndPos, jumpProgress);

        float jumpHeight = jumpCurve.Evaluate(jumpProgress) * (FROG_BASE_JUMP_HEIGHT + (curJumpDist * FROG_JUMP_HEIGHT_MULTIPLIER));
        animatedPos.y += jumpHeight; // Vertical movement.
        frog.position = animatedPos;

        // Apply blob shadow to surface below.
        Vector3 shadowPos = animatedPos;
        shadowPos.y -= jumpHeight;

        if(attachedTile != null && shadowPos.y < attachedTile.topSurfaceY)
            shadowPos.y = attachedTile.topSurfaceY;

        frogShadow.position = shadowPos + frogShadowOffset;
        idleTimer = 0f;

        // Animate frog tilting.
        AnimateFrogTilt(jumpProgress);

        // Shrink and fade out shadow as it gets further from the surface.
        UpdateFrogShadow(jumpHeight);

        // Detect if we landed on any tiles early (after peak of the jump / accelerating down).
        if(jumpProgress > jumpPeakTime) {
            int landedPosIndex;

            for(int i = 0; i < tileController.activeTiles.Count; i++) {
                TileEntity thisTile = tileController.activeTiles[i];
                float thisTopY = thisTile.topSurfaceY;

                // We passed through top surface during descent and within hitbox.
                if(animatedPos.y < thisTopY && thisTile.CanLand(animatedPos, frogRadius, out landedPosIndex)) {
                    // Update animation end pos to top surface of this tile.
                    animatedPos.y = thisTopY;
                    frogEndPos = animatedPos;
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator<float> FrogDrown() {
        frogShadowRenderer.enabled = false;
        Vector3 startPos = frog.position;
        float time = 0f;

        while(time < 1f) {
            frog.position = startPos - new Vector3(0f, time * 3f, 0f);
            time += Time.deltaTime * 3f;
            yield return 0f;
        }

        gameSceneryRefs.frogSkinner.cachedGo.SetActive(false);
    }

    private TileEntity GetJumpResult(out JumpStatus jStatus, out int landedPosIndex) {
        foreach(TileEntity tile in tileController.activeTiles) {
            jStatus = tile.GetJumpStatus(frog.position, frogRadius, out landedPosIndex);

            if(jStatus != JumpStatus.Failed)
                return tile; // Yes, we hit this tile.
        }

        jStatus = JumpStatus.Failed;
        landedPosIndex = -1;
        return null;
    }
    
    private void CalculateNewScore(TileEntity targetTile, JumpStatus resultFromJump) {
        if(resultFromJump == JumpStatus.Failed)
            return; // This shouldn't happen. If it does happen, question the universe or the programmer(s).

        // Play land sound.
        sfxSource.pitch = Random.Range(0.95f, 1.05f);
        sfxSource.PlayOneShot(landSound, 0.75f);

        if(targetTile.didAwardScore)
            return; // We already landed on this tile, so don't award any extra points.
        
        // Base score depends on the difficulty of the tile that we landed on.
        int toReward = targetTile.baseScoreReward;
        bool eatenInsect = (resultFromJump == JumpStatus.Perfect || resultFromJump == JumpStatus.NearMiss_Perfect);

        // Double points + streak multiplier.
        if(eatenInsect) {
            curGameStats.bugsEaten++;

            if(targetTile.insect.curInsect.specialType == InsectSettings.SpecialType.Firefly) {
                curGameStats.firefliesEaten++;
            }

            curLeapStreak++;
            curScoreMultiplier = Mathf.Min(curScoreMultiplier * SCORE_STREAK_GROWTH_RATE, SCORE_STREAK_MAX_MULTIPLIER);

            // Set the streak as highest if it surpassed the current record.
            if(curLeapStreak > curGameStats.highestLeapStreak) {
                curGameStats.highestLeapStreak = curLeapStreak;
                int recordLeapStreak = PlayerPrefs.GetInt("RecordLeapStreak", 0);

                if(curGameStats.highestLeapStreak > recordLeapStreak) {
                    // Achieved new record leap streak!
                    recordLeapStreak = curGameStats.highestLeapStreak;
                    PlayerPrefs.SetInt("RecordLeapStreak", recordLeapStreak);

                    // Trigger leap streak achievements.
                    AchievementController.instance.UpdateProgressAll(false);
                }
            }

            toReward = Mathf.RoundToInt(toReward * 2f * curScoreMultiplier);

            // Vibrate device very briefly (if enabled in settings).
            Vibrate(5);

            // Play perfect sound (which escalates in pitch as you get a higher streak).
            float pitch = Mathf.Min(1f + ((curLeapStreak - 1) * 0.05f), 2f) * Random.Range(0.99f, 1.01f);
            NGUITools.PlaySound(perfLandSound, 0.1f, pitch);
            
            gameUI.UpdateLeapStreak(curLeapStreak, false);

            // Play awesome shockwave effect.
            PlayShockwave();
        }
        else {
            // Reset streak and multiplier.
            curLeapStreak = 0;
            curScoreMultiplier = 1f;

            gameUI.UpdateLeapStreak(curLeapStreak, true);
        }

        // Handle near miss streak.
        bool nearMiss = (resultFromJump == JumpStatus.NearMiss_Success || resultFromJump == JumpStatus.NearMiss_Perfect);

        if(nearMiss) {
            curNearMissStreak++;

            if(curNearMissStreak > curGameStats.highestNearMissStreak)
                curGameStats.highestNearMissStreak = curNearMissStreak;
        }
        else {
            curNearMissStreak = 0;
        }

        // Leapfrogging. Get extra points and difficulty for skipping lilypads.
        int leapfrog = targetTile.mapDepth - lastTileDepth - 1;

        if(leapfrog > 0) {
            for(int i = 0; i < leapfrog; i++) {
                toReward++;
                toReward *= 2;
            }
            
            int totalLeapfrog = PlayerPrefs.GetInt("Achv_LeapfrogCount", 0);
            totalLeapfrog += leapfrog;
            PlayerPrefs.SetInt("Achv_LeapfrogCount", totalLeapfrog);
            AchievementController.instance.UpdateProgressAll(false);
        }

        lastTileDepth = targetTile.mapDepth;
        curGameStats.finalScore += toReward;

        if(toReward > curGameStats.highestLeapScore) {
            curGameStats.highestLeapScore = toReward;
            int recordLeapScore = PlayerPrefs.GetInt("RecordLeapScore", 0);

            if(curGameStats.highestLeapScore > recordLeapScore) {
                // Achieved new record leap score!
                recordLeapScore = curGameStats.highestLeapScore;
                PlayerPrefs.SetInt("RecordLeapScore", recordLeapScore);

                // Trigger leap score achievements.
                AchievementController.instance.UpdateProgressAll(false);
            }
        }

        gameUI.UpdateScoreLabels(curGameStats.finalScore, toReward, eatenInsect);
        targetTile.didAwardScore = true;
    }

    public void OnPhoneBackButton() {
        if(gameUI.scoreScreen.activeSelf && !inTransitionOrBusy) {
            // We are in score screen. Back button will take you back to main menu.
            BackToMainMenu();
        }
        else {
            // Otherwise, back button triggers pause button.
            gameUI.TogglePauseMenu();
        }
    }

    public void BackToMainMenu_Confirm() {
        if(inTransitionOrBusy)
            return;

        DialogBoxUI.instance.Display("ATTENTION", "Are you sure you want to return to main menu? Your current game will be lost!", BackToMainMenu, null);
    }

    public void BackToMainMenu() {
        if(inTransitionOrBusy)
            return;

        Timing.KillCoroutines();
        sfxSource.Stop();

        mainMenu.ShowMenu();
        exitingGameSession = true;
        inTransitionOrBusy = true;
    }

    private void UpdateWaterPosition() {
        Vector3 waterPos = camTrans.position;
        waterPos.y = waterHeight;
        water.position = waterPos;
    }

    private void ResetFrogAnimationState() {
        frog.localScale = Vector3.one;
        frogMeshTrans.localRotation = Quaternion.Euler(defaultFrogTilt);
    }

    private void AnimateFrogTilt(float progress) {
        Vector3 finalMeshRot = defaultFrogTilt;
        finalMeshRot.x += frogJumpTiltAnim.Evaluate(progress);
        frogMeshTrans.localRotation = Quaternion.Euler(finalMeshRot);
    }

    private void UpdateFrogRotationY(float dirX, float dirZ) {
        float angle = Mathf.Rad2Deg * Mathf.Atan2(dirX, dirZ);
        frog.rotation = Quaternion.Euler(0f, angle, 0f);
        frogShadow.rotation = Quaternion.Euler(90f, angle, 0f);
    }

    private void UpdateFrogShadow(float curJumpHeight) {
        float shadowScale = 1f - (curJumpHeight * FROG_SHADOW_SHRINK_AMOUNT);

        if(shadowScale > 0f) {
            frogShadowRenderer.enabled = true;
            frogShadowRenderer.material.SetFloat(_ShadowStrength, shadowScale);
            frogShadow.localScale = frogShadowScale * shadowScale;
        }
        else {
            frogShadowRenderer.enabled = false;
        }
    }

    private void PlayShockwave() {
        Timing.RunCoroutine(ShockwaveRoutine(), "game");
    }

    private IEnumerator<float> ShockwaveRoutine() {
        ParticleSystem.EmitParams emitParam = new ParticleSystem.EmitParams();
        emitParam.position = frog.position;

        // Play extra shockwaves as streak gets higher.
        int toEmit = Mathf.Min(curLeapStreak, 5);
        int emitCount = 0;
        float timer = 0f;

        while(true) {
            timer += Time.deltaTime;

            // Framerate independent timer.
            while(timer > 0.075f && emitCount < toEmit) {
                frogShockwave.Emit(emitParam, 1);
                timer -= 0.075f;
                emitCount++;
            }

            if(emitCount >= toEmit)
                yield break; // Done.

            yield return 0f;
        }
    }

    private void PerformPoolLoop() {
        float minCamZ = camTrans.position.z + BOT_SCREEN_CAMBOUNDS;

        // Recycle tiles that goes behind camera.
        while(minCamZ > tileController.minTileZ)
            tileController.RecycleOldestTile();

        // Place tiles in front.
        while(tileController.pooledTiles.Count > 0)
            tileController.PlaceNextTile();
    }

    private bool WithinCoastline(Vector3 pos) {
        for(int i = 0; i < coastlineHitboxes.Length; i++) {
            if(pos.x >= coastlineHitboxes[i].minX && pos.x <= coastlineHitboxes[i].maxX && pos.z <= coastlineHitboxes[i].z) {
                return true;
            }
        }

        return false;
    }

    public void UpdateFrogSkin(FrogSkinData skinData) {
        gameSceneryRefs.frogSkinner.UpdateVisuals(skinData);
        mainMenuSceneryRefs.frogSkinner.UpdateVisuals(skinData);
    }

    public void UpdateMapTheme(ShopController.MapTheme map) {
        curMapTheme = map.mapProperties;

        // Global
        directionalLight.color = curMapTheme.lightColor;
        directionalLight.intensity = curMapTheme.lightIntensity;

        // Game
        gameSceneryRefs.ApplyMapTheme(curMapTheme);
        mainMenuSceneryRefs.ApplyMapTheme(curMapTheme);
    }

    private void Vibrate(long milliseconds) {
        if(!GameSettings.instance.vibrationEnabled)
            return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if(!initVibrator) {
            vibrator = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = vibrator.GetStatic<AndroidJavaObject>("currentActivity");
            vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            vibratorUseAmplitude = (apiLevel >= 26 && vibrator.Call<bool>("hasAmplitudeControl")); // API level 26+.
            initVibrator = true;

            if(vibratorUseAmplitude) {
                vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
            }
        }
        
        if(vibratorUseAmplitude) {
            int amplitude = vibrationEffect.GetStatic<int>("DEFAULT_AMPLITUDE");
            AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
            vibrator.Call("vibrate", effect);
        }
        else {
            vibrator.Call("vibrate", milliseconds);
        }
#endif
    }

    private void CheatsForDebugging() {
#if DEBUG_MODE
        // Hardcode PlayerPrefs stuff here. MAKE SURE THIS FUNCTION IS GRAYED OUT FOR RELEASE APK
        PlayerPrefs.SetInt("FrogPoints", 295);
        PlayerPrefs.SetInt("TotalPointsGathered", 11559);
        PlayerPrefs.SetInt("HighScore", 4186);
        PlayerPrefs.SetInt("RecordLeapStreak", 25);
        PlayerPrefs.SetInt("RecordLeapScore", 327);
        PlayerPrefs.SetInt("TotalBugsEaten", 4651);
        PlayerPrefs.SetInt("FirefliesEaten", 102);
        PlayerPrefs.SetInt("RecordNearMissStreak", 11);

        PlayerPrefs.SetInt("Skin_Crimson", 1);
        PlayerPrefs.SetInt("Skin_Inferno", 2);
        PlayerPrefs.SetInt("Map_Lake", 1);
        PlayerPrefs.SetInt("Map_Ocean", 1);
        PlayerPrefs.SetInt("Map_Tropical", 2);
        PlayerPrefs.SetInt("Map_Night Time", 1);
        PlayerPrefs.SetInt("Map_Volcano", 1);
        PlayerPrefs.SetInt("Map_Toxic", 1);
#endif
    }
}

public class GameSessionStats {
    public int finalScore;
    public int highestDifficulty;
    public int highestLeapStreak;
    public int highestLeapScore;
    public int highestNearMissStreak;
    public int bugsEaten;
    public int firefliesEaten;

    public void ProcessExtraStats(TileController tc) {
        highestDifficulty = tc.tileDifficultyLevel;
    }

    public void Reset() {
        finalScore = 0;
        highestDifficulty = 0;
        highestLeapStreak = 0;
        highestLeapScore = 0;
        highestNearMissStreak = 0;
        bugsEaten = 0;
        firefliesEaten = 0;
    }
}

[System.Serializable]
public struct CoastlineHitbox {
    public float minX;
    public float maxX;
    public float z;
}

[System.Serializable]
public class MapThemeData {
    public enum SpecialType { None, Volcano, Toxic };

    public SpecialType specialType = SpecialType.None;
    public Material waterMaterial;
    public Material coastMaterial;
    public Color lightColor;
    public float lightIntensity;
    public AudioClip frogDrownSound;
    public GameObject frogDrownParticles;
}

[System.Serializable]
public class WorldReferences {
    public FrogSkinHandler frogSkinner;
    public Renderer waterRenderer;
    public Renderer coastRenderer;
    public GameObject shoreFoam;
    public GameObject lavaBubbles;

    public void ApplyMapTheme(MapThemeData theme) {
        waterRenderer.material = theme.waterMaterial;
        coastRenderer.material = theme.coastMaterial;

        // Special stuff.
        bool useWaterFoam = true;

        if(theme.specialType == MapThemeData.SpecialType.Volcano) {
            useWaterFoam = false;
        }

        shoreFoam.SetActive(useWaterFoam);
        lavaBubbles.SetActive(!useWaterFoam);

        // TODO: Later spawn some props in both game and main menu scenery based on curMapTheme.specialType
    }
}