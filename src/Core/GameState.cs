using System.Numerics;
using CloneTato.Assets;
using CloneTato.Data;
using CloneTato.Entities;
using Raylib_cs;

namespace CloneTato.Core;

public class GameState
{
    public AssetManager Assets = null!;
    public Player Player = new();
    public List<Enemy> Enemies = new();
    public List<Projectile> Projectiles = new();
    public List<XPOrb> XPOrbs = new();
    public List<DamageNumber> DamageNumbers = new();
    public List<Projectile> EnemyProjectiles = new();
    public List<Mine> Mines = new();
    public List<MeleeSwipe> MeleeSwipes = new();
    public List<HealthPickup> HealthPickups = new();
    public List<Obstacle> Obstacles = new();
    public List<Barrel> Barrels = new();
    public List<TerrainZone> TerrainZones = new();

    // Visual-only ground decorations (no collision)
    public List<GroundScatter> GroundScatterProps = new();

    // Explosion VFX pool
    public const int MaxExplosionVFX = 10;
    public ExplosionVFX[] ExplosionEffects = new ExplosionVFX[MaxExplosionVFX];

    // Enemy mines (placed by Planter Bot)
    public const int MaxEnemyMines = 30;
    public List<EnemyMine> EnemyMines = new();

    public List<WeaponInstance> EquippedWeapons = new();
    public List<float> WeaponCooldowns = new();
    public List<int> WeaponClipAmmo = new();     // current ammo in clip per weapon
    public List<float> WeaponReloadTimers = new(); // >0 means reloading
    public List<float> WeaponOrbitAngles = new(); // visual angle around player
    public List<ItemDef> OwnedItems = new();

    public Vector2 MouseWorldPosition; // mouse cursor in world space
    public bool IsFiring; // true while primary fire button is held (RT / left click)
    public bool IsFirePressed; // true on the frame primary fire is first pressed
    public bool IsSecondaryFiring; // true on the frame secondary fire is pressed (LT / right click)
    public bool IsSecondaryDown;   // true while secondary fire is held (for hold-through-cooldown)
    public bool IsSpecialActivated; // true on the frame special ability is pressed (LB / Q)
    public bool AutoAimEnabled = true; // Brotato-style: auto-target nearest enemy for ranged heroes

    // Special ability cooldown (hero-specific, not a weapon)
    public float SpecialCooldown;
    public float SpecialMaxCooldown;

    public Stats ItemBonus;
    public Stats LevelBonus;
    public Stats MetaBonus;
    public Passives Passives = new(); // mechanical passive upgrades (ricochet, thorns, etc.)

    public bool SandboxMode; // small arena, invincible boss, no waves — for movement testing
    public int EffectiveArenaWidth => SandboxMode ? Constants.LogicalWidth : Constants.ArenaWidth;
    public int EffectiveArenaHeight => SandboxMode ? Constants.LogicalHeight : Constants.ArenaHeight;

    public int CurrentBiome = 1;
    public int CurrentWave;
    public float WaveTimer;
    public bool WaveActive;
    public int EnemiesSpawnedThisWave;
    public int EnemiesKilledThisWave;
    public float SpawnAccumulator;
    public WaveConfig? CurrentWaveConfig;

    public int Gold;
    public int XP;
    public int Level = 1;
    public int XPToNextLevel = 8;
    public bool LevelUpPending;
    public int PendingLevelUps;

    // Combat feel — set by systems, consumed by PlayingScreen
    public float PendingHitstop;        // seconds of hitstop to apply
    public float PendingShakeDuration;
    public float PendingShakeIntensity;

    public void RequestHitstop(float duration)
    {
        if (duration > PendingHitstop) PendingHitstop = duration;
    }

    public void RequestScreenShake(float duration, float intensity)
    {
        if (intensity > PendingShakeIntensity)
        {
            PendingShakeDuration = duration;
            PendingShakeIntensity = intensity;
        }
    }

    // Stats tracking
    public int TotalEnemiesKilled;
    public int TotalDamageDealt;
    public float TotalTimeSurvived;

    // Combo kill system
    public int ComboCount;
    public float ComboTimer;
    public const float ComboWindow = 1.5f; // seconds between kills to maintain combo
    public int BestCombo;

