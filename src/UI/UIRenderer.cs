using System.Numerics;
using CloneTato.Assets;
using CloneTato.Core;
using CloneTato.Entities;
using Raylib_cs;

namespace CloneTato.UI;

public static class UIRenderer
{
    private const int HPBarFrameW = 51;
    private const int HPBarFrameH = 9;
    private const int HPBarFrameCount = 11;

    public static void DrawHUD(GameState state)
    {
        // Health bar (top left)
        int barX = 10, barY = 8;
        float hpPct = (float)state.Player.CurrentHP / state.Player.ComputedStats.MaxHP;
        hpPct = Math.Clamp(hpPct, 0f, 1f);

        if (state.Assets.HPBarSheet.Id != 0)
        {
            // STRANDED HP bar — pick frame from spritesheet (frame 0 = full, frame 10 = empty)
            int frameIdx = (int)((1f - hpPct) * (HPBarFrameCount - 1));
            frameIdx = Math.Clamp(frameIdx, 0, HPBarFrameCount - 1);
            var src = new Rectangle(0, frameIdx * HPBarFrameH, HPBarFrameW, HPBarFrameH);
            // Scale up 2x for visibility
            var dest = new Rectangle(barX, barY, HPBarFrameW * 2, HPBarFrameH * 2);
            Raylib.DrawTexturePro(state.Assets.HPBarSheet, src, dest, Vector2.Zero, 0f, Color.White);
            DrawTextSmall($"{state.Player.CurrentHP}/{state.Player.ComputedStats.MaxHP}",
                barX + HPBarFrameW * 2 + 4, barY + 4, Color.White);
        }
        else
        {
            // Legacy rectangle HP bar
            Raylib.DrawRectangle(barX - 1, barY - 1, 102, 12, Color.Black);
            Raylib.DrawRectangle(barX, barY, 100, 10, Color.DarkGray);
            Color hpColor = hpPct > 0.5f ? Color.Green : hpPct > 0.25f ? Color.Orange : Color.Red;
            Raylib.DrawRectangle(barX, barY, (int)(100 * hpPct), 10, hpColor);
            DrawTextSmall($"{state.Player.CurrentHP}/{state.Player.ComputedStats.MaxHP}",
                barX + 104, barY, Color.White);
        }

        // Compute bar dimensions based on which HP bar style is active
        int barW = state.Assets.HPBarSheet.Id != 0 ? HPBarFrameW * 2 : 100;
        int barH = state.Assets.HPBarSheet.Id != 0 ? HPBarFrameH * 2 : 10;

        // XP bar (below health)
        int xpY = barY + barH + 3;
        float xpPct = (float)state.XP / state.XPToNextLevel;
        Raylib.DrawRectangle(barX - 1, xpY - 1, barW + 2, 5, Color.Black);
        Raylib.DrawRectangle(barX, xpY, barW, 3, Color.DarkGray);
        Raylib.DrawRectangle(barX, xpY, (int)(barW * xpPct), 3, Color.SkyBlue);
        DrawTextSmall($"Lv {state.Level}", barX + barW + 4, xpY - 1, Color.SkyBlue);

        // Wave info (top center) with biome name
        string biomeName = Screens.BiomeSelectScreen.GetBiomeName(state.CurrentBiome);
        string waveText = $"{biomeName} - Wave {state.CurrentWave}/{Constants.WavesPerBiome}";
        int textW = waveText.Length * 5;
        DrawTextSmall(waveText, Constants.LogicalWidth / 2 - textW / 2, 4, Color.White);

        if (state.WaveActive && state.WaveTimer > 0)
        {
            string timerText = $"{state.WaveTimer:F0}s";
            int timerW = timerText.Length * 5;
            DrawTextSmall(timerText, Constants.LogicalWidth / 2 - timerW / 2, 14, Color.Gold);
        }

        // Gold (top right) — use coin icon if available
        string goldText = $"${state.Gold}";
        if (state.Assets.CoinIcon.Id != 0)
        {
            int coinX = Constants.LogicalWidth - goldText.Length * 5 - 22;
            var coinTex = state.Assets.CoinIcon;
            Raylib.DrawTexturePro(coinTex,
                new Rectangle(0, 0, coinTex.Width, coinTex.Height),
                new Rectangle(coinX, 2, 12, 12),
                Vector2.Zero, 0f, Color.White);
            DrawTextSmall(goldText, coinX + 14, 4, Color.Gold);
        }
        else
        {
            DrawTextSmall(goldText, Constants.LogicalWidth - goldText.Length * 5 - 8, 4, Color.Gold);
        }

        // Enemies remaining
        int remaining = state.ActiveEnemyCount();
        if (state.WaveActive && remaining > 0)
        {
            string enemyText = $"{remaining} enemies";
            DrawTextSmall(enemyText, Constants.LogicalWidth - enemyText.Length * 5 - 8, 14, Color.Red);
        }

        // Combo indicator (top right, below enemies)
        if (state.ComboCount >= 3)
        {
            float comboMult = 1f + Math.Min(state.ComboCount - 1, 20) * 0.05f;
            string comboText = $"x{state.ComboCount} ({comboMult:F2}x)";
            Color comboColor = state.ComboCount >= 15 ? Color.Gold
                : state.ComboCount >= 8 ? Color.Orange : Color.White;
            DrawTextSmall(comboText, Constants.LogicalWidth - comboText.Length * 5 - 8, 24, comboColor);
        }

        // Dash cooldown indicator (below XP bar)
        int dashY = xpY + 6;
        float dashCD = Math.Max(0.15f, Constants.DashCooldown - state.Player.ComputedStats.DashCooldownReduction);
        if (state.Player.IsDashing)
        {
            DrawTextSmall("DASH", barX, dashY - 1, Color.White);
        }
        else if (state.Player.DashCooldownTimer > 0)
        {
            float dashPct = 1f - state.Player.DashCooldownTimer / dashCD;
            Raylib.DrawRectangle(barX - 1, dashY - 1, 42, 5, Color.Black);
            Raylib.DrawRectangle(barX, dashY, 40, 3, Color.DarkGray);
            Raylib.DrawRectangle(barX, dashY, (int)(40 * dashPct), 3, Color.Purple);
            DrawTextSmall("DASH", barX + 44, dashY - 1, Color.Gray);
        }
        else
        {
            DrawTextSmall("DASH [SPACE]", barX, dashY - 1, Color.Purple);
        }

        // Post-dash buff indicator
        if (state.Player.DashBuffTimer > 0)
        {
            int buffY = dashY + 8;
            float buffPct = state.Player.DashBuffTimer / Player.DashBuffDuration;
            Raylib.DrawRectangle(barX - 1, buffY - 1, 42, 5, Color.Black);
            Raylib.DrawRectangle(barX, buffY, 40, 3, Color.DarkGray);
            Raylib.DrawRectangle(barX, buffY, (int)(40 * buffPct), 3, Color.Gold);
            DrawTextSmall("BUFF", barX + 44, buffY - 1, Color.Gold);
        }

        // Weapon slots (bottom left): up to 4 weapons + Special
        int weaponY = Constants.LogicalHeight - 28;
        for (int i = 0; i < state.EquippedWeapons.Count; i++)
        {
            var weapon = state.EquippedWeapons[i];
            int wx = 4 + i * 30;

            bool isReloading = state.WeaponReloadTimers[i] > 0;
            bool isOnCooldown = state.WeaponCooldowns[i] > 0 && weapon.Def.CooldownTime > 0;

            // Background
            Raylib.DrawRectangle(wx - 1, weaponY - 1, 28, 28, new Color(0, 0, 0, 150));

            // Cooldown darkening for secondaries
            if (isOnCooldown)
            {
                float cooldownPct = state.WeaponCooldowns[i] / (weapon.Def.CooldownTime > 0 ? weapon.Def.CooldownTime : 1f);
                int darkH = (int)(26 * cooldownPct);
                Raylib.DrawRectangle(wx, weaponY + (26 - darkH), 26, darkH, new Color(0, 0, 0, 180));
            }

            Color weapTint = isReloading ? new Color((byte)100, (byte)100, (byte)100, (byte)255) : Color.White;
            state.Assets.Weapons.Draw(weapon.Def.SpriteIndex, wx + 2, weaponY + 2, weapTint);

            // Weapon number label above
            Raylib.DrawText($"{i + 1}", wx + 10, weaponY - 9, 6, Color.LightGray);

            // Reload bar overlay
            if (isReloading)
            {
                float reloadPct = 1f - state.WeaponReloadTimers[i] / weapon.ReloadTime;
                Raylib.DrawRectangle(wx, weaponY + 24, (int)(26 * reloadPct), 2, Color.SkyBlue);
            }

            // Clip ammo counter (for all weapons with clips)
            if (weapon.ClipSize > 0)
            {
                string ammoText = isReloading ? "R" : $"{state.WeaponClipAmmo[i]}";
                Color ammoColor = isReloading ? Color.SkyBlue :
                    state.WeaponClipAmmo[i] <= weapon.ClipSize / 4 ? Color.Red : Color.White;
                Raylib.DrawText(ammoText, wx + 1, weaponY + 18, 6, ammoColor);
            }

            // Upgrade level indicator
            if (weapon.UpgradeLevel > 0)
            {
                string lvl = $"+{weapon.UpgradeLevel}";
                Raylib.DrawText(lvl, wx + 18, weaponY + 18, 6, Color.Gold);
            }
        }

        // Special ability cooldown indicator
        {
            int sx = 4 + state.EquippedWeapons.Count * 30;
            Raylib.DrawRectangle(sx - 1, weaponY - 1, 28, 28, new Color(0, 0, 0, 150));
            Raylib.DrawText("SPL", sx, weaponY - 9, 6, Color.Magenta);

            if (state.SpecialCooldown > 0 && state.SpecialMaxCooldown > 0)
            {
                float pct = state.SpecialCooldown / state.SpecialMaxCooldown;
                int darkH = (int)(26 * pct);
                Raylib.DrawRectangle(sx, weaponY + (26 - darkH), 26, darkH, new Color(0, 0, 0, 180));
                Raylib.DrawText("Q", sx + 9, weaponY + 8, 10, new Color(255, 255, 255, 100));
            }
            else
            {
                Raylib.DrawText("Q", sx + 9, weaponY + 8, 10, Color.Magenta);
            }
        }

        // Active passives (bottom right, stacking upward)
        DrawPassives(state);
    }

