namespace CloneTato.Data;

public class WaveConfig
{
    public float Duration;
    public int TotalEnemies;
    public float BaseSpawnRate; // enemies per second (multiplied by phase curve in WaveSystem)
    public int[] EnemyTypeIndices = [];
    public float[]? SpawnWeights; // optional per-enemy-type weights (parallel to EnemyTypeIndices)
    public bool IsBossWave;
    public int GoldReward;

    /// <summary>
    /// Get wave config for a specific biome and wave number.
    /// Biome 1 = The Waste, Biome 2 = Blood Desert, Biome 3 = The Temple.
    /// </summary>
    public static WaveConfig GetWave(int biome, int wave)
    {
        return biome switch
        {
            1 => GetWasteWave(wave),
            2 => GetBloodDesertWave(wave),
            3 => GetTempleWave(wave),
            _ => GetWasteWave(wave), // fallback
        };
    }

    // Biome 1: The Waste
    // Core: Small Bug [1], Medium Insect [2], Rusty Robot [10], Delivery Bot [13], Spiny Beetle [8], Big Bug [7]
    // Crossover: Tribe Hunter [0] (ranged), Warrior [6] (rush melee), Hooded Minion [14] (assassin)
    // Boss: Dust Warrior
    // Design: fast roster ramp, cross-biome enemies for variety from wave 3+
    private static WaveConfig GetWasteWave(int wave)
    {
        return wave switch
        {
            1 => new WaveConfig
            {
                Duration = 35f,
                TotalEnemies = 30,
                BaseSpawnRate = 0.80f,
                EnemyTypeIndices = [1, 2],                 // Bugs + Insects
                SpawnWeights = [0.65f, 0.35f],
                GoldReward = 12,
            },
            2 => new WaveConfig
            {
                Duration = 40f,
                TotalEnemies = 38,
                BaseSpawnRate = 0.85f,
                EnemyTypeIndices = [1, 2, 10],             // + Rusty Robot (kamikaze)
                SpawnWeights = [0.40f, 0.30f, 0.30f],
                GoldReward = 15,
            },
            3 => new WaveConfig
            {
                Duration = 45f,
                TotalEnemies = 48,
                BaseSpawnRate = 0.90f,
                EnemyTypeIndices = [1, 2, 0, 8, 10],       // + Tribe Hunter (ranged!) + Spiny Beetle
                SpawnWeights = [0.25f, 0.20f, 0.18f, 0.20f, 0.17f],
                GoldReward = 18,
            },
            4 => new WaveConfig
            {
                Duration = 50f,
                TotalEnemies = 55,
                BaseSpawnRate = 0.95f,
                EnemyTypeIndices = [1, 2, 0, 6, 7, 8, 10, 13], // + Warrior (rush) + Big Bug + Delivery Bot
                SpawnWeights = [0.16f, 0.12f, 0.12f, 0.12f, 0.10f, 0.14f, 0.12f, 0.12f],
                GoldReward = 20,
            },
            5 => new WaveConfig
            {
                Duration = 55f,
                TotalEnemies = 65,
                BaseSpawnRate = 1.0f,
                EnemyTypeIndices = [1, 2, 0, 6, 7, 8, 10, 13],
                SpawnWeights = [0.14f, 0.12f, 0.12f, 0.14f, 0.12f, 0.14f, 0.10f, 0.12f],
                GoldReward = 22,
            },
            6 => new WaveConfig
            {
                Duration = 60f,
                TotalEnemies = 72,
                BaseSpawnRate = 1.05f,
                EnemyTypeIndices = [1, 2, 0, 6, 7, 8, 10, 14], // + Hooded Minion (assassin crossover)
                SpawnWeights = [0.14f, 0.10f, 0.10f, 0.14f, 0.14f, 0.14f, 0.12f, 0.12f],
                GoldReward = 25,
            },
            7 => new WaveConfig
            {
                Duration = 65f,
                TotalEnemies = 82,
                BaseSpawnRate = 1.10f,
                EnemyTypeIndices = [1, 0, 6, 7, 8, 10, 14], // Heavy mix — bugs, humanoids, bots
                SpawnWeights = [0.14f, 0.12f, 0.16f, 0.16f, 0.16f, 0.14f, 0.12f],
                GoldReward = 28,
            },
            8 => new WaveConfig
            {
                Duration = 70f,
                TotalEnemies = 92,
                BaseSpawnRate = 1.15f,
                EnemyTypeIndices = [1, 6, 7, 8, 10, 14],   // Pure combat — rush + tank + assassin
                SpawnWeights = [0.16f, 0.18f, 0.20f, 0.18f, 0.14f, 0.14f],
                GoldReward = 32,
            },
            9 => new WaveConfig
            {
                Duration = 75f,
                TotalEnemies = 105,
                BaseSpawnRate = 1.25f,
                EnemyTypeIndices = [1, 6, 7, 8, 10, 14],   // Pre-boss gauntlet — relentless
                SpawnWeights = [0.14f, 0.18f, 0.22f, 0.18f, 0.16f, 0.12f],
                GoldReward = 38,
            },
            10 => new WaveConfig
            {
                Duration = 90f,
                TotalEnemies = 40,                          // Steady adds during boss fight
                BaseSpawnRate = 0.55f,
                EnemyTypeIndices = [1, 10, 6],              // Bugs + Robots + Warriors
                SpawnWeights = [0.40f, 0.30f, 0.30f],
                IsBossWave = true,
                GoldReward = 50,
            },
            _ => GetWasteWave(Math.Clamp(wave, 1, 10)),
        };
    }

