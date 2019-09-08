using UnityEngine;
using System.Collections.Generic;

public class TileController : MonoBehaviour {
    private const int TILE_POOL_SIZE = 6;

    public List<TileEntity> activeTiles;
    public Queue<TileEntity> pooledTiles;
    public GameController gameController;
    public TileEntity tilePrefab;
    public float angleVariance = 15f;
    public float baseDistance = 3f; // Easiest difficulty.
    public float maxDistance = 6f; // Hardest difficulty.
    public Vector2 distanceVariance = new Vector2(0.7f, 1.3f);
    public float baseSizeRadius = 1f; // Easiest difficulty.
    public float minSizeMultiplier = 0.2f; // Hardest difficulty.
    public Vector2 sizeVariance = new Vector2(0.9f, 1.1f);
    public float baseDriftVelocity = 0.5f; // Easiest difficulty.
    public float maxDriftVelocity = 2.5f; // Hardest difficulty.
    public Vector2 velocityVariance = new Vector2(0.9f, 1.1f);
    public float baseSinkTime = 3.5f; // Easiest difficulty.
    public float minSinkTime = 1f; // Hardest difficulty.
    public float sizeDifficultyFactor = 0.95f;
    public float distanceDifficultyFactor = 1.05f;
    public float velocityDifficultyFactor = 1.06f;
    public float sinkTimeDifficultyFactor = 0.95f;
    public TileDistribution[] tileTypeDistributions = new TileDistribution[1];

    public float minTileZ { get; private set; }

    public int tileDifficultyLevel {
        get {
            return (lastTileDepth / gameController.increaseDifficultyInterval);
        }
    }

    private Vector3 lastTilePos;
    private TileDifficulty[] difficultyStats;
    private int lastTileDepth;
    private int curBaseScoreReward;

    public void Initialize() {
        // Create tile pool.
        pooledTiles = new Queue<TileEntity>(TILE_POOL_SIZE);
        activeTiles = new List<TileEntity>(TILE_POOL_SIZE);

        for(int i = 0; i < TILE_POOL_SIZE; i++) {
            TileEntity newEntity = Instantiate(tilePrefab);
            ReturnToPool(newEntity);
        }

        difficultyStats = new TileDifficulty[TileEntity.TYPE_COUNT];
    }

    public void Reset(Vector3 initPos) {
        // Reset difficulty and reward.
        lastTileDepth = 0;
        curBaseScoreReward = 1;

        for(int i = 0; i < difficultyStats.Length; i++) 
            difficultyStats[i].Init(gameController.increaseDifficultyInterval, sizeDifficultyFactor, distanceDifficultyFactor, velocityDifficultyFactor, sinkTimeDifficultyFactor);

        for(int i = 0; i < activeTiles.Count; i++)
            ReturnToPool(activeTiles[i]);

        activeTiles.Clear();

        UpdateTileBounds();
        lastTilePos = initPos;

        // Create tiles.
        for(int i = 0; i < pooledTiles.Count; i++)
            PlaceNextTile();
    }