    private static void DrawPassives(GameState state)
    {
        var p = state.Passives;
        int x = Constants.LogicalWidth - 8; // right-aligned
        int y = Constants.LogicalHeight - 10;
        const int lineH = 9;

        // Build list of active passives (bottom to top)
        if (p.AdrenalineWindow > 0)
        {
            Color c = p.AdrenalineActive > 0 ? Color.Gold : Color.DarkGray;
            string text = p.AdrenalineActive > 0
                ? $"ADRENALINE {p.AdrenalineActive:F1}s"
                : "ADRENALINE";
            DrawTextSmall(text, x - text.Length * 5, y, c);
            y -= lineH;
        }
        if (p.OverclockMult > 0)
        {
            string text = $"OVERCLOCK {(int)(p.OverclockMult * 100)}%";
            DrawTextSmall(text, x - text.Length * 5, y, Color.SkyBlue);
            y -= lineH;
        }
        if (p.ExplosiveKills)
        {
            string text = "EXPLOSIVE";
            DrawTextSmall(text, x - text.Length * 5, y, Color.Orange);
            y -= lineH;
        }
        if (p.ThornsDamage > 0)
        {
            string text = $"THORNS x{p.ThornsDamage}";
            DrawTextSmall(text, x - text.Length * 5, y,
                new Color((byte)180, (byte)100, (byte)255, (byte)255));
            y -= lineH;
        }
        if (p.VampiricHeal > 0)
        {
            string text = $"VAMPIRIC +{p.VampiricHeal}";
            DrawTextSmall(text, x - text.Length * 5, y, Color.Green);
            y -= lineH;
        }
        if (p.Ricochet > 0)
        {
            string text = $"RICOCHET x{p.Ricochet}";
            DrawTextSmall(text, x - text.Length * 5, y, Color.Yellow);
            y -= lineH;
        }
    }

