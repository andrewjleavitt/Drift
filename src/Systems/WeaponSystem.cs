using System.Numerics;
using CloneTato.Core;
using CloneTato.Data;
using CloneTato.Entities;
using Raylib_cs;

namespace CloneTato.Systems;

public static class WeaponSystem
{
    public const float WeaponOrbitRadius = 18f; // distance from player center
    private const float OrbitBobSpeed = 2.5f;   // idle bob frequency
    private const float OrbitBobAmount = 1.5f;  // idle bob amplitude in pixels

    // Fan spread angles per weapon count (radians) — symmetric around aim
    // 1 weapon: centered. 2: ±15°. 3: -20°/0°/+20°. 4: ±12°/±32°
    private static readonly float[][] FanOffsets =
    [
        [],                                                              // 0 weapons (unused)
        [0f],                                                            // 1 weapon
        [-0.26f, 0.26f],                                                 // 2 weapons (~15°)
        [-0.35f, 0f, 0.35f],                                             // 3 weapons (~20°)
        [-0.56f, -0.21f, 0.21f, 0.56f],                                  // 4 weapons (~12°/32°)
    ];

    private static float GetAttackSpeedMultiplier(GameState state)
    {
        var player = state.Player;
        float mult = player.ComputedStats.AttackSpeedMultiplier;
        if (player.DashBuffTimer > 0 && player.ComputedStats.PostDashAttackSpeed > 0)
            mult *= (1f + player.ComputedStats.PostDashAttackSpeed);
        // Adrenaline Rush: attack speed burst from kill streak
        if (state.Passives.AdrenalineActive > 0)
            mult *= (1f + state.Passives.AdrenalineBoost);
        return mult;
    }

    public static void Update(float dt, GameState state)
    {
        var player = state.Player;
        Vector2 mouseWorld = state.MouseWorldPosition;

        // Aim direction from player to mouse
        Vector2 aimDir = mouseWorld - player.Position;
        float aimDist = aimDir.Length();
        if (aimDist > 1f) aimDir /= aimDist;
        else aimDir = new Vector2(1, 0);
        float aimAngle = MathF.Atan2(aimDir.Y, aimDir.X);

        // Fan weapons out around aim direction — each weapon gets its own target angle
        int weaponCount = state.EquippedWeapons.Count;
        int fanIdx = Math.Clamp(weaponCount, 0, FanOffsets.Length - 1);
        var offsets = FanOffsets[fanIdx];
        for (int w = 0; w < weaponCount; w++)
        {
            float targetAngle = aimAngle + (w < offsets.Length ? offsets[w] : 0f);
            float current = state.WeaponOrbitAngles[w];
            float diff = NormalizeAngle(targetAngle - current);
            state.WeaponOrbitAngles[w] = current + diff * Math.Min(1f, 10f * dt);
        }

        // Tick all cooldowns and reload timers
        for (int w = 0; w < weaponCount; w++)
        {
            if (state.WeaponCooldowns[w] > 0)
                state.WeaponCooldowns[w] -= dt;

            // Reload timer
            if (state.WeaponReloadTimers[w] > 0)
            {
                state.WeaponReloadTimers[w] -= dt * state.Player.ComputedStats.ReloadSpeedMultiplier;
                if (state.WeaponReloadTimers[w] <= 0)
                {
                    // Reload complete — refill clip
                    state.WeaponClipAmmo[w] = state.EquippedWeapons[w].ClipSize;
                }
            }
        }

        // Update mines
        UpdateMines(dt, state);

        // Update melee swipe visuals
        for (int i = 0; i < state.MeleeSwipes.Count; i++)
            if (state.MeleeSwipes[i].Active)
                state.MeleeSwipes[i].Update(dt);

        // Fire all weapons — no primary/secondary distinction, all auto-fire
        for (int w = 0; w < weaponCount; w++)
        {
            if (state.WeaponCooldowns[w] > 0) continue;
            if (state.WeaponReloadTimers[w] > 0) continue; // reloading

            var weapon = state.EquippedWeapons[w];

            // Check clip (0 = unlimited, e.g. melee)
            if (weapon.ClipSize > 0 && state.WeaponClipAmmo[w] <= 0)
            {
                // Start reload
                state.WeaponReloadTimers[w] = weapon.ReloadTime;
                continue;
            }

            if (weapon.Def.Type == WeaponType.Melee)
            {
                // Melee: manual swing on fire press (Hades-style)
                UpdateMeleeWeapon(state, w, weapon, aimDir, aimAngle);
            }
            else if (weapon.Def.IsMine || weapon.Def.IsLockOn || weapon.Def.ExplosionRadius > 0)
            {
                // Tactical weapons (grenades, mines, rockets, missiles): auto-fire on cooldown
                // when enemies exist, otherwise require manual trigger
                if (state.IsFiring || state.IsSecondaryFiring || state.IsSecondaryDown)
                    FireSecondaryWeapon(state, w, weapon, aimDir, aimAngle);
            }
            else
            {
                // Ranged guns: auto-fire via auto-aim or manual fire
                UpdatePrimaryWeapon(state, w, weapon, aimDir, aimAngle);
            }
        }

        // Tick special ability cooldown
        if (state.SpecialCooldown > 0)
            state.SpecialCooldown -= dt;
    }

