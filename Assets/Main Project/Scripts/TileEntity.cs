using UnityEngine;
using System.Collections.Generic;
using TrinarySoftware;

public class TileEntity : MonoBehaviour {
    public enum Type { LilyPad, DriftingLog, FloatingBox };

    public const int TYPE_COUNT = 3; // Equal to Type enum length.
    private const float SINK_SHAKE_SPEED = 18f;
    private const float SINK_SHAKE_INTENSITY = 0.03f;
    
    public GameObject cachedGo;
    public Transform cachedTrans;
    public TileSettings[] tiles;
    public InsectEntity insect;
    public float insectMaxOffsetRatio = 0.9f; // Farthest the insect can be from the center of the tile.
    public AnimationCurve frogLandBobCurve = AnimationCurve.Linear(0f, 0f, 1f, -1f);
    public float bobAnimSpeed = 0.25f;
    public AnimationCurve sinkAnimationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public Type tileType { get; private set; }
    public Vector3 hitboxExtent { get; private set; }
    public Vector3 moveDelta { get; private set; }
    public int baseScoreReward { get; private set; }
    public int mapDepth { get; private set; }
    public float horizontalVelocity { get; private set; }
    public float height { get; private set; }
    public bool frogAttached { get; private set; }
    public bool insectIsActive { get; private set; }
    public bool didAwardScore { get; set; }

    public TileSettings curSettings {
        get {
            return tiles[(int)tileType];
        }
    }
    
    public float radius {
        get {
            return Mathf.Max(hitboxExtent.x, hitboxExtent.z);
        }
    }
    
    public float topSurfaceY {
        get {
            return cachedTrans.position.y + hitboxExtent.y;
        }
    }

    private float horizontalResetDist {
        get {
            return GameController.instance.boundsWidth + ((hitboxExtent.x + GameController.HORIZONTAL_SCREEN_BUFFER) * 2f) - 0.01f;
        }
    }

    public float timeUntilSink {
        get {
            return (sinkLoseTime - sinkTimer) * sinkDuration;
        }
    }

    private CoroutineHandle bobRoutine;
    private Vector3 targetPos;
    private Vector3 lastPos;
    private Vector3 shakeOffset;
    private bool sinking;
    private float velocityDampen;
    private float sinkDuration;
    private float idleBobY;
    private float landBobOffsetY;
    private float sinkTimer;
    private float sinkOffsetY;
    private float sinkLoseTime;
    private float idleTimer;
    
    public void Initialize(Type type, Vector3 pos, float scale, float heightScaleRatio, float velocity, float sinkTime, int tileDepth, int scoreReward) {
        // Set tile type.
        int typeIndex = (int)type;
        tileType = type;

        // Basic properties
        targetPos = pos;
        shakeOffset = Vector3.zero;
        UpdateEntityPos();

        TileSettings properties = tiles[typeIndex];
        float scaleMultiplier = tiles[typeIndex].tileScaleFactor;
        float hbX = Mathf.Max(properties.minimumDimensions.x, scale * scaleMultiplier);
        float hbY = Mathf.Max(properties.minimumDimensions.y, properties.constantHitboxHeight);
        float hbZ = Mathf.Max(properties.minimumDimensions.z, scale * scaleMultiplier * heightScaleRatio * properties.hitboxDepthRatio);

        // Some hitbox properties should not affect visual scaling.
        Vector3 visualScaling = new Vector3(1f, 1f, 1f / properties.hitboxDepthRatio);

        if(properties.hitboxHeightScaling > 0f) {
            float heightScaler = hbZ * properties.hitboxHeightScaling; // Depth multiplied by factor.
            hbY += heightScaler;
            visualScaling.y = heightScaler * 2f / properties.hitboxDepthRatio; // Scale visual height.
        }

        hitboxExtent = new Vector3(hbX, hbY, hbZ);
        mapDepth = tileDepth;
        baseScoreReward = scoreReward;
        horizontalVelocity = velocity;
        sinkLoseTime = sinkAnimationCurve.keys[1].time; // The point before the rapid sinking animation.
        sinkDuration = sinkTime / sinkLoseTime; // Account for the lose time threshold.

        // Toggle visuals of tile and update its scaling.
        for(int i = 0; i < tiles.Length; i++) {
            if(i == typeIndex) {
                tiles[i].visualRoot.SetActive(true);

                float scaleWidth = hitboxExtent.x * visualScaling.x * 2f;
                float scaleHeight = visualScaling.y; // Independent from hitbox size. Scale with the hitbox height scaling.
                float scaleDepth = hitboxExtent.z * visualScaling.z * 2f;
                tiles[i].visualRoot.transform.localScale = new Vector3(scaleWidth, scaleHeight, scaleDepth);
            }
            else {
                tiles[i].visualRoot.SetActive(false);
            }
        }

        // Other visual stuff.
        RandomizeRotationVisuals();
        ResetInsect();
        ResetParticles();

        // Other variables.
        frogAttached = false;
        didAwardScore = false;
        sinking = false;
        velocityDampen = 1f;
        landBobOffsetY = 0f;
        idleBobY = 0f;
        sinkTimer = 0f;
        sinkOffsetY = 0f;
        idleTimer = Mathf.PI * 2f * Random.value;
    }
    