    public static void DrawTextSmall(string text, int x, int y, Color color)
    {
        Raylib.DrawText(text, x, y, 8, color);
    }

    public static void DrawTextMedium(string text, int x, int y, Color color)
    {
        Raylib.DrawText(text, x, y, 12, color);
    }

    public static void DrawTextLarge(string text, int x, int y, Color color)
    {
        Raylib.DrawText(text, x, y, 20, color);
    }

    public static bool DrawButton(string text, int x, int y, int w, int h, Color bgColor, bool selected = false)
    {
        var mouse = Display.ScreenToLogical(Raylib.GetMousePosition());

        bool hovered = mouse.X >= x && mouse.X <= x + w && mouse.Y >= y && mouse.Y <= y + h;
        bool clicked = hovered && Raylib.IsMouseButtonPressed(MouseButton.Left);

        bool highlighted = hovered || selected;
        Color bg = highlighted
            ? new Color((byte)Math.Min(255, bgColor.R + 30), (byte)Math.Min(255, bgColor.G + 30),
                (byte)Math.Min(255, bgColor.B + 30), bgColor.A)
            : bgColor;
        Raylib.DrawRectangle(x, y, w, h, bg);

        Color borderColor = selected ? Color.Gold : Color.White;
        Raylib.DrawRectangleLines(x, y, w, h, borderColor);

        int textW = text.Length * 5; // approximate
        DrawTextSmall(text, x + w / 2 - textW / 2, y + h / 2 - 4, Color.White);

        return clicked;
    }

    public static Vector2 GetLogicalMouse()
    {
        return Display.ScreenToLogical(Raylib.GetMousePosition());
    }
}