    // Biome 2: Blood Desert
    // Core: Tribe Warrior [3], Archer [4], Guard [5], Warrior [6], Relic Guardian [9]
    // Filler: Small Bug [1] (fast melee), Spiny Beetle [8] (ranged crossover)
    // Boss: Blowfish
    // Design: melee-heavy early, archers mixed in (not dominant). Feels like a tribal rush.
    private static WaveConfig GetBloodDesertWave(int wave)
    {
        return wave switch
        {
            1 => new WaveConfig
            {
                Duration = 40f,
                TotalEnemies = 35,
                BaseSpawnRate = 0.80f,
                EnemyTypeIndices = [6, 1],                 // Warriors + Small Bugs — melee rush intro
                SpawnWeights = [0.55f, 0.45f],
                GoldReward = 14,
            },
            2 => new WaveConfig
            {
                Duration = 45f,
                TotalEnemies = 42,
                BaseSpawnRate = 0.85f,
                EnemyTypeIndices = [4, 6, 1],              // + Archers (minority, not wall)
                SpawnWeights = [0.25f, 0.40f, 0.35f],
                GoldReward = 16,
            },
            3 => new WaveConfig
            {
                Duration = 50f,
                TotalEnemies = 50,
                BaseSpawnRate = 0.90f,
                EnemyTypeIndices = [3, 4, 5, 6],           // Full humanoid roster
                SpawnWeights = [0.25f, 0.20f, 0.25f, 0.30f],
                GoldReward = 20,
            },
            4 => new WaveConfig
            {
                Duration = 55f,
                TotalEnemies = 58,
                BaseSpawnRate = 0.95f,
                EnemyTypeIndices = [1, 3, 4, 5, 6],        // + Small Bug filler
                SpawnWeights = [0.18f, 0.22f, 0.18f, 0.20f, 0.22f],
                GoldReward = 22,
            },
            5 => new WaveConfig
            {
                Duration = 60f,
                TotalEnemies = 65,
                BaseSpawnRate = 1.0f,
                EnemyTypeIndices = [1, 3, 4, 5, 6, 8],     // + Spiny Beetle
                SpawnWeights = [0.14f, 0.18f, 0.16f, 0.18f, 0.18f, 0.16f],
                GoldReward = 25,
            },
            6 => new WaveConfig
            {
                Duration = 65f,
                TotalEnemies = 72,
                BaseSpawnRate = 1.05f,
                EnemyTypeIndices = [1, 3, 4, 5, 6, 8, 9],  // + Relic Guardian (full roster)
                SpawnWeights = [0.10f, 0.14f, 0.14f, 0.14f, 0.16f, 0.16f, 0.16f],
                GoldReward = 28,
            },
            7 => new WaveConfig
            {
                Duration = 70f,
                TotalEnemies = 82,
                BaseSpawnRate = 1.10f,
                EnemyTypeIndices = [3, 4, 5, 6, 8, 9],     // Core roster, heavy
                SpawnWeights = [0.16f, 0.14f, 0.16f, 0.18f, 0.16f, 0.20f],
                GoldReward = 30,
            },
            8 => new WaveConfig
            {
                Duration = 75f,
                TotalEnemies = 92,
                BaseSpawnRate = 1.15f,
                EnemyTypeIndices = [3, 4, 5, 6, 9],        // Drop filler, pure humanoid combat
                SpawnWeights = [0.18f, 0.16f, 0.18f, 0.22f, 0.26f],
                GoldReward = 34,
            },
            9 => new WaveConfig
            {
                Duration = 80f,
                TotalEnemies = 105,
                BaseSpawnRate = 1.25f,
                EnemyTypeIndices = [3, 4, 5, 6, 9],        // Pre-boss gauntlet — heavy Relic Guardians
                SpawnWeights = [0.16f, 0.14f, 0.16f, 0.20f, 0.34f],
                GoldReward = 40,
            },
            10 => new WaveConfig
            {
                Duration = 100f,
                TotalEnemies = 30,
                BaseSpawnRate = 0.50f,
                EnemyTypeIndices = [6, 1],                  // Warriors + Bugs as adds (no archers during boss)
                SpawnWeights = [0.55f, 0.45f],
                IsBossWave = true,
                GoldReward = 55,
            },
            _ => GetBloodDesertWave(Math.Clamp(wave, 1, 10)),
        };
    }

