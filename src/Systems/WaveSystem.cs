using CloneTato.Core;

namespace CloneTato.Systems;

public static class WaveSystem
{
    public static void Update(float dt, GameState state)
    {
        if (!state.WaveActive) return;

        var config = state.CurrentWaveConfig;
        if (config == null) return;

        state.WaveTimer -= dt;

        // Spawn enemies (respecting per-biome density cap)
        // Global density multiplier — applied to both count and rate
        const float DensityMult = 3.5f;
        int totalEnemies = (int)(config.TotalEnemies * DensityMult);
        float spawnRate = config.BaseSpawnRate * DensityMult;

        int enemyCap = Constants.BiomeEnemyCap(state.CurrentBiome);
        if (state.EnemiesSpawnedThisWave < totalEnemies)
        {
            float waveProgress = 1f - (state.WaveTimer / config.Duration);
            float spawnMult = GetSpawnPhaseMultiplier(waveProgress);
            state.SpawnAccumulator += spawnRate * spawnMult * dt;

            while (state.SpawnAccumulator >= 1f && state.EnemiesSpawnedThisWave < totalEnemies)
            {
                if (state.ActiveEnemyCount() >= enemyCap)
                {
                    // Cap reached — hold accumulator at 1, don't spawn until some die
                    state.SpawnAccumulator = 1f;
                    break;
                }
                EnemySystem.SpawnEnemy(state, state.CurrentWave);
                state.SpawnAccumulator -= 1f;
            }
        }

        // Wave complete: timer expired AND all enemies dead
        bool allSpawned = state.EnemiesSpawnedThisWave >= totalEnemies;
        bool allDead = state.ActiveEnemyCount() == 0;
        bool timerDone = state.WaveTimer <= 0;

        if (allSpawned && allDead && timerDone)
        {
            state.WaveActive = false;
            state.Gold += config.GoldReward;
        }
        else if (timerDone && !allSpawned)
        {
            // Timer ran out but not all spawned: force-spawn remaining faster
            state.SpawnAccumulator += spawnRate * 3f * dt;
        }
    }

    /// <summary>
    /// Phase-based spawn pacing within a wave. Smoothly interpolates between phases.
    /// Ramp (0-25%) → Build (25-55%) → Peak (55-80%) → Surge (80-100%)
    /// </summary>
    private static float GetSpawnPhaseMultiplier(float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);

        // Phase boundaries and multipliers
        // Ramp:  0.0 - 0.25 → 0.6x
        // Build: 0.25 - 0.55 → 1.0x
        // Peak:  0.55 - 0.80 → 1.4x
        // Surge: 0.80 - 1.0  → 1.8x
        if (progress < 0.20f)
            return 0.7f;
        else if (progress < 0.25f)
            return Lerp(0.7f, 1.0f, (progress - 0.20f) / 0.05f);   // smooth ramp→build
        else if (progress < 0.50f)
            return 1.0f;
        else if (progress < 0.55f)
            return Lerp(1.0f, 1.4f, (progress - 0.50f) / 0.05f);   // smooth build→peak
        else if (progress < 0.75f)
            return 1.4f;
        else if (progress < 0.80f)
            return Lerp(1.4f, 1.8f, (progress - 0.75f) / 0.05f);   // smooth peak→surge
        else
            return 1.8f;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}