    private static void UpdatePrimaryWeapon(GameState state, int w, WeaponInstance weapon,
        Vector2 aimDir, float aimAngle)
    {
        // Player-controlled firing based on FireMode
        switch (weapon.Def.FireMode)
        {
            case FireMode.HoldAuto:
                // Hold to fire continuously
                if (!state.IsFiring) return;
                break;
            case FireMode.TapSemi:
                // Each press = one shot
                if (!state.IsFirePressed) return;
                break;
            default:
                if (!state.IsFiring) return;
                break;
        }

        var player = state.Player;

        // Fire from player center toward mouse cursor
        Vector2 fireDir = state.MouseWorldPosition - player.Position;
        float fireDist = fireDir.Length();
        if (fireDist > 1f) fireDir /= fireDist;
        else fireDir = aimDir;

        float cooldown = 1f / (weapon.FireRate * GetAttackSpeedMultiplier(state));
        state.WeaponCooldowns[w] = cooldown;

        // Consume ammo
        if (weapon.ClipSize > 0)
            state.WeaponClipAmmo[w]--;

        if (weapon.Def.IsLockOn)
        {
            FireLockOnVolley(state, w, weapon, aimAngle, player.Position);
            return;
        }

        if (weapon.BurstCount > 1)
        {
            float baseAngle = MathF.Atan2(fireDir.Y, fireDir.X);
            for (int b = 0; b < weapon.BurstCount; b++)
            {
                float spreadAngle = baseAngle + (Random.Shared.NextSingle() - 0.5f) * weapon.Def.Spread * 2f;
                Vector2 spreadDir = new(MathF.Cos(spreadAngle), MathF.Sin(spreadAngle));
                FireProjectile(state, player.Position, spreadDir, weapon);
            }
        }
        else
        {
            if (weapon.Def.Spread > 0)
            {
                float baseAngle = MathF.Atan2(fireDir.Y, fireDir.X);
                float spreadAngle = baseAngle + (Random.Shared.NextSingle() - 0.5f) * weapon.Def.Spread;
                fireDir = new Vector2(MathF.Cos(spreadAngle), MathF.Sin(spreadAngle));
            }
            FireProjectile(state, player.Position, fireDir, weapon);
        }

        state.Assets.PlaySoundVariant("shoot", 0.25f);
    }

