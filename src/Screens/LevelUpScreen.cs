using CloneTato.Core;
using CloneTato.Data;
using CloneTato.UI;
using Raylib_cs;

namespace CloneTato.Screens;

public class LevelUpScreen
{
    private readonly Upgrade[] _choices = new Upgrade[3];
    private bool _initialized;
    private int _selected;

    private record Upgrade(string Name, string Description, Stats Bonus, Action<Passives>? PassiveEffect = null);

    private static readonly Upgrade[] AllUpgrades =
    {
        // --- Stat upgrades ---
        new("Max HP +25", "+25 Maximum HP", new Stats { MaxHP = 25 }),
        new("Speed +15", "+15 Move Speed", new Stats { MoveSpeed = 15f }),
        new("Damage +12%", "+12% Damage", new Stats { DamageMultiplier = 0.12f }),
        new("Atk Speed +15%", "+15% Attack Speed", new Stats { AttackSpeedMultiplier = 0.15f }),
        new("Armor +3", "+3 Armor", new Stats { Armor = 3 }),
        new("Dodge +6%", "+6% Dodge Chance", new Stats { DodgeChance = 0.06f }),
        new("Crit +6%", "+6% Crit Chance", new Stats { CritChance = 0.06f }),
        new("Pickup +20", "+20 Pickup Range", new Stats { PickupRange = 20f }),
        new("XP +12%", "+12% XP Gain", new Stats { XPMultiplier = 0.12f }),

        // --- Dash upgrades ---
        new("Dash Cooldown", "-0.15s Dash Cooldown", new Stats { DashCooldownReduction = 0.15f }),
        new("Dash: Atk Speed", "35% atk speed after dash", new Stats { PostDashAttackSpeed = 0.35f }),
        new("Dash: Move", "30% move speed after dash", new Stats { PostDashMoveSpeed = 0.30f }),
        new("Dash: Invuln", "0.6s invuln after dash", new Stats { PostDashInvuln = 0.6f }),

        // --- Trade-off upgrades (high risk, high reward — the fun stuff) ---
        new("Glass Cannon", "+40% DMG, -20 HP", new Stats { DamageMultiplier = 0.40f, MaxHP = -20 }),
        new("Berserker", "+25% DMG, +20% Spd, -3 Armor", new Stats { DamageMultiplier = 0.25f, AttackSpeedMultiplier = 0.20f, Armor = -3 }),
        new("Speedster", "+25 Speed, +8% Dodge", new Stats { MoveSpeed = 25f, DodgeChance = 0.08f }),
        new("Critical Mass", "+12% Crit, +0.5x Crit DMG", new Stats { CritChance = 0.12f, CritDamage = 0.50f }),
        new("XP Vacuum", "+80 Pickup Range", new Stats { PickupRange = 80f }),
        new("Thick Skin", "+50 HP, +5 Armor, -12 Spd", new Stats { MaxHP = 50, Armor = 5, MoveSpeed = -12f }),
        new("Bullet Time", "+30% Atk Spd, -10 HP", new Stats { AttackSpeedMultiplier = 0.30f, MaxHP = -10 }),
        new("Reload Frenzy", "+40% Reload Spd", new Stats { ReloadSpeedMultiplier = 0.40f }),

        // --- Mechanical passives ---
        new("Ricochet", "Shots bounce to 1 enemy", default, p => p.Ricochet += 1),
        new("Vampiric", "Melee kills heal 3 HP", default, p => p.VampiricHeal += 3),
        new("Thorns", "Hit attackers for 20 dmg", default, p => p.ThornsDamage += 20),
        new("Explosive Kills", "Kills trigger AOE blast", default, p => p.ExplosiveKills = true),
        new("Overclock", "Secondary CD -30%", default, p => p.OverclockMult += 0.30f),
        new("Adrenaline Rush", "3 kills in 2s = atk burst", default, p => { p.AdrenalineWindow = 2f; p.AdrenalineBoost += 0.45f; }),
    };