    // Entity pools
    public const int MaxEnemies = 500;
    public const int MaxProjectiles = 500;
    public const int MaxXPOrbs = 400;
    public const int MaxDamageNumbers = 100;
    public const int MaxEnemyProjectiles = 200;
    public const int MaxHealthPickups = 20;
    public const int MaxMines = 30;
    public const int MaxMeleeSwipes = 20;

    public void InitPools()
    {
        Enemies.Clear();
        for (int i = 0; i < MaxEnemies; i++)
            Enemies.Add(new Enemy());

        Projectiles.Clear();
        for (int i = 0; i < MaxProjectiles; i++)
            Projectiles.Add(new Projectile());

        EnemyProjectiles.Clear();
        for (int i = 0; i < MaxEnemyProjectiles; i++)
            EnemyProjectiles.Add(new Projectile());

        XPOrbs.Clear();
        for (int i = 0; i < MaxXPOrbs; i++)
            XPOrbs.Add(new XPOrb());

        DamageNumbers.Clear();
        for (int i = 0; i < MaxDamageNumbers; i++)
            DamageNumbers.Add(new DamageNumber());

        HealthPickups.Clear();
        for (int i = 0; i < MaxHealthPickups; i++)
            HealthPickups.Add(new HealthPickup());

        Mines.Clear();
        for (int i = 0; i < MaxMines; i++)
            Mines.Add(new Mine());

        EnemyMines.Clear();
        for (int i = 0; i < MaxEnemyMines; i++)
            EnemyMines.Add(new EnemyMine());

        MeleeSwipes.Clear();
        for (int i = 0; i < MaxMeleeSwipes; i++)
            MeleeSwipes.Add(new MeleeSwipe());
    }

    public void StartNewRun(CharacterDef character)
    {
        InitPools();
        Player = new Player();
        Player.Init(character);

        // Set active hero sprite based on character type
        Assets.HeroSprite = character.HeroType switch
        {
            HeroType.BladeDancer => Assets.HeroSwordSprite ?? Assets.HeroGunSprite,
            HeroType.Drifter => Assets.StarterHeroSprite ?? Assets.HeroGunSprite,
            _ => Assets.HeroGunSprite,
        };

        EquippedWeapons.Clear();
        WeaponCooldowns.Clear();
        WeaponClipAmmo.Clear();
        WeaponReloadTimers.Clear();
        WeaponOrbitAngles.Clear();
        OwnedItems.Clear();

        // Give starting weapon
        var startWeapon = new WeaponInstance(WeaponDatabase.Weapons[character.StartingWeaponIndex]);
        EquippedWeapons.Add(startWeapon);
        WeaponCooldowns.Add(0f);
        WeaponClipAmmo.Add(startWeapon.ClipSize);
        WeaponReloadTimers.Add(0f);
        WeaponOrbitAngles.Add(0f);

        SpecialCooldown = 0f;
        SpecialMaxCooldown = Systems.SpecialAbilitySystem.GetMaxCooldown(character.HeroType);

        GenerateArena();

        ItemBonus = default;
        LevelBonus = default;
        Passives = new Passives();

        CurrentBiome = 1;
        CurrentWave = 0;
        Gold = 0;
        XP = 0;
        Level = 1;
        XPToNextLevel = 8;
        LevelUpPending = false;
        PendingLevelUps = 0;

        TotalEnemiesKilled = 0;
        TotalDamageDealt = 0;
        TotalTimeSurvived = 0;
        ComboCount = 0;
        ComboTimer = 0;
        BestCombo = 0;

        if (SandboxMode)
            SetupSandbox();
    }

    private void SetupSandbox()
    {
        // Spawn a boss near center-right, invincible and harmless
        var bossEnemy = GetInactiveEnemy();
        if (bossEnemy != null)
        {
            float arenaW = SandboxMode ? Constants.LogicalWidth : Constants.ArenaWidth;
            float arenaH = SandboxMode ? Constants.LogicalHeight : Constants.ArenaHeight;
            var bossPos = new Vector2(arenaW * 0.65f, arenaH * 0.5f);
            var bossDef = Data.EnemyDatabase.Enemies[0]; // use first enemy def as base
            bossEnemy.InitAsBoss(bossDef, bossPos, 1f);
            bossEnemy.DefIndex = 0;
            bossEnemy.BossSpriteType = CurrentBiome switch
            {
                2 => 1, 3 => 2, _ => 0,
            };
            bossEnemy.CurrentHP = 999999;
            bossEnemy.ContactDamage = 0;
            bossEnemy.MeleeAttackDamage = 0;
            bossEnemy.HasMeleeAttack = false;
            bossEnemy.Speed = 0; // stationary
        }

        // Place player at center-left
        Player.Position = new Vector2(
            (SandboxMode ? Constants.LogicalWidth : Constants.ArenaWidth) * 0.35f,
            (SandboxMode ? Constants.LogicalHeight : Constants.ArenaHeight) * 0.5f);
    }

