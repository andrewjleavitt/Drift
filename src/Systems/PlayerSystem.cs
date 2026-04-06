using System.Numerics;
using CloneTato.Core;
using CloneTato.Entities;
using Raylib_cs;

namespace CloneTato.Systems;

public static class PlayerSystem
{
    public static void Update(float dt, GameState state)
    {
        var player = state.Player;

        // Sandbox: player is invincible
        if (state.SandboxMode)
            player.InvincibilityTimer = 1f;

        // Dash cooldown
        if (player.DashCooldownTimer > 0)
            player.DashCooldownTimer -= dt;

        // Post-dash buff timer
        if (player.DashBuffTimer > 0)
            player.DashBuffTimer -= dt;

        // Currently dashing — fully invincible, no damage
        if (player.IsDashing)
        {
            player.DashTimer -= dt;
            if (player.DashTimer <= 0)
            {
                player.IsDashing = false;
                float dashCD = Math.Max(0.15f, Constants.DashCooldown - player.ComputedStats.DashCooldownReduction);
                player.DashCooldownTimer = dashCD;

                // Post-dash buffs (only if player has earned them via upgrades)
                bool hasAnyDashBuff = player.ComputedStats.PostDashAttackSpeed > 0
                    || player.ComputedStats.PostDashMoveSpeed > 0
                    || player.ComputedStats.PostDashInvuln > 0;
                if (hasAnyDashBuff)
                    player.DashBuffTimer = Player.DashBuffDuration;

                // Post-dash invulnerability (from upgrade)
                if (player.ComputedStats.PostDashInvuln > 0)
                    player.InvincibilityTimer = player.ComputedStats.PostDashInvuln;
            }
            else
            {
                player.Velocity = player.DashDirection * (Constants.DashSpeed + player.ComputedStats.DashSpeedBonus);
                player.Position += player.Velocity * dt;

                // Clamp to arena + obstacle collision
                player.Position.X = Math.Clamp(player.Position.X, 12f, state.EffectiveArenaWidth - 12f);
                player.Position.Y = Math.Clamp(player.Position.Y, 12f, state.EffectiveArenaHeight - 12f);
                CollisionSystem.ResolveObstacleCollision(state, ref player.Position, player.Radius);

                // Stay invincible during entire dash — don't tick down InvincibilityTimer
                if (player.FlashTimer > 0) player.FlashTimer -= dt;
                state.TotalTimeSurvived += dt;
                return;
            }
        }

        // Input
        Vector2 input = InputHelper.GetMoveInput();

        // BladeDancer: aim comes from movement direction (Hades-style), not right stick
        if (player.HeroType == Data.HeroType.BladeDancer)
        {
            if (input.LengthSquared() > 0.1f)
                player.LastMoveDirection = Vector2.Normalize(input);
            // Override aim to follow facing/move direction
            state.MouseWorldPosition = player.Position + player.LastMoveDirection * 80f;
        }
        else if (InputHelper.GamepadAvailable)
        {
            // Gamepad aim: right stick sets aim, hold last direction when idle
            Vector2 gamepadAim = InputHelper.GetAimInput();
            if (gamepadAim.LengthSquared() > 0)
                player.LastMoveDirection = Vector2.Normalize(gamepadAim);
            state.MouseWorldPosition = player.Position + player.LastMoveDirection * 80f;
        }

        // Auto-aim: find nearest enemy and override aim + auto-fire (Brotato-style)
        // BladeDancer excluded — melee uses movement-based aim
        if (state.AutoAimEnabled && player.HeroType != Data.HeroType.BladeDancer)
        {
            float bestDist = float.MaxValue;
            int bestIdx = -1;

            // Use the longest-range equipped weapon as the auto-aim radius
            float autoAimRadius = 200f; // minimum fallback
            for (int w = 0; w < state.EquippedWeapons.Count; w++)
            {
                var wep = state.EquippedWeapons[w];
                if (wep.Range > autoAimRadius)
                    autoAimRadius = wep.Range;
            }
            autoAimRadius *= 1.2f; // acquire targets slightly before they enter weapon range

            for (int e = 0; e < state.Enemies.Count; e++)
            {
                var enemy = state.Enemies[e];
                if (!enemy.Active || enemy.IsDying || enemy.IsBurrowed) continue;
                float dist = Vector2.Distance(player.Position, enemy.Position);
                if (dist < bestDist && dist < autoAimRadius)
                {
                    bestDist = dist;
                    bestIdx = e;
                }
            }

            if (bestIdx >= 0)
            {
                // Override aim to point at nearest enemy
                state.MouseWorldPosition = state.Enemies[bestIdx].Position;
                // Auto-fire all weapons: primary, secondary, everything
                state.IsFiring = true;
                state.IsFirePressed = true;
                state.IsSecondaryFiring = true;
                state.IsSecondaryDown = true;
            }
            // When no enemies in range, fall back to mouse/stick aim (already set above)
        }

        // Dash initiation
        if (InputHelper.IsDashPressed() && player.DashCooldownTimer <= 0)
        {
            Vector2 dashDir = input.LengthSquared() > 0.1f ? Vector2.Normalize(input) : Vector2.Zero;
            // If not moving, dash toward aim direction
            if (dashDir == Vector2.Zero)
            {
                Vector2 toAim = state.MouseWorldPosition - player.Position;
                if (toAim.LengthSquared() > 1f)
                    dashDir = Vector2.Normalize(toAim);
            }
            if (dashDir != Vector2.Zero)
            {
                player.IsDashing = true;
                float dashDur = Constants.DashDuration + player.ComputedStats.DashDurationBonus;
                player.DashTimer = dashDur;
                player.DashDirection = dashDir;
                player.InvincibilityTimer = dashDur; // i-frames during dash
                return;
            }
        }

        // Face toward aim (or move direction for BladeDancer)
        Vector2 faceDir = player.HeroType == Data.HeroType.BladeDancer
            ? player.LastMoveDirection
            : state.MouseWorldPosition - player.Position;
        // BladeDancer uses unit vector so deadzone is smaller
        float facingDeadzone = player.HeroType == Data.HeroType.BladeDancer ? 0.1f : 8f;
        if (MathF.Abs(faceDir.X) > facingDeadzone)
            player.FacingLeft = faceDir.X < 0;

        // Post-dash move speed buff
        float moveSpeed = player.ComputedStats.MoveSpeed;
        if (player.DashBuffTimer > 0 && player.ComputedStats.PostDashMoveSpeed > 0)
            moveSpeed *= (1f + player.ComputedStats.PostDashMoveSpeed);

        // Terrain zone speed modifier
        float terrainMult = CollisionSystem.GetTerrainSpeedMultiplier(state, player.Position);
        moveSpeed *= terrainMult;

        // Apply knockback if active — overrides movement input
        if (player.KnockbackTimer > 0)
        {
            player.KnockbackTimer -= dt;
            player.Position += player.KnockbackVelocity * dt;
            player.KnockbackVelocity *= 0.85f; // friction decay
        }

        player.Velocity = input * moveSpeed;
        player.Position += player.Velocity * dt;

        // Clamp to arena + obstacle collision
        player.Position.X = Math.Clamp(player.Position.X, 12f, Constants.ArenaWidth - 12f);
        player.Position.Y = Math.Clamp(player.Position.Y, 12f, Constants.ArenaHeight - 12f);
        CollisionSystem.ResolveObstacleCollision(state, ref player.Position, player.Radius);

        // Terrain healing (oasis)
        float healRate = CollisionSystem.GetTerrainHealRate(state, player.Position);
        if (healRate > 0 && player.CurrentHP < player.ComputedStats.MaxHP)
        {
            player.CurrentHP = Math.Min(player.CurrentHP + (int)(healRate * dt + 0.5f),
                player.ComputedStats.MaxHP);
        }

        // Terrain damage (ooze)
        if (player.InvincibilityTimer <= 0)
        {
            float oozeDmg = CollisionSystem.GetTerrainDamageRate(state, player.Position);
            if (oozeDmg > 0)
            {
                int dmg = Math.Max(1, (int)(oozeDmg * dt + 0.5f) - player.ComputedStats.Armor);
                player.CurrentHP -= dmg;
                player.FlashTimer = 0.08f;
            }
        }

        // Timers
        if (player.InvincibilityTimer > 0) player.InvincibilityTimer -= dt;
        if (player.FlashTimer > 0) player.FlashTimer -= dt;
        if (player.MeleeAnimTimer > 0) player.MeleeAnimTimer -= dt;

        // Animation
        if (player.Velocity.LengthSquared() > 1f)
        {
            player.AnimTimer += dt;

            // Footstep sounds synced to walk cycle
            player.FootstepTimer -= dt;
            if (player.FootstepTimer <= 0)
            {
                state.Assets.PlaySoundVariant("move", 0.03f);
                player.FootstepTimer = 0.22f; // ~4.5 steps/sec — crisp, quick cadence
            }
        }
        else
        {
            player.FootstepTimer = 0f; // first step plays immediately on move start
        }

        state.TotalTimeSurvived += dt;

        // Combo timer decay
        if (state.ComboTimer > 0)
        {
            state.ComboTimer -= dt;
            if (state.ComboTimer <= 0)
                state.ComboCount = 0;
        }

        // Adrenaline Rush timers
        if (state.Passives.AdrenalineTimer > 0)
        {
            state.Passives.AdrenalineTimer -= dt;
            if (state.Passives.AdrenalineTimer <= 0)
                state.Passives.AdrenalineKills = 0;
        }
        if (state.Passives.AdrenalineActive > 0)
            state.Passives.AdrenalineActive -= dt;
    }
}