    private void Update() {
        float dt = Time.deltaTime;

        if(horizontalVelocity != 0f && !curSettings.ensureStatic) {
            float dampenFactor = curSettings.velocityDampenFactor;

            if(frogAttached)
                velocityDampen = Mathf.Max(1f - dampenFactor, velocityDampen - (dt * dampenFactor));
            else
                velocityDampen = Mathf.Min(velocityDampen + (dt * dampenFactor), 1f);

            lastPos = targetPos;
            targetPos.x += horizontalVelocity * velocityDampen * dt;
        }

        if(sinking) {
            // Sinking movement.
            sinkTimer += dt / sinkDuration;
            sinkOffsetY = sinkAnimationCurve.Evaluate(sinkTimer) * hitboxExtent.y * sinkLoseTime;
            
            // Stop sinking at the end of animation.
            if(sinkTimer > 1f) {
                sinkOffsetY = sinkAnimationCurve.Evaluate(1f) * hitboxExtent.y;
                sinkTimer = 1f;
                sinking = false;
            }

            if(sinkTimer > sinkLoseTime * 0.75f) {
                float x = (Mathf.PerlinNoise(48f, Time.time * SINK_SHAKE_SPEED) - 0.5f) * SINK_SHAKE_INTENSITY;
                float z = (Mathf.PerlinNoise(Time.time * SINK_SHAKE_SPEED, -32f) - 0.5f) * SINK_SHAKE_INTENSITY;
                shakeOffset = new Vector3(x, 0f, z);
            }

            // Lose condition when it starts dropping.
            if(frogAttached && sinkTimer > sinkLoseTime) {
                GameController.instance.TriggerLoss();
                OnFrogDetach(GameController.instance.frog, false);
            }
        }

        bool hasBobAnimation = (curSettings.idleBobAmount > 0f && curSettings.idleBobSpeed > 0f);

        // Tile horizontally.
        if(curSettings.repeatHorizontallyOffscreen) {
            if(targetPos.x < GameController.instance.leftBounds - hitboxExtent.x - GameController.HORIZONTAL_SCREEN_BUFFER) {
                ShiftEntityX(horizontalResetDist);
            }
            else if(targetPos.x > GameController.instance.rightBounds + hitboxExtent.x + GameController.HORIZONTAL_SCREEN_BUFFER) {
                ShiftEntityX(-horizontalResetDist);
            }
        }
        
        if(hasBobAnimation) {
            if(frogAttached) {
                // Stay still.
                idleTimer = Mathf.Lerp(idleTimer, (idleTimer > Mathf.PI) ? Mathf.PI * 2f : 0f, Time.deltaTime * 3f);
            }
            else {
                // Sinusoidal bobbing.
                idleTimer += dt * curSettings.idleBobSpeed;

                if(idleTimer >= Mathf.PI * 2f)
                    idleTimer -= Mathf.PI * 2f;
            }

            idleBobY = (Mathf.Sin(idleTimer) - 1f) * curSettings.idleBobAmount;
        }
        
        UpdateEntityPos();
    }