    private static void FireSecondaryWeapon(GameState state, int w, WeaponInstance weapon,
        Vector2 aimDir, float aimAngle)
    {
        var player = state.Player;

        // Use CooldownTime if set, otherwise fall back to 1/FireRate
        float cooldown = weapon.Def.CooldownTime > 0
            ? weapon.Def.CooldownTime
            : 1f / (weapon.FireRate * GetAttackSpeedMultiplier(state));
        // Overclock: reduce secondary cooldown
        if (state.Passives.OverclockMult > 0)
            cooldown *= (1f - state.Passives.OverclockMult);
        state.WeaponCooldowns[w] = cooldown;

        if (weapon.Def.IsMine)
        {
            // Place mine at player's feet
            var mine = state.GetInactiveMine();
            if (mine != null)
            {
                mine.Init(player.Position, (int)weapon.Damage, weapon.ExplosionRadius,
                    weapon.Def.MineProximity, weapon.Def.SpriteIndex);
            }
            state.Assets.PlaySoundVariant("select", 0.3f);
        }
        else if (weapon.Def.IsLockOn)
        {
            FireLockOnVolley(state, w, weapon, aimAngle, player.Position);
        }
        else
        {
            // Fire grenade/rocket/bomb from player toward mouse
            Vector2 fireDir = state.MouseWorldPosition - player.Position;
            float dist = fireDir.Length();
            if (dist > 1f) fireDir /= dist;
            else fireDir = aimDir;

            FireProjectile(state, player.Position, fireDir, weapon);
            state.Assets.PlaySoundVariant("shoot", 0.4f);
        }
    }