    public void StartWave()
    {
        CurrentWave++;
        CurrentWaveConfig = WaveConfig.GetWave(CurrentBiome, CurrentWave);
        WaveTimer = CurrentWaveConfig.Duration;
        WaveActive = true;
        EnemiesSpawnedThisWave = 0;
        EnemiesKilledThisWave = 0;
        SpawnAccumulator = 0;
    }

    public Enemy? GetInactiveEnemy()
    {
        for (int i = 0; i < Enemies.Count; i++)
            if (!Enemies[i].Active) return Enemies[i];
        return null;
    }

    public Projectile? GetInactiveProjectile()
    {
        for (int i = 0; i < Projectiles.Count; i++)
            if (!Projectiles[i].Active) return Projectiles[i];
        return null;
    }

    public Projectile? GetInactiveEnemyProjectile()
    {
        for (int i = 0; i < EnemyProjectiles.Count; i++)
            if (!EnemyProjectiles[i].Active) return EnemyProjectiles[i];
        return null;
    }

    public XPOrb? GetInactiveXPOrb()
    {
        for (int i = 0; i < XPOrbs.Count; i++)
            if (!XPOrbs[i].Active) return XPOrbs[i];
        return null;
    }

    public DamageNumber? GetInactiveDamageNumber()
    {
        for (int i = 0; i < DamageNumbers.Count; i++)
            if (!DamageNumbers[i].Active) return DamageNumbers[i];
        return null;
    }

    public HealthPickup? GetInactiveHealthPickup()
    {
        for (int i = 0; i < HealthPickups.Count; i++)
            if (!HealthPickups[i].Active) return HealthPickups[i];
        return null;
    }

    public Mine? GetInactiveMine()
    {
        for (int i = 0; i < Mines.Count; i++)
            if (!Mines[i].Active) return Mines[i];
        return null;
    }

    public MeleeSwipe? GetInactiveMeleeSwipe()
    {
        for (int i = 0; i < MeleeSwipes.Count; i++)
            if (!MeleeSwipes[i].Active) return MeleeSwipes[i];
        return null;
    }

    public EnemyMine? GetInactiveEnemyMine()
    {
        for (int i = 0; i < EnemyMines.Count; i++)
            if (!EnemyMines[i].Active) return EnemyMines[i];
        return null;
    }

    public void SpawnExplosionVFX(Vector2 pos, float radius, int defIndex = -1)
    {
        for (int i = 0; i < ExplosionEffects.Length; i++)
        {
            if (!ExplosionEffects[i].Active)
            {
                ExplosionEffects[i].Init(pos, radius, 0.5f, defIndex);
                return;
            }
        }
    }