    public TileEntity PlaceNextTile() {
        if(pooledTiles.Count == 0)
            return null; // Pool is empty.
        
        // Ensure tile position is at water height.
        lastTilePos.y = gameController.waterHeight;
        
        // Determine tile type from difficulty.
        TileEntity.Type tileType = TileEntity.Type.LilyPad;
        int difficultyCount = tileTypeDistributions[0].difficultyProbabilities.Length;

        if(tileDifficultyLevel > 0) {
            // Introduce new tile types through distribution.
            float distribValue = Random.value;
            int difficultyDistribCount = tileTypeDistributions[0].difficultyProbabilities.Length;
            int distribIndex = Mathf.Min(tileDifficultyLevel, difficultyDistribCount - 1);
            
            float distribStart = 0f;
            float distribEnd = 0f;

            for(int i = 0; i < tileTypeDistributions.Length; i++) {
                float prob = tileTypeDistributions[i].difficultyProbabilities[distribIndex];
                distribEnd += prob;

                if(distribValue >= distribStart && distribValue < distribEnd) {
                    tileType = (TileEntity.Type)i;
                    break; // Select this tile.
                }

                distribStart += prob;
            }
        }
        
        int tileIndex = (int)tileType;
        TileDifficulty tileDiff = difficultyStats[tileIndex];

        // Place the next pad somewhere in front of the last tile position.
        float distFromFrog = Mathf.Min(baseDistance * tileDiff.distMultiplier * Random.Range(distanceVariance.x, distanceVariance.y), maxDistance);
        float angleRad = Random.Range(-angleVariance, angleVariance) * Mathf.Deg2Rad;
        lastTilePos += new Vector3(Mathf.Sin(angleRad) * distFromFrog, 0f, Mathf.Cos(angleRad) * distFromFrog);

        TileEntity newTile = pooledTiles.Dequeue();
        float radius = baseSizeRadius * Mathf.Max(minSizeMultiplier, tileDiff.sizeMultiplier * Random.Range(sizeVariance.x, sizeVariance.y));
        float sinkTime = Mathf.Max(minSinkTime, baseSinkTime * tileDiff.sinkTimeMultiplier);

        if(tileType == TileEntity.Type.DriftingLog) {
            // Determine horizontal velocity.
            float heightFactor = Mathf.Max(minSizeMultiplier, tileDiff.sizeMultiplier * Random.Range(sizeVariance.x, sizeVariance.y)); // Calculate another random scale factor for height.
            float velocity = Mathf.Min(baseDriftVelocity * tileDiff.velocityMultiplier, maxDriftVelocity) * Random.Range(velocityVariance.x, velocityVariance.y);

            if(Random.value < 0.5f)
                velocity = -velocity; // 50% chance to go left.

            newTile.Initialize(tileType, lastTilePos, radius, heightFactor, velocity, sinkTime, lastTileDepth, curBaseScoreReward);
        }
        else { // lilypad and floating box.
            newTile.Initialize(tileType, lastTilePos, radius, 1f, 0f, sinkTime, lastTileDepth, curBaseScoreReward);
        }

        // Add to active list, since it is a valid thing to jump on.
        activeTiles.Add(newTile);

        // Increase difficulty for this specific tile.
        difficultyStats[tileIndex].AdvanceProgress();

        // Increase reward every X tiles.
        lastTileDepth++;

        if(lastTileDepth % gameController.increaseDifficultyInterval == 0)
            curBaseScoreReward++;

        UpdateTileBounds();
        return newTile;
    }

    public void RecycleOldestTile() {
        if(activeTiles.Count == 0)
            return;

        int oldestDepth = activeTiles[0].mapDepth; // oldest depth = lower.
        int targetIndex = 0;

        for(int i = 1; i < activeTiles.Count; i++) {
            if(activeTiles[i].mapDepth < oldestDepth) {
                oldestDepth = activeTiles[i].mapDepth;
                targetIndex = i;
            }
        }
        
        ReturnToPool(activeTiles[targetIndex]);
        activeTiles.RemoveAt(targetIndex);
        
        UpdateTileBounds();
    }
    
    public void UpdateTileBounds() {
        minTileZ = float.MaxValue;

        for(int i = 0; i < activeTiles.Count; i++) {
            float z = activeTiles[i].cachedTrans.position.z;

            if(z < minTileZ)
                minTileZ = z + activeTiles[i].radius;
        }
    }
    
    private void ReturnToPool(TileEntity tile) {
        pooledTiles.Enqueue(tile);
    }

    public struct TileDifficulty {
        public int progress;
        public float sizeMultiplier;
        public float distMultiplier;
        public float velocityMultiplier;
        public float sinkTimeMultiplier;

        private int advanceInterval;
        private float sizeFactor;
        private float distFactor;
        private float velocityFactor;
        private float sinkTimeFactor;

        public void Init(int interval, float size, float dist, float vel, float sink) {
            progress = 0;
            sizeMultiplier = 1f;
            distMultiplier = 1f;
            velocityMultiplier = 1f;
            sinkTimeMultiplier = 1f;

            advanceInterval = interval;
            sizeFactor = size;
            distFactor = dist;
            velocityFactor = vel;
            sinkTimeFactor = sink;
        }

        public void AdvanceProgress() {
            progress++;

            if(progress >= advanceInterval) {
                sizeMultiplier *= sizeFactor;
                distMultiplier *= distFactor;
                velocityMultiplier *= velocityFactor;
                sinkTimeMultiplier *= sinkTimeFactor;
                progress = 0;
            }
        }
    }

    [System.Serializable]
    public class TileDistribution {
        public float[] difficultyProbabilities;
    }
}