    private void UpdateEntityPos() {
        lastPos = cachedTrans.position;
        cachedTrans.position = targetPos + shakeOffset + new Vector3(0f, idleBobY + landBobOffsetY + sinkOffsetY, 0f);
        moveDelta = cachedTrans.position - lastPos;

        // Reposition foam particles after updating position.
        float waterHeight = GameController.instance.waterHeight;

        foreach(Transform fpt in curSettings.surfaceParticleTrans) {
            Vector3 pos = fpt.position;
            pos.y = waterHeight + 0.01f;
            fpt.position = pos;
        }
        
        // Update the emission of particles.
        bool tileAboveWater = (topSurfaceY > waterHeight + 0.01f);

        foreach(ParticleSystem fp in curSettings.foamParticles) {
            ParticleSystem.EmissionModule emit = fp.emission;
            emit.enabled = tileAboveWater; // Toggle foam when above water.
        }

        if(!tileAboveWater && curSettings.sinkingParticles != null) {
            ParticleSystem.EmissionModule emit = curSettings.sinkingParticles.emission;
            emit.enabled = false; // Disable sinking particles when below water.
        }
    }

    private void ShiftEntityX(float shiftAmount) {
        Vector3 shift = new Vector3(shiftAmount, 0f, 0f);
        cachedTrans.position += shift;
        targetPos += shift;
        lastPos += shift; // Ignore teleportation move delta.
        moveDelta = cachedTrans.position - lastPos; // Recalculate movement delta immediately.
    }

    public JumpStatus GetJumpStatus(Vector3 pos, float frogRadius, out int landedPosIndex) {
        JumpStatus basicStatus = OnPadStatus(pos, frogRadius, out landedPosIndex); // Get if we are simply on the pad or not.

        // Check for special conditions/statuses.
        if(basicStatus != JumpStatus.Failed && insectIsActive) {
            float sqrDistFromInsect = HorizontalSqrDistance(pos, ApplyHorizontalOffsetToPosition(insect.cachedTrans.position, landedPosIndex));

            if(sqrDistFromInsect < frogRadius * frogRadius) {
                if(basicStatus == JumpStatus.NearMiss_Success)
                    return JumpStatus.NearMiss_Perfect; // Touching insect, but nearly missed tile.
                else
                    return JumpStatus.Perfect; // Touching insect.
            }
        }

        return basicStatus;
    }

    private JumpStatus OnPadStatus(Vector3 frogPos, float frogRadius, out int landedPosIndex) {
        if(CanLand(frogPos, frogRadius, out landedPosIndex)) {
            Vector3 adjustedTilePos = ApplyHorizontalOffsetToPosition(targetPos, landedPosIndex);

            if(tileType == Type.LilyPad) {
                float sqrDistFromCenter = HorizontalSqrDistance(adjustedTilePos, frogPos);

                // Nearly missed the tile: 0% to 75% of the frog is hanging off.
                float nearMissThreshold = hitboxExtent.x - frogRadius;
                bool nearMiss = (sqrDistFromCenter > nearMissThreshold * nearMissThreshold);

                return (nearMiss) ? JumpStatus.NearMiss_Success : JumpStatus.Success;
            }
            else { // drifting log or floating box.
                Vector2 distFromCenter = new Vector2(Mathf.Abs(frogPos.x - adjustedTilePos.x), Mathf.Abs(frogPos.z - adjustedTilePos.z));

                // Nearly missed the tile: 0% to 75% of the frog is hanging off.
                float nearMissX = hitboxExtent.x - frogRadius;
                float nearMissY = hitboxExtent.z - frogRadius;
                bool nearMiss = (distFromCenter.x > nearMissX || distFromCenter.y > nearMissY);

                return (nearMiss) ? JumpStatus.NearMiss_Success : JumpStatus.Success;
            }
        }

        return JumpStatus.Failed; // Missed completely.
    }