    private static void UpdateMeleeWeapon(GameState state, int w, WeaponInstance weapon,
        Vector2 aimDir, float aimAngle)
    {
        var player = state.Player;

        // BladeDancer: manual attack on RT press (Hades-style)
        // Swing toward aim direction, can whiff if nothing is in range
        // Dash strike: RT during dash = lunging slash with bonus damage/range
        bool isDashStrike = player.IsDashing && state.IsFirePressed;
        if (!isDashStrike && !state.IsFirePressed) return;

        float cooldown = 1f / (weapon.FireRate * GetAttackSpeedMultiplier(state));
        // Dash strike has reduced cooldown for snappy chaining
        if (isDashStrike) cooldown *= 0.6f;
        state.WeaponCooldowns[w] = cooldown;

        // Dash strike swings in dash direction; normal attack swings toward aim
        float swingAngle = isDashStrike
            ? MathF.Atan2(player.DashDirection.Y, player.DashDirection.X)
            : aimAngle;
        // Dash strike: wider arc, more range, more damage
        float effectiveArc = isDashStrike ? weapon.MeleeArc * 1.4f : weapon.MeleeArc;
        float effectiveRange = isDashStrike ? weapon.Range * 1.5f : weapon.Range;
        float halfArc = effectiveArc / 2f;
        float dashDmgMult = isDashStrike ? 1.5f : 1f;
        int damage = (int)(weapon.Damage * player.ComputedStats.DamageMultiplier * dashDmgMult);

        // Damage all enemies in the arc
        int hitCount = 0;
        for (int e = 0; e < state.Enemies.Count; e++)
        {
            var enemy = state.Enemies[e];
            if (!enemy.Active || enemy.IsDying || enemy.IsBurrowed) continue;

            float dist = Vector2.Distance(player.Position, enemy.Position);
            if (dist > effectiveRange + enemy.Radius) continue;

            // Check if enemy is within the arc
            float angleToEnemy = MathF.Atan2(
                enemy.Position.Y - player.Position.Y,
                enemy.Position.X - player.Position.X);
            float angleDiff = MathF.Abs(NormalizeAngle(angleToEnemy - swingAngle));
            if (angleDiff > halfArc) continue;

            // Hit!
            bool crit = Random.Shared.NextSingle() < player.ComputedStats.CritChance;
            int finalDamage = crit ? (int)(damage * player.ComputedStats.CritDamage) : damage;
            finalDamage = enemy.ApplyFrontalReduction(finalDamage, player.Position);

            enemy.CurrentHP -= finalDamage;
            enemy.FlashTimer = 0.1f;
            state.TotalDamageDealt += finalDamage;
            hitCount++;

            // Knockback away from player — resist based on enemy type
            Vector2 knockDir = dist > 1f
                ? Vector2.Normalize(enemy.Position - player.Position)
                : new Vector2(MathF.Cos(swingAngle), MathF.Sin(swingAngle));
            float knockResist = enemy.IsBoss ? 0.3f
                : enemy.Behavior == Entities.EnemyBehavior.Tank ? 0.4f
                : enemy.Behavior == Entities.EnemyBehavior.FastChase ? 1.4f
                : 1f;
            enemy.KnockbackVelocity = knockDir * Constants.KnockbackForce * 1.5f * knockResist;
            enemy.KnockbackTimer = Constants.KnockbackDuration;

            var dmgNum = state.GetInactiveDamageNumber();
            dmgNum?.Init(enemy.Position, finalDamage.ToString(),
                crit ? Color.Yellow : Color.White);

            // Enemy death — hitstop on kill
            if (enemy.CurrentHP <= 0 && !enemy.IsDying)
            {
                state.HandleEnemyDeath(enemy);
                state.RequestHitstop(0.05f);  // ~3 frames freeze on melee kill
                state.RequestScreenShake(0.12f, 2.5f);
            }
        }

        // Trigger player melee attack animation
        player.MeleeAnimTimer = isDashStrike ? 0.25f : 0.35f; // dash strike is faster
        player.MeleeAttackCount++;

        // Melee lunge — dash strike gets a stronger forward burst
        float lungeForce = isDashStrike ? 140f : 80f;
        Vector2 lungeDir = isDashStrike ? player.DashDirection : aimDir;
        player.Velocity += lungeDir * lungeForce;

        // End dash on dash strike (committed attack, can't keep rolling)
        if (isDashStrike)
        {
            player.DashTimer = 0f;
            player.IsDashing = false;
        }

        // Spawn swipe visual — dash strike is bigger and gold-tinted
        Color swipeColor;
        if (isDashStrike)
            swipeColor = hitCount > 0
                ? new Color((byte)255, (byte)200, (byte)80, (byte)255)   // gold flash
                : new Color((byte)200, (byte)180, (byte)100, (byte)150);
        else
            swipeColor = hitCount > 0
                ? weapon.Def.Name switch
                {
                    "Knife" => new Color((byte)200, (byte)230, (byte)255, (byte)255),
                    "Spear" => new Color((byte)255, (byte)220, (byte)150, (byte)255),
                    "Hammer" => new Color((byte)255, (byte)180, (byte)100, (byte)255),
                    "Cleaver" => new Color((byte)255, (byte)150, (byte)150, (byte)255),
                    _ => Color.White,
                }
                : new Color((byte)200, (byte)200, (byte)200, (byte)150);
        var swipe = state.GetInactiveMeleeSwipe();
        swipe?.Init(player.Position, swingAngle, weapon.MeleeArc, weapon.Range, swipeColor);

        if (hitCount > 0)
        {
            state.Assets.PlaySoundVariant("hurt", 0.2f);
            // Screen shake scales with multi-hit (cleaving through a crowd feels powerful)
            float shakeIntensity = 1.0f + (hitCount - 1) * 0.3f;
            state.RequestScreenShake(0.06f + hitCount * 0.02f, shakeIntensity);
            // Hitstop on multi-hit for extra impact
            if (hitCount >= 3)
                state.RequestHitstop(0.03f);
        }
        else
            state.Assets.PlaySoundVariant("move", 0.15f);
    }