    public void HandleEnemyDeath(Enemy enemy)
    {
        enemy.StartDeath();
        TotalEnemiesKilled++;
        EnemiesKilledThisWave++;

        // Vampiric: heal on any kill
        if (Passives.VampiricHeal > 0)
        {
            Player.CurrentHP = Math.Min(
                Player.CurrentHP + Passives.VampiricHeal,
                Player.ComputedStats.MaxHP);
            var healNum = GetInactiveDamageNumber();
            healNum?.Init(Player.Position, $"+{Passives.VampiricHeal}", Color.Green);
        }

        // Combo tracking
        if (ComboTimer > 0)
            ComboCount++;
        else
            ComboCount = 1;
        ComboTimer = ComboWindow;
        if (ComboCount > BestCombo) BestCombo = ComboCount;

        // Explosive Kills: small AOE blast on every kill
        if (Passives.ExplosiveKills)
        {
            const float blastRadius = 40f;
            const int blastDamage = 8;
            for (int j = 0; j < Enemies.Count; j++)
            {
                var other = Enemies[j];
                if (!other.Active || other.IsDying || other == enemy) continue;
                if (Vector2.Distance(enemy.Position, other.Position) < blastRadius)
                {
                    other.CurrentHP -= blastDamage;
                    other.FlashTimer = 0.1f;
                    if (other.CurrentHP <= 0 && !other.IsDying)
                        HandleEnemyDeath(other);
                }
            }
            SpawnExplosionVFX(enemy.Position, blastRadius * 0.6f);
        }

        // Adrenaline Rush: track kill streak
        if (Passives.AdrenalineWindow > 0)
        {
            if (Passives.AdrenalineTimer > 0)
                Passives.AdrenalineKills++;
            else
                Passives.AdrenalineKills = 1;
            Passives.AdrenalineTimer = Passives.AdrenalineWindow;

            // Trigger burst on 3+ kills in window
            if (Passives.AdrenalineKills >= 3)
            {
                Passives.AdrenalineActive = 3f; // 3 seconds of attack speed boost
                Passives.AdrenalineKills = 0;
            }
        }

        // Combo multiplier: 1.0x at 1 kill, scales up with kills
        // 5 kills = 1.25x, 10 kills = 1.5x, 20 kills = 2.0x
        float comboMult = 1f + Math.Min(ComboCount - 1, 20) * 0.05f;

        // Kamikaze enemies explode on death (smaller than fuse explosion)
        if (enemy.IsKamikaze && enemy.FuseTimer > 0)
        {
            // Death explosion — 60% radius and damage of normal
            float radius = enemy.ExplosionRadius * 0.6f;
            int damage = (int)(enemy.ExplosionDamage * 0.6f);

            // Damage nearby enemies
            for (int j = 0; j < Enemies.Count; j++)
            {
                var other = Enemies[j];
                if (!other.Active || other.IsDying || other == enemy) continue;
                float d = Vector2.Distance(enemy.Position, other.Position);
                if (d < radius)
                {
                    int edm = Math.Max(1, (int)(damage * 0.5f));
                    other.CurrentHP -= edm;
                    other.FlashTimer = 0.1f;
                    if (other.IsKamikaze && other.FuseTimer > 0.2f)
                    {
                        other.FuseArmed = true;
                        other.FuseTimer = 0.2f;
                    }
                    if (other.CurrentHP <= 0 && !other.IsDying)
                        HandleEnemyDeath(other);
                }
            }

            SpawnExplosionVFX(enemy.Position, radius);
            RequestScreenShake(0.12f, 2f);
            Assets.PlaySoundVariant("explosion", 0.3f);
        }

        // Big Bug death burst — nest splits into small bugs
        if (enemy.DefIndex == 7 && !enemy.IsBoss)
        {
            int burstCount = 2 + (CurrentWave >= 7 ? 1 : 0); // 3 in later waves
            float scaleFactor = Constants.WaveStatScale(CurrentBiome, CurrentWave);
            for (int b = 0; b < burstCount; b++)
            {
                var bug = GetInactiveEnemy();
                if (bug == null) break;
                float angle = (MathF.PI * 2f / burstCount) * b + Random.Shared.NextSingle() * 0.5f;
                Vector2 spawnPos = enemy.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 15f;
                bug.Init(Data.EnemyDatabase.Enemies[1], spawnPos, scaleFactor); // Small Bug
                bug.DefIndex = 1;
                // Scatter outward from death position
                bug.KnockbackVelocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150f;
                bug.KnockbackTimer = 0.2f;
            }
        }

        // Loot enemies burst extra drops in a ring
        if (enemy.IsLootEnemy)
        {
            // Scatter XP orbs in a burst
            int orbCount = 6;
            for (int o = 0; o < orbCount; o++)
            {
                var orb = GetInactiveXPOrb();
                if (orb == null) break;
                float angle = o * (MathF.PI * 2f / orbCount);
                var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                orb.Init(enemy.Position + offset, Math.Max(1, (int)(enemy.XPValue * comboMult / orbCount)));
            }
            Gold += (int)(enemy.GoldValue * comboMult);

            // Always drop a health pickup
            var hp = GetInactiveHealthPickup();
            hp?.Init(enemy.Position, 15);
            return;
        }

        // XP orbs (with combo multiplier)
        int xpValue = (int)(enemy.XPValue * comboMult);
        int normalOrbCount = 1 + xpValue / 3;
        for (int o = 0; o < normalOrbCount; o++)
        {
            var orb = GetInactiveXPOrb();
            orb?.Init(enemy.Position, Math.Max(1, xpValue / normalOrbCount));
        }
        Gold += (int)(enemy.GoldValue * comboMult);

        // Armed enemies have a 30% chance to drop a health pickup
        if (enemy.IsArmed && Random.Shared.NextSingle() < 0.30f)
        {
            var pickup = GetInactiveHealthPickup();
            pickup?.Init(enemy.Position, 10);
        }
    }