    public Vector3 GetStablePosition(Vector3 frogPos, float frogRadius, float minMoveDist, int landedPosIndex) {
        Vector3 adjustedTilePos = ApplyHorizontalOffsetToPosition(cachedTrans.position, landedPosIndex);

        // Find the closest point to frog that will make frog 100% on the tile.
        // Dir is the vector to the stable position from the center.
        Vector3 dir = (frogPos - adjustedTilePos); // Default: from center to frog.
        dir.y = 0f; // Horizontal direction.

        if(tileType == Type.LilyPad) {
            float stableDistFromCenter = radius - frogRadius;
            adjustedTilePos += dir.normalized * stableDistFromCenter;
        }
        else if(tileType == Type.DriftingLog || tileType == Type.FloatingBox) {
            float stableX = hitboxExtent.x - frogRadius;
            float stableZ = hitboxExtent.z - frogRadius;

            // Clamp to edges.
            dir.x = Mathf.Clamp(dir.x, -stableX, stableX);
            dir.z = Mathf.Clamp(dir.z, -stableZ, stableZ);

            adjustedTilePos += dir; // Apply modified direction.
        }

        // Check whether the stable position is farther than the minimum.
        float curJumpDist = (adjustedTilePos - frogPos).magnitude;

        if(curJumpDist > 0f && minMoveDist > 0f && curJumpDist < minMoveDist)
            adjustedTilePos = frogPos - (dir * minMoveDist); // Move towards center by the minimum distance.

        // Force the frog position to be on the tile's top surface.
        adjustedTilePos.y = topSurfaceY;
        return adjustedTilePos;
    }

    public bool CanLand(Vector3 frogPos, float frogRadius, out int landedPosIndex) {
        if(tileType == Type.LilyPad) {
            // Circular hitbox.
            float successDistThreshold = hitboxExtent.x + (frogRadius * 0.5f); // At least 25% of the frog has to be within the tile for it to count.

            for(int i = 0; i <= 2; i++) {
                float sqrDistFromCenter = GetCircularHitboxOffset(i, frogPos, targetPos);

                // Within circular hitbox.
                if(sqrDistFromCenter < successDistThreshold * successDistThreshold) {
                    landedPosIndex = i;
                    return true;
                }
            }
        }
        else { // drifting log or floating box.
            if(tileType == Type.DriftingLog || tileType == Type.FloatingBox) {
                // At least 25% of the frog has to be within the tile for it to count.
                float thresholdX = hitboxExtent.x + (frogRadius * 0.5f);
                float thresholdY = hitboxExtent.z + (frogRadius * 0.5f);

                for(int i = 0; i <= 2; i++) {
                    Vector2 distFromCenter = GetRectangularHitboxOffset(i, frogPos, targetPos);

                    // Within rectangular hitbox.
                    if(distFromCenter.x < thresholdX && distFromCenter.y < thresholdY) {
                        landedPosIndex = i;
                        return true;
                    }
                }
            }
        }

        landedPosIndex = -1;
        return false;
    }
    
    // Tests circular hitbox, including off-screen. Returns true if frog is within it.
    private float GetCircularHitboxOffset(int posIndex, Vector3 frogPos, Vector3 tilePos) {
        Vector3 adjustedTilePos = ApplyHorizontalOffsetToPosition(tilePos, posIndex);
        return HorizontalSqrDistance(adjustedTilePos, frogPos);
    }