    private static void FireLockOnVolley(GameState state, int w, WeaponInstance weapon,
        float aimAngle, Vector2 weaponPos)
    {
        var player = state.Player;

        // Find all active, non-dying enemies in range
        var targets = new List<int>();
        for (int e = 0; e < state.Enemies.Count; e++)
        {
            if (!state.Enemies[e].Active || state.Enemies[e].IsDying) continue;
            if (Vector2.Distance(player.Position, state.Enemies[e].Position) < weapon.Range * 1.5f)
                targets.Add(e);
        }
        if (targets.Count == 0) return;

        int missileCount = weapon.MissileCount;

        // Distribute missiles among targets
        int[] missilesPerTarget = new int[targets.Count];
        for (int i = 0; i < missileCount; i++)
            missilesPerTarget[i % targets.Count]++;

        for (int t = 0; t < targets.Count; t++)
        {
            for (int m = 0; m < missilesPerTarget[t]; m++)
            {
                var proj = state.GetInactiveProjectile();
                if (proj == null) break;

                float spreadAngle = aimAngle + (Random.Shared.NextSingle() - 0.5f) * MathF.PI * 0.8f;
                Vector2 launchDir = new(MathF.Cos(spreadAngle), MathF.Sin(spreadAngle));

                float lifetime = weapon.Range / weapon.ProjectileSpeed * 2f;
                int damage = (int)weapon.Damage;
                proj.Init(weaponPos, launchDir * weapon.ProjectileSpeed, damage, lifetime,
                    0, weapon.Def.ProjectileColor, weapon.ExplosionRadius);
                proj.IsHoming = true;
                proj.TargetEnemyIndex = targets[t];
                proj.TurnRate = weapon.Def.MissileTurnRate;
            }
        }

        state.Assets.PlaySoundVariant("shoot", 0.4f);
    }

    public static void UpdateHomingProjectiles(float dt, GameState state)
    {
        for (int p = 0; p < state.Projectiles.Count; p++)
        {
            var proj = state.Projectiles[p];
            if (!proj.Active || !proj.IsHoming) continue;

            int ti = proj.TargetEnemyIndex;

            // Check if target is still valid
            if (ti < 0 || ti >= state.Enemies.Count ||
                !state.Enemies[ti].Active || state.Enemies[ti].IsDying)
            {
                // Find nearest alive enemy as new target
                float bestDist = float.MaxValue;
                int bestIdx = -1;
                for (int e = 0; e < state.Enemies.Count; e++)
                {
                    if (!state.Enemies[e].Active || state.Enemies[e].IsDying) continue;
                    float d = Vector2.Distance(proj.Position, state.Enemies[e].Position);
                    if (d < bestDist) { bestDist = d; bestIdx = e; }
                }
                proj.TargetEnemyIndex = bestIdx;
                ti = bestIdx;
            }

            if (ti < 0) continue; // no enemies left, fly straight

            // Steer toward target
            Vector2 toTarget = state.Enemies[ti].Position - proj.Position;
            float targetAngle = MathF.Atan2(toTarget.Y, toTarget.X);
            float currentAngle = MathF.Atan2(proj.Velocity.Y, proj.Velocity.X);
            float angleDiff = NormalizeAngle(targetAngle - currentAngle);

            float maxTurn = proj.TurnRate * dt;
            float turn = Math.Clamp(angleDiff, -maxTurn, maxTurn);
            float newAngle = currentAngle + turn;

            float speed = proj.Velocity.Length();
            // Accelerate slightly over time
            speed = Math.Min(speed + 80f * dt, speed * 1.5f);
            proj.Velocity = new Vector2(MathF.Cos(newAngle), MathF.Sin(newAngle)) * speed;
        }
    }