    public int ActiveEnemyCount()
    {
        int count = 0;
        for (int i = 0; i < Enemies.Count; i++)
            if (Enemies[i].Active) count++;
        return count;
    }

    public void AddXP(int amount)
    {
        XP += (int)(amount * Player.ComputedStats.XPMultiplier);
        while (XP >= XPToNextLevel)
        {
            XP -= XPToNextLevel;
            Level++;
            XPToNextLevel = 8 + Level * 4 + Level * Level / 2;
            PendingLevelUps++;
            LevelUpPending = true;
        }
    }

    private void GenerateArena()
    {
        Obstacles.Clear();
        Barrels.Clear();
        TerrainZones.Clear();
        GroundScatterProps.Clear();

        // Sandbox: tiny empty arena, no obstacles or zones
        if (SandboxMode) return;

        var rng = Random.Shared;
        float centerX = Constants.ArenaWidth / 2f;
        float centerY = Constants.ArenaHeight / 2f;
        float safeRadius = 80f;

        bool useStranded = Assets.HasStrandedTerrain;
        int[] biomeObs = AssetManager.GetBiomeObstacles(CurrentBiome);
        int[] biomeScatter = AssetManager.GetBiomeScatter(CurrentBiome);

        // --- Pattern-based placement (2-4 formations) ---
        int patternCount = rng.Next(1, 3);
        for (int p = 0; p < patternCount; p++)
        {
            // Pick a random center for this formation, away from spawn
            Vector2 patCenter = Vector2.Zero;
            bool found = false;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                patCenter = new Vector2(
                    rng.NextSingle() * (Constants.ArenaWidth - 200) + 100,
                    rng.NextSingle() * (Constants.ArenaHeight - 200) + 100);
                if (Vector2.Distance(patCenter, new Vector2(centerX, centerY)) < safeRadius + 60f)
                    continue;
                found = true;
                break;
            }
            if (!found) continue;

            float rotation = rng.NextSingle() * MathF.PI * 2f;
            int pattern = CurrentBiome switch
            {
                3 => rng.Next(4), // temple gets shrine pattern
                _ => rng.Next(3), // waste/swamp: grove, corridor, outpost
            };

            switch (pattern)
            {
                case 0: // Grove — 3-4 obstacles clustered with scatter ring
                    PlaceGrovePattern(rng, patCenter, rotation, biomeObs, biomeScatter, useStranded);
                    break;
                case 1: // Corridor — parallel obstacles creating a lane
                    PlaceCorridorPattern(rng, patCenter, rotation, biomeObs, useStranded);
                    break;
                case 2: // Outpost — obstacle + barrels nearby
                    PlaceOutpostPattern(rng, patCenter, rotation, biomeObs, useStranded);
                    break;
                case 3: // Shrine — ring of obstacles with open center (temple)
                    PlaceShrinePattern(rng, patCenter, rotation, biomeObs, biomeScatter, useStranded);
                    break;
            }
        }

        // --- Fill remaining with random singles (up to target) ---
        int targetObstacles = rng.Next(6, 10);
        int remaining = targetObstacles - Obstacles.Count;
        for (int i = 0; i < remaining; i++)
        {
            TryPlaceRandomObstacle(rng, centerX, centerY, safeRadius, biomeObs, useStranded);
        }