    // Tests rectangular hitbox, including off-screen. Returns true if frog is within it.
    private Vector2 GetRectangularHitboxOffset(int posIndex, Vector3 frogPos, Vector3 tilePos) {
        Vector3 adjustedTilePos = ApplyHorizontalOffsetToPosition(tilePos, posIndex);
        return new Vector2(Mathf.Abs(frogPos.x - adjustedTilePos.x), Mathf.Abs(frogPos.z - adjustedTilePos.z));
    }
    
    private float HorizontalSqrDistance(Vector3 a, Vector3 b) {
        float dx = b.x - a.x;
        float dz = b.z - a.z;
        return (dx * dx) + (dz * dz); // Standard distance formula without sqrt applied.
    }

    // Accounts for off-screen tile positions. posIndex: 0 = center, 1 = left offscreen, 2 = right offscreen.
    private Vector3 ApplyHorizontalOffsetToPosition(Vector3 pos, int posIndex) {
        if(posIndex == 1)
            return new Vector3(pos.x - horizontalResetDist, pos.y, pos.z);
        else if(posIndex == 2)
            return new Vector3(pos.x + horizontalResetDist, pos.y, pos.z);

        return pos;
    }

    private void RandomizeRotationVisuals() {
        float angle = 0f;

        if(tileType == Type.DriftingLog)
            angle = Random.Range(0, 2) * 180f;
        else if(tileType == Type.LilyPad || tileType == Type.FloatingBox)
            angle = Random.Range(0, 4) * 90f;

        if(angle > 0f)
            curSettings.visualMeshTrans.rotation *= Quaternion.Euler(0f, 0f, angle);
    }

    public void OnFrogAttached(Transform frogTrans) {
        if(frogAttached)
            return;

        // Parent to tile and snap position to top surface.
        frogAttached = true;
        Vector3 frogPos = frogTrans.position;
        frogPos.y = topSurfaceY;
        frogTrans.position = frogPos;

        // Too Late To Turn Back achievement.
        int tileDifficulty = (mapDepth / GameController.instance.increaseDifficultyInterval) + 1;
        int recordDifficulty = PlayerPrefs.GetInt("RecordDifficultyReached", 0);

        if(tileDifficulty > recordDifficulty) {
            PlayerPrefs.SetInt("RecordDifficultyReached", tileDifficulty);
            AchievementController.instance.UpdateProgressAll(false);
        }

        // Do the bob animation.
        PlayBobAnimation();
    }

    public void OnFrogDetach(Transform frogTrans, bool fromJump) {
        if(!frogAttached)
            return;

        if(fromJump && timeUntilSink <= 0.175f) {
            // Jumped during last 0.175 seconds before lose condition.
            AchievementController.instance.AwardEventAchievement(AchievementID.QuickFeet, false);
        }

        frogAttached = false;
    }

    public void StartSinking() {
        if(curSettings.isSinkable) {
            sinking = true;

            if(curSettings.sinkingParticles != null) {
                // Start playing sinking particles.
                ParticleSystem.EmissionModule emit = curSettings.sinkingParticles.emission;
                emit.enabled = true;
            }
        }
    }

    public void PlayBobAnimation() {
        if(bobRoutine.IsValid)
            Timing.KillCoroutines(bobRoutine);

        if(curSettings.landBobStrength > 0f)
            bobRoutine = Timing.RunCoroutine(DoBobRoutine(), "tile");
    }

    private void ResetParticles() {
        foreach(ParticleSystem fp in curSettings.foamParticles) {
            ParticleSystem.EmissionModule em = fp.emission;
            em.enabled = true;
        }

        if(curSettings.sinkingParticles != null) {
            ParticleSystem.EmissionModule emit = curSettings.sinkingParticles.emission;
            emit.enabled = false;
        }
    }