    private static void UpdateMines(float dt, GameState state)
    {
        for (int m = 0; m < state.Mines.Count; m++)
        {
            var mine = state.Mines[m];
            if (!mine.Active) continue;
            mine.Update(dt);
            if (mine.ArmTimer > 0) continue;

            // Check enemy proximity
            for (int e = 0; e < state.Enemies.Count; e++)
            {
                var enemy = state.Enemies[e];
                if (!enemy.Active || enemy.IsDying) continue;
                if (Vector2.Distance(mine.Position, enemy.Position) < mine.ProximityRadius + enemy.Radius)
                {
                    // Explode!
                    ExplodeAt(state, mine.Position, mine.Damage, mine.ExplosionRadius);
                    mine.Active = false;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// AOE explosion damage at a world position.
    /// </summary>
    public static void ExplodeAt(GameState state, Vector2 pos, int damage, float radius)
    {
        var player = state.Player;
        float dmgMult = player.ComputedStats.DamageMultiplier;

        for (int e = 0; e < state.Enemies.Count; e++)
        {
            var enemy = state.Enemies[e];
            if (!enemy.Active || enemy.IsDying || enemy.IsBurrowed) continue;
            float dist = Vector2.Distance(pos, enemy.Position);
            if (dist > radius + enemy.Radius) continue;

            // Damage falls off with distance
            float falloff = 1f - (dist / (radius + enemy.Radius)) * 0.5f;
            int finalDamage = (int)(damage * dmgMult * falloff);

            bool crit = Random.Shared.NextSingle() < player.ComputedStats.CritChance;
            if (crit) finalDamage = (int)(finalDamage * player.ComputedStats.CritDamage);
            finalDamage = enemy.ApplyFrontalReduction(finalDamage, pos);

            enemy.CurrentHP -= finalDamage;
            enemy.FlashTimer = 0.1f;
            state.TotalDamageDealt += finalDamage;

            // Knockback from explosion center — resist based on enemy type
            Vector2 knockDir = dist > 1f
                ? Vector2.Normalize(enemy.Position - pos)
                : new Vector2(Random.Shared.NextSingle() - 0.5f, Random.Shared.NextSingle() - 0.5f);
            float exKnockResist = enemy.IsBoss ? 0.3f
                : enemy.Behavior == Entities.EnemyBehavior.Tank ? 0.4f
                : 1f;
            enemy.KnockbackVelocity = knockDir * Constants.KnockbackForce * 2f * exKnockResist;
            enemy.KnockbackTimer = Constants.KnockbackDuration * 1.5f;

            var dmgNum = state.GetInactiveDamageNumber();
            dmgNum?.Init(enemy.Position, finalDamage.ToString(),
                crit ? Color.Yellow : Color.Orange);

            if (enemy.CurrentHP <= 0 && !enemy.IsDying)
                state.HandleEnemyDeath(enemy);
        }

        state.Assets.PlaySoundVariant("explosion", 0.5f);
    }

    public static Vector2 GetWeaponWorldPosition(GameState state, int weaponIndex)
    {
        float angle = state.WeaponOrbitAngles[weaponIndex];
        // Gentle idle bob — each weapon offset slightly so they don't bob in sync
        float time = (float)Raylib.GetTime();
        float bob = MathF.Sin(time * OrbitBobSpeed + weaponIndex * 1.8f) * OrbitBobAmount;
        float radius = WeaponOrbitRadius + bob;
        return state.Player.Position + new Vector2(
            MathF.Cos(angle) * radius,
            MathF.Sin(angle) * radius);
    }

    public static float GetWeaponDrawAngle(GameState state, int weaponIndex)
    {
        return state.WeaponOrbitAngles[weaponIndex];
    }

    private static void FireProjectile(GameState state, Vector2 origin, Vector2 dir, WeaponInstance weapon)
    {
        var proj = state.GetInactiveProjectile();
        if (proj == null) return;

        float lifetime = weapon.Range / weapon.ProjectileSpeed;
        int damage = (int)weapon.Damage;
        proj.Init(origin, dir * weapon.ProjectileSpeed, damage, lifetime,
            weapon.PierceCount, weapon.Def.ProjectileColor, weapon.ExplosionRadius);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.PI * 2f;
        while (angle < -MathF.PI) angle += MathF.PI * 2f;
        return angle;
    }
}