        // --- Ground scatter (biome-filtered) ---
        if (useStranded && Assets.GroundScatterTextures.Length > 0 && biomeScatter.Length > 0)
        {
            int scatterCount = rng.Next(20, 35);
            for (int i = 0; i < scatterCount; i++)
            {
                float x = rng.NextSingle() * Constants.ArenaWidth;
                float y = rng.NextSingle() * Constants.ArenaHeight;
                GroundScatterProps.Add(new GroundScatter
                {
                    Position = new Vector2(x, y),
                    TextureIndex = biomeScatter[rng.Next(biomeScatter.Length)],
                    FlipH = rng.NextSingle() < 0.5f,
                });
            }
        }

        // Large accent props — waste/swamp only (swords/poles don't fit temple)
        if (useStranded && Assets.LargeScatterTextures.Length > 0 && CurrentBiome != 3)
        {
            int accentCount = rng.Next(3, 7);
            for (int i = 0; i < accentCount; i++)
            {
                float x = rng.NextSingle() * (Constants.ArenaWidth - 40) + 20;
                float y = rng.NextSingle() * (Constants.ArenaHeight - 40) + 20;
                GroundScatterProps.Add(new GroundScatter
                {
                    Position = new Vector2(x, y),
                    TextureIndex = rng.Next(Assets.LargeScatterTextures.Length),
                    FlipH = rng.NextSingle() < 0.5f,
                    IsLarge = true,
                });
            }
        }

        // --- Barrels (4-8) ---
        int barrelCount = rng.Next(3, 6);
        for (int i = 0; i < barrelCount; i++)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float x = rng.NextSingle() * (Constants.ArenaWidth - 80) + 40;
                float y = rng.NextSingle() * (Constants.ArenaHeight - 80) + 40;
                var pos = new Vector2(x, y);

                if (Vector2.Distance(pos, new Vector2(centerX, centerY)) < safeRadius + 12f)
                    continue;

                bool overlaps = false;
                foreach (var obs in Obstacles)
                {
                    if (Vector2.Distance(pos, obs.Position) < 12f + obs.Radius + 8f)
                    { overlaps = true; break; }
                }
                if (!overlaps)
                    foreach (var other in Barrels)
                        if (Vector2.Distance(pos, other.Position) < 24f)
                        { overlaps = true; break; }
                if (overlaps) continue;