    public void Update(float dt, GameState state, GameStateManager manager)
    {
        if (!_initialized)
        {
            GenerateChoices();
            _initialized = true;
            _selected = 0;
        }

        int hDir = InputHelper.GetMenuHorizontal();
        if (hDir != 0)
            _selected = (_selected + hDir + 3) % 3;

        if (InputHelper.IsConfirmPressed())
            ChooseUpgrade(state, manager, _selected);
    }

    private void ChooseUpgrade(GameState state, GameStateManager manager, int index)
    {
        var choice = _choices[index];

        // Apply stat bonus
        state.LevelBonus = state.LevelBonus + choice.Bonus;
        state.RecomputePlayerStats();

        // Apply passive effect
        if (choice.PassiveEffect != null)
            choice.PassiveEffect(state.Passives);

        state.PendingLevelUps--;
        if (state.PendingLevelUps <= 0)
            state.LevelUpPending = false;
        _initialized = false;

        // Heal a bit on level up
        state.Player.CurrentHP = Math.Min(
            state.Player.CurrentHP + 5,
            state.Player.ComputedStats.MaxHP);

        state.Assets.PlaySoundVariant("select", 0.5f);

        if (!state.LevelUpPending)
            manager.TransitionTo(GameScreen.Playing);
    }

    private void GenerateChoices()
    {
        var indices = Enumerable.Range(0, AllUpgrades.Length).ToList();
        for (int i = 0; i < 3; i++)
        {
            int pick = Random.Shared.Next(indices.Count);
            _choices[i] = AllUpgrades[indices[pick]];
            indices.RemoveAt(pick);
        }
    }

    public void Draw(GameState state, GameStateManager manager)
    {
        // Semi-transparent overlay
        Raylib.DrawRectangle(0, 0, Constants.LogicalWidth, Constants.LogicalHeight, new Color(0, 0, 0, 180));

        string title = $"LEVEL UP! (Lv {state.Level})";
        int titleW = Raylib.MeasureText(title, 16);
        Raylib.DrawText(title, Constants.LogicalWidth / 2 - titleW / 2, 80, 16, Color.Gold);

        UIRenderer.DrawTextSmall("Choose an upgrade:", Constants.LogicalWidth / 2 - 40, 105, Color.White);

        int cardW = 150, cardH = 50;
        int totalW = 3 * (cardW + 12) - 12;
        int startX = Constants.LogicalWidth / 2 - totalW / 2;
        int cardY = 120;

        var mouse = Display.ScreenToLogical(Raylib.GetMousePosition());

        for (int i = 0; i < 3; i++)
        {
            int cx = startX + i * (cardW + 12);
            bool hovered = mouse.X >= cx && mouse.X <= cx + cardW && mouse.Y >= cardY && mouse.Y <= cardY + cardH;
            bool selected = _selected == i;

            if (hovered) _selected = i;

            // Mechanical passives get a distinct card color
            bool isMechanical = _choices[i].PassiveEffect != null;
            Color bg = (hovered || selected)
                ? (isMechanical ? new Color(40, 70, 80, 255) : new Color(80, 60, 40, 255))
                : (isMechanical ? new Color(25, 45, 50, 255) : new Color(50, 35, 25, 255));
            Color border = (hovered || selected) ? Color.Gold : Color.Gray;

            Raylib.DrawRectangle(cx, cardY, cardW, cardH, bg);
            Raylib.DrawRectangleLines(cx, cardY, cardW, cardH, border);

            Color nameColor = isMechanical ? new Color(100, 220, 255, 255) : Color.White;
            UIRenderer.DrawTextSmall(_choices[i].Name, cx + 6, cardY + 8, nameColor);
            UIRenderer.DrawTextSmall(_choices[i].Description, cx + 6, cardY + 22, Color.LightGray);

            if (hovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                ChooseUpgrade(state, manager, i);
        }

        string hint = InputHelper.GamepadAvailable
            ? "Left/Right select, A confirm"
            : "Left/Right select, Enter confirm";
        int hintW = hint.Length * 5;
        UIRenderer.DrawTextSmall(hint, Constants.LogicalWidth / 2 - hintW / 2, cardY + cardH + 10, Color.Gray);
    }
}