    private void ResetInsect() {
        insect.Initialize();
        insectIsActive = true;

        Vector2 randomOff;
        float extentX = Mathf.Max(0f, hitboxExtent.x - insect.curInsect.radius);
        float extentZ = Mathf.Max(0f, hitboxExtent.z - insect.curInsect.radius);

        if(tileType == Type.DriftingLog) {
            // Mostly random from left to right.
            float x = Random.Range(-extentX, extentX) * insectMaxOffsetRatio;
            float z = Random.Range(-extentZ, extentZ) * 0.5f * insectMaxOffsetRatio;
            randomOff = new Vector2(x, z);
        }
        else if(tileType == Type.FloatingBox) {
            // Random position within the box.
            float x = Random.Range(-extentX, extentX) * insectMaxOffsetRatio;
            float z = Random.Range(-extentZ, extentZ) * insectMaxOffsetRatio;
            randomOff = new Vector2(x, z);
        }
        else {
            // Random position within the circle.
            randomOff = Random.insideUnitCircle * extentX * insectMaxOffsetRatio;
        }

        insect.cachedTrans.localPosition = new Vector3(randomOff.x, hitboxExtent.y, randomOff.y);
    }

    public void ClearInsect(Vector3 frogPos, bool eaten) {
        insect.Disappear(frogPos, eaten);
        insectIsActive = false;
    }

    private IEnumerator<float> DoBobRoutine() {
        float timer = 0f;

        while(timer < 1f) {
            timer += Time.deltaTime;
            landBobOffsetY = frogLandBobCurve.Evaluate(timer) * curSettings.landBobStrength;
            yield return 0f;
        }

        // Reset to starting position.
        landBobOffsetY = 0f;
    }

    private void OnDrawGizmosSelected() {
        // Draw hitbox as a gizmo.
        Gizmos.color = new Color(0.75f, 0.2f, 0.45f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, hitboxExtent);

        if(tileType == Type.LilyPad)
            Gizmos.DrawSphere(Vector3.zero, 1f);
        else
            Gizmos.DrawCube(Vector3.zero, new Vector3(2f, 2f, 2f));
    }

    [System.Serializable]
    public class TileSettings {
        public GameObject visualRoot;           // The gameobject root for this tile.
        public Transform visualRootTrans;       // Same thing as above, except it is the transform component.
        public Transform visualMeshTrans;       // The actual mesh (transform)
        public Transform[] surfaceParticleTrans;    // Contains all the transforms of the particles on water surface.
        public ParticleSystem[] foamParticles;  // The particles to emit while tile is afloat.
        public Vector3 minimumDimensions = new Vector3(0.25f, 0f, 0.25f); // Minimum size of hitbox for any tile of this type. Circular hitboxes will use the maximum of these two values.
        public float tileScaleFactor = 1f;      // Simple scale multiplier for unit size.
        public float hitboxDepthRatio = 1f;     // != 1 for non-square tiles.
        public float constantHitboxHeight = 0.05f;  // Constant hitbox height to be set. Will not affect the visual scaling.
        public float hitboxHeightScaling = 0f;  // The factor to scale with hitbox depth. Set to 0 to disable scaling.
        public bool ensureStatic = true;        // Set to true if it cannot be affected by velocity.
        public float velocityDampenFactor = 0.5f;   // The amount to dampen velocity by when frog is on the tile.
        public bool repeatHorizontallyOffscreen = true; // Set to true if it will repeat horizontal position when going off screen.
        public float landBobStrength = 1f;      // Bob animation strength multiplier when frog lands on this tile.
        public float idleBobSpeed = 1f;         // Idle bob animation speed.
        public float idleBobAmount = 0f;        // Idle bob animation strength.
        public bool isSinkable = false;         // Whether or not this tile will sink while the frog is attached to it.
        public ParticleSystem sinkingParticles; // Particles to emit while sinking.
    }
}