                var type = CurrentBiome switch
                {
                    2 => rng.NextSingle() < 0.3f ? BarrelType.Explosive : BarrelType.Toxic, // swamp: more toxic
                    _ => rng.NextSingle() < 0.55f ? BarrelType.Explosive : BarrelType.Toxic,
                };
                int hp = type == BarrelType.Explosive ? 3 : 5;
                Barrels.Add(new Barrel
                {
                    Position = pos, Radius = 8f, Type = type,
                    CurrentHP = hp, MaxHP = hp, Active = true,
                });
                break;
            }
        }

        // --- Terrain zones (biome-specific mix) ---
        int zoneCount = rng.Next(2, 4);
        for (int i = 0; i < zoneCount; i++)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float x = rng.NextSingle() * (Constants.ArenaWidth - 160) + 80;
                float y = rng.NextSingle() * (Constants.ArenaHeight - 160) + 80;
                float radius = 40f + rng.NextSingle() * 30f;

                if (Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY)) < safeRadius)
                    continue;

                // Biome-specific zone types
                TerrainType type = CurrentBiome switch
                {
                    2 => rng.NextSingle() < 0.5f       // swamp: ooze pools + oasis bogs
                        ? TerrainType.Ooze : TerrainType.Oasis,
                    3 => rng.NextSingle() < 0.7f        // temple: mostly sand (stone dust), rare oasis
                        ? TerrainType.Sand : TerrainType.Oasis,
                    _ => rng.NextSingle() < 0.65f        // waste: sand + oasis (original)
                        ? TerrainType.Sand : TerrainType.Oasis,
                };

                var zone = new TerrainZone
                {
                    Position = new Vector2(x, y),
                    Radius = radius,
                    Type = type,
                    Active = true,
                };

                // Swamp ooze zones are permanent but weaker than barrel ooze
                if (type == TerrainType.Ooze && CurrentBiome == 2)
                {
                    zone.DamagePerSecond = 5f;
                    zone.Duration = 0; // permanent
                }

                TerrainZones.Add(zone);
                break;
            }
        }

        // Decorative patches
        int decoSet = rng.Next(3);
        int decoCount = rng.Next(2, 5);
        for (int i = 0; i < decoCount; i++)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float x = rng.NextSingle() * (Constants.ArenaWidth - 160) + 80;
                float y = rng.NextSingle() * (Constants.ArenaHeight - 160) + 80;
                float radius = 30f + rng.NextSingle() * 35f;

                if (Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY)) < safeRadius)
                    continue;

                TerrainZones.Add(new TerrainZone
                {
                    Position = new Vector2(x, y),
                    Radius = radius,
                    Type = TerrainType.Decorative,
                    DecoTileSet = decoSet,
                    Active = true,
                });
                break;
            }
        }
    }

    // --- Arena pattern helpers ---

    private bool TryPlaceObstacle(Vector2 pos, float safeRadius, int texIdx, bool useStranded)
    {
        float centerX = Constants.ArenaWidth / 2f;
        float centerY = Constants.ArenaHeight / 2f;
        float radius = useStranded && texIdx < AssetManager.ObstacleRadii.Length
            ? AssetManager.ObstacleRadii[texIdx] : 7f;

        if (pos.X < 40 || pos.X > Constants.ArenaWidth - 40 ||
            pos.Y < 40 || pos.Y > Constants.ArenaHeight - 40)
            return false;
        if (Vector2.Distance(pos, new Vector2(centerX, centerY)) < safeRadius + radius)
            return false;

        foreach (var other in Obstacles)
            if (Vector2.Distance(pos, other.Position) < radius + other.Radius + 15f)
                return false;

        if (useStranded)
        {
            Obstacles.Add(new Obstacle
            {
                Position = pos, Radius = radius,
                TextureIndex = texIdx, UseStranded = true, Active = true,
            });
        }
        else
        {
            int[] treeSprites = { 3 * 18 + 3, 3 * 18 + 8 };
            Obstacles.Add(new Obstacle
            {
                Position = pos, Radius = radius,
                SpriteIndex = treeSprites[Random.Shared.Next(treeSprites.Length)], Active = true,
            });
        }
        return true;
    }

    private void TryPlaceRandomObstacle(Random rng, float centerX, float centerY,
        float safeRadius, int[] biomeObs, bool useStranded)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = rng.NextSingle() * (Constants.ArenaWidth - 80) + 40;
            float y = rng.NextSingle() * (Constants.ArenaHeight - 80) + 40;
            int texIdx = useStranded ? biomeObs[rng.Next(biomeObs.Length)] : 0;
            if (TryPlaceObstacle(new Vector2(x, y), safeRadius, texIdx, useStranded))
                break;
        }
    }

    /// <summary>Grove: 3-4 obstacles clustered tightly with dense scatter ring.</summary>
    private void PlaceGrovePattern(Random rng, Vector2 center, float rotation,
        int[] biomeObs, int[] biomeScatter, bool useStranded)
    {
        int count = rng.Next(3, 5);
        float spread = 35f + rng.NextSingle() * 15f;
        for (int i = 0; i < count; i++)
        {
            float angle = rotation + (MathF.PI * 2f / count) * i + (rng.NextSingle() - 0.5f) * 0.5f;
            float dist = spread * (0.6f + rng.NextSingle() * 0.4f);
            Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
            int texIdx = useStranded ? biomeObs[rng.Next(biomeObs.Length)] : 0;
            TryPlaceObstacle(pos, 80f, texIdx, useStranded);
        }

        // Dense scatter ring around the grove
        if (useStranded && biomeScatter.Length > 0)
        {
            int scatterCount = rng.Next(6, 12);
            for (int i = 0; i < scatterCount; i++)
            {
                float angle = rng.NextSingle() * MathF.PI * 2f;
                float dist = rng.NextSingle() * (spread + 20f);
                GroundScatterProps.Add(new GroundScatter
                {
                    Position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist,
                    TextureIndex = biomeScatter[rng.Next(biomeScatter.Length)],
                    FlipH = rng.NextSingle() < 0.5f,
                });
            }
        }
    }

    /// <summary>Corridor: 4-6 obstacles in two parallel rows creating a lane.</summary>
    private void PlaceCorridorPattern(Random rng, Vector2 center, float rotation,
        int[] biomeObs, bool useStranded)
    {
        int pairsCount = rng.Next(2, 4);
        float spacing = 50f + rng.NextSingle() * 20f;
        float width = 40f + rng.NextSingle() * 15f;
        Vector2 dir = new(MathF.Cos(rotation), MathF.Sin(rotation));
        Vector2 perp = new(-dir.Y, dir.X);

        for (int i = 0; i < pairsCount; i++)
        {
            float offset = (i - (pairsCount - 1) * 0.5f) * spacing;
            Vector2 rowCenter = center + dir * offset;
            int texIdx = useStranded ? biomeObs[rng.Next(biomeObs.Length)] : 0;
            TryPlaceObstacle(rowCenter + perp * width * 0.5f, 80f, texIdx, useStranded);
            texIdx = useStranded ? biomeObs[rng.Next(biomeObs.Length)] : 0;
            TryPlaceObstacle(rowCenter - perp * width * 0.5f, 80f, texIdx, useStranded);
        }
    }

    /// <summary>Outpost: 1-2 obstacles with 2-3 barrels nearby.</summary>
    private void PlaceOutpostPattern(Random rng, Vector2 center, float rotation,
        int[] biomeObs, bool useStranded)
    {
        int texIdx = useStranded ? biomeObs[rng.Next(biomeObs.Length)] : 0;
        TryPlaceObstacle(center, 80f, texIdx, useStranded);

        // Second obstacle nearby
        if (rng.NextSingle() < 0.5f)
        {
            float angle = rotation + MathF.PI * 0.7f;
            texIdx = useStranded ? biomeObs[rng.Next(biomeObs.Length)] : 0;
            TryPlaceObstacle(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 35f,
                80f, texIdx, useStranded);
        }

        // Barrels clustered near the obstacle
        for (int b = 0; b < rng.Next(2, 4); b++)
        {
            float angle = rotation + b * MathF.PI * 0.6f + rng.NextSingle() * 0.4f;
            Vector2 bPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (25f + rng.NextSingle() * 15f);
            if (bPos.X < 40 || bPos.X > Constants.ArenaWidth - 40 ||
                bPos.Y < 40 || bPos.Y > Constants.ArenaHeight - 40) continue;

            bool overlaps = false;
            foreach (var obs in Obstacles)
                if (Vector2.Distance(bPos, obs.Position) < obs.Radius + 12f)
                { overlaps = true; break; }
            foreach (var other in Barrels)
                if (Vector2.Distance(bPos, other.Position) < 20f)
                { overlaps = true; break; }
            if (overlaps) continue;

            var type = rng.NextSingle() < 0.55f ? BarrelType.Explosive : BarrelType.Toxic;
            int hp = type == BarrelType.Explosive ? 3 : 5;
            Barrels.Add(new Barrel
            {
                Position = bPos, Radius = 8f, Type = type,
                CurrentHP = hp, MaxHP = hp, Active = true,
            });
        }
    }

    /// <summary>Shrine: 3-5 obstacles in a ring with open center and scatter. Temple-flavored.</summary>
    private void PlaceShrinePattern(Random rng, Vector2 center, float rotation,
        int[] biomeObs, int[] biomeScatter, bool useStranded)
    {
        int count = rng.Next(3, 6);
        float ringRadius = 40f + rng.NextSingle() * 20f;
        for (int i = 0; i < count; i++)
        {
            float angle = rotation + (MathF.PI * 2f / count) * i;
            Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;
            int texIdx = useStranded ? biomeObs[rng.Next(biomeObs.Length)] : 0;
            TryPlaceObstacle(pos, 80f, texIdx, useStranded);
        }

        // Scatter inside the shrine ring
        if (useStranded && biomeScatter.Length > 0)
        {
            int scatterCount = rng.Next(4, 8);
            for (int i = 0; i < scatterCount; i++)
            {
                float angle = rng.NextSingle() * MathF.PI * 2f;
                float dist = rng.NextSingle() * ringRadius * 0.7f;
                GroundScatterProps.Add(new GroundScatter
                {
                    Position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist,
                    TextureIndex = biomeScatter[rng.Next(biomeScatter.Length)],
                    FlipH = rng.NextSingle() < 0.5f,
                });
            }
        }
    }

    public void RecomputePlayerStats()
    {
        ItemBonus = default;
        foreach (var item in OwnedItems)
            ItemBonus = ItemBonus + item.StatModifier;
        Player.RecomputeStats(ItemBonus, LevelBonus + MetaBonus);
    }
}