    // Biome 3: The Temple
    // Core: Hooded Minion [14], Circle Bot [12], Ranged Minion [16], Guard Robot [11], Bomb Minion [15], Planter Bot [17]
    // Filler: Small Bug [1] (fast filler)
    // Boss: Tarnished Widow
    // Design: assassins + explosions. Fast, chaotic, high threat.
    private static WaveConfig GetTempleWave(int wave)
    {
        return wave switch
        {
            1 => new WaveConfig
            {
                Duration = 40f,
                TotalEnemies = 38,
                BaseSpawnRate = 0.85f,
                EnemyTypeIndices = [14, 15],               // Hooded + Bomb Minions — chaos intro
                SpawnWeights = [0.60f, 0.40f],
                GoldReward = 16,
            },
            2 => new WaveConfig
            {
                Duration = 45f,
                TotalEnemies = 45,
                BaseSpawnRate = 0.90f,
                EnemyTypeIndices = [12, 14, 15, 16],       // + Circle Bot + Ranged Minion
                SpawnWeights = [0.22f, 0.30f, 0.22f, 0.26f],
                GoldReward = 20,
            },
            3 => new WaveConfig
            {
                Duration = 50f,
                TotalEnemies = 55,
                BaseSpawnRate = 0.95f,
                EnemyTypeIndices = [11, 12, 14, 15, 16],   // + Guard Robot
                SpawnWeights = [0.18f, 0.18f, 0.24f, 0.20f, 0.20f],
                GoldReward = 22,
            },
            4 => new WaveConfig
            {
                Duration = 55f,
                TotalEnemies = 62,
                BaseSpawnRate = 1.0f,
                EnemyTypeIndices = [11, 12, 14, 15, 16, 17], // Full roster by wave 4
                SpawnWeights = [0.14f, 0.16f, 0.22f, 0.18f, 0.16f, 0.14f],
                GoldReward = 25,
            },
            5 => new WaveConfig
            {
                Duration = 60f,
                TotalEnemies = 70,
                BaseSpawnRate = 1.05f,
                EnemyTypeIndices = [1, 11, 12, 14, 15, 16, 17], // + Bug filler
                SpawnWeights = [0.12f, 0.14f, 0.14f, 0.18f, 0.16f, 0.14f, 0.12f],
                GoldReward = 28,
            },
            6 => new WaveConfig
            {
                Duration = 65f,
                TotalEnemies = 80,
                BaseSpawnRate = 1.10f,
                EnemyTypeIndices = [11, 12, 14, 15, 16, 17],
                SpawnWeights = [0.16f, 0.16f, 0.20f, 0.18f, 0.14f, 0.16f],
                GoldReward = 32,
            },
            7 => new WaveConfig
            {
                Duration = 70f,
                TotalEnemies = 90,
                BaseSpawnRate = 1.15f,
                EnemyTypeIndices = [11, 12, 14, 15, 16, 17], // Heavy everything
                SpawnWeights = [0.18f, 0.18f, 0.16f, 0.18f, 0.14f, 0.16f],
                GoldReward = 35,
            },
            8 => new WaveConfig
            {
                Duration = 75f,
                TotalEnemies = 100,
                BaseSpawnRate = 1.20f,
                EnemyTypeIndices = [11, 12, 14, 15, 17],   // Drop Ranged Minion, mine chaos
                SpawnWeights = [0.18f, 0.18f, 0.22f, 0.20f, 0.22f],
                GoldReward = 38,
            },
            9 => new WaveConfig
            {
                Duration = 80f,
                TotalEnemies = 115,
                BaseSpawnRate = 1.30f,
                EnemyTypeIndices = [11, 12, 14, 15, 16, 17], // Pre-boss — everything maxed
                SpawnWeights = [0.16f, 0.16f, 0.18f, 0.20f, 0.14f, 0.16f],
                GoldReward = 42,
            },
            10 => new WaveConfig
            {
                Duration = 110f,
                TotalEnemies = 35,
                BaseSpawnRate = 0.55f,
                EnemyTypeIndices = [14, 15, 1],            // Hooded + Bomb + Bug adds
                SpawnWeights = [0.40f, 0.35f, 0.25f],
                IsBossWave = true,
                GoldReward = 60,
            },
            _ => GetTempleWave(Math.Clamp(wave, 1, 10)),
        };
    }
}
