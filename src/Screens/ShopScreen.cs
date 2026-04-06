using CloneTato.Core;
using CloneTato.Data;
using CloneTato.UI;
using Raylib_cs;

namespace CloneTato.Screens;

public class ShopScreen
{
    private List<object> _shopItems = new(); // WeaponDef or ItemDef
    private bool _initialized;
    private int _rerollCost = 5;

    // Gamepad navigation: 0=upgrades row, 1=items row, 2=buttons row
    private int _navRow;
    private int _navCol;


    public void Update(float dt, GameState state, GameStateManager manager)
    {
        if (!_initialized)
        {
            GenerateShopItems(state);
            _initialized = true;
            _rerollCost = 5 + state.CurrentWave;
            _navRow = 2;
            _navCol = 1; // default to Next Wave
        }

        // Gamepad/keyboard navigation
        int vDir = InputHelper.GetMenuVertical();
        if (vDir != 0)
            _navRow = Math.Clamp(_navRow + vDir, 0, 2);

        int hDir = InputHelper.GetMenuHorizontal();
        if (hDir != 0)
        {
            int maxCol = _navRow switch
            {
                0 => Math.Max(0, state.EquippedWeapons.Count - 1),
                1 => Math.Max(0, _shopItems.Count - 1),
                2 => 1, // reroll, next wave
                _ => 0,
            };
            _navCol = Math.Clamp(_navCol + hDir, 0, maxCol);
        }

        if (InputHelper.IsConfirmPressed())
            ActivateSelected(state, manager);

        // Sell weapon: S key or Backspace when on weapon row
        if (_navRow == 0 && (Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Backspace)))
            SellWeapon(state, _navCol);
    }

    private void ActivateSelected(GameState state, GameStateManager manager)
    {
        switch (_navRow)
        {
            case 0: // Upgrade or sell weapon
                if (_navCol < state.EquippedWeapons.Count)
                {
                    var weapon = state.EquippedWeapons[_navCol];
                    if (weapon.CanUpgrade && state.Gold >= weapon.UpgradeCost)
                    {
                        state.Gold -= weapon.UpgradeCost;
                        weapon.UpgradeLevel++;
                        state.Assets.PlaySoundVariant("select", 0.5f);
                    }
                }
                break;
            case 1: // Buy item
                if (_navCol < _shopItems.Count)
                    TryBuyItem(state, _navCol);
                break;
            case 2: // Buttons
                if (_navCol == 0) // Reroll
                {
                    if (state.Gold >= _rerollCost)
                    {
                        state.Gold -= _rerollCost;
                        _rerollCost += 3;
                        GenerateShopItems(state);
                        state.Assets.PlaySoundVariant("select", 0.5f);
                    }
                }
                else // Next wave
                {
                    NextWave(state, manager);
                }
                break;
        }
    }

    private void SellWeapon(GameState state, int index)
    {
        if (index < 0 || index >= state.EquippedWeapons.Count) return;
        if (state.EquippedWeapons.Count <= 1) return; // can't sell last weapon

        var weapon = state.EquippedWeapons[index];
        int sellPrice = weapon.Def.Cost / 2 + weapon.UpgradeLevel * 8; // half base + some for upgrades
        state.Gold += sellPrice;

        state.EquippedWeapons.RemoveAt(index);
        state.WeaponCooldowns.RemoveAt(index);
        state.WeaponClipAmmo.RemoveAt(index);
        state.WeaponReloadTimers.RemoveAt(index);
        state.WeaponOrbitAngles.RemoveAt(index);

        state.Assets.PlaySoundVariant("coin", 0.5f);
    }

    private void TryBuyItem(GameState state, int index)
    {
        if (index >= _shopItems.Count) return;

        if (_shopItems[index] is WeaponDef weapon)
        {
            if (state.Gold < weapon.Cost) return;
            if (state.EquippedWeapons.Count >= Constants.MaxWeaponSlots) return; // slots full — sell first

            state.Gold -= weapon.Cost;
            var newWeapon = new WeaponInstance(weapon);

            state.EquippedWeapons.Add(newWeapon);
            state.WeaponCooldowns.Add(0f);
            state.WeaponClipAmmo.Add(newWeapon.ClipSize);
            state.WeaponReloadTimers.Add(0f);
            state.WeaponOrbitAngles.Add(0f);

            _shopItems.RemoveAt(index);
            state.Assets.PlaySoundVariant("select", 0.5f);
        }
        else if (_shopItems[index] is ItemDef item)
        {
            if (state.Gold >= item.Cost)
            {
                state.Gold -= item.Cost;
                state.OwnedItems.Add(item);
                _shopItems.RemoveAt(index);
                state.RecomputePlayerStats();
                state.Assets.PlaySoundVariant("coin", 0.5f);
            }
        }
    }

    private void GenerateShopItems(GameState state)
    {
        _shopItems.Clear();
        int maxTier = 1 + state.CurrentWave / 5;
        bool isBladeDancer = state.Player.HeroType == HeroType.BladeDancer;

        var availableWeapons = WeaponDatabase.Weapons.Where(w =>
        {
            if (w.ShopTier > maxTier) return false;
            // Melee weapons only for BladeDancer
            if (w.Type == WeaponType.Melee && !isBladeDancer) return false;
            return true;
        }).ToList();

        var availableItems = ItemDatabase.Items.Where(i => i.ShopTier <= maxTier).ToList();

        Shuffle(availableWeapons);
        Shuffle(availableItems);

        // Offer 2-3 weapons (more variety with 4 slots to fill)
        int weaponOffers = Math.Min(3, availableWeapons.Count);
        for (int i = 0; i < weaponOffers; i++)
            _shopItems.Add(availableWeapons[i]);

        // Offer 1-2 items
        for (int i = 0; i < Math.Min(2, availableItems.Count); i++)
            _shopItems.Add(availableItems[i]);
    }

    private void NextWave(GameState state, GameStateManager manager)
    {
        _initialized = false;
        state.RecomputePlayerStats();
        // Heal 10% between waves
        state.Player.CurrentHP = Math.Min(
            state.Player.CurrentHP + state.Player.ComputedStats.MaxHP / 10,
            state.Player.ComputedStats.MaxHP);

        // Refill all clips between waves
        for (int i = 0; i < state.EquippedWeapons.Count; i++)
        {
            state.WeaponClipAmmo[i] = state.EquippedWeapons[i].ClipSize;
            state.WeaponReloadTimers[i] = 0f;
        }

        state.StartWave();
        manager.TransitionTo(GameScreen.Playing);
    }

    public void Draw(GameState state, GameStateManager manager)
    {
        Raylib.ClearBackground(new Color(35, 25, 20, 255));

        string title = $"SHOP - Wave {state.CurrentWave} Complete!";
        int titleW = Raylib.MeasureText(title, 16);
        Raylib.DrawText(title, Constants.LogicalWidth / 2 - titleW / 2, 8, 16, Color.Gold);

        UIRenderer.DrawTextMedium($"Gold: ${state.Gold}", Constants.LogicalWidth / 2 - 30, 28, Color.Gold);

        var mouse = Display.ScreenToLogical(Raylib.GetMousePosition());

        // === WEAPON UPGRADES + SELL (top section) ===
        if (state.EquippedWeapons.Count > 0)
        {
            string slotsLabel = $"WEAPONS ({state.EquippedWeapons.Count}/{Constants.MaxWeaponSlots})";
            UIRenderer.DrawTextSmall(slotsLabel, 20, 48, Color.Orange);

            int ugW = 120, ugH = 52;
            int ugY = 60;
            for (int i = 0; i < state.EquippedWeapons.Count; i++)
            {
                var weapon = state.EquippedWeapons[i];
                int ux = 20 + i * (ugW + 6);
                bool hovered = mouse.X >= ux && mouse.X <= ux + ugW && mouse.Y >= ugY && mouse.Y <= ugY + ugH;
                bool selected = _navRow == 0 && _navCol == i;

                if (hovered && Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    _navRow = 0;
                    _navCol = i;
                }

                Color bg = (hovered || selected) ? new Color(70, 55, 35, 255) : new Color(45, 32, 22, 255);
                Raylib.DrawRectangle(ux, ugY, ugW, ugH, bg);
                Color border = weapon.CanUpgrade
                    ? (selected ? Color.Gold : hovered ? Color.Orange : Color.Orange)
                    : Color.Gray;
                if (selected) border = Color.Gold;
                Raylib.DrawRectangleLines(ux, ugY, ugW, ugH, border);

                // Weapon icon
                state.Assets.Weapons.Draw(weapon.Def.SpriteIndex, ux + 2, ugY + 7, Color.White);

                // Name + level
                string lvlText = weapon.UpgradeLevel > 0 ? $" +{weapon.UpgradeLevel}" : "";
                UIRenderer.DrawTextSmall($"{weapon.Def.Name}{lvlText}", ux + 26, ugY + 3, Color.White);

                if (weapon.CanUpgrade)
                {
                    UIRenderer.DrawTextSmall(weapon.UpgradePreview(), ux + 26, ugY + 13, Color.LightGray);
                    UIRenderer.DrawTextSmall($"Upgrade: ${weapon.UpgradeCost}", ux + 26, ugY + 25,
                        state.Gold >= weapon.UpgradeCost ? Color.Green : Color.Red);

                    if (hovered && Raylib.IsMouseButtonPressed(MouseButton.Left) && state.Gold >= weapon.UpgradeCost)
                    {
                        state.Gold -= weapon.UpgradeCost;
                        weapon.UpgradeLevel++;
                        state.Assets.PlaySoundVariant("select", 0.5f);
                    }
                }
                else
                {
                    UIRenderer.DrawTextSmall("MAX LEVEL", ux + 26, ugY + 18, Color.Gold);
                }

                // Sell button (always visible if more than 1 weapon)
                if (state.EquippedWeapons.Count > 1)
                {
                    int sellPrice = weapon.Def.Cost / 2 + weapon.UpgradeLevel * 8;
                    int sellBtnY = ugY + ugH - 12;
                    int sellBtnW = ugW - 4;
                    bool sellHovered = mouse.X >= ux + 2 && mouse.X <= ux + 2 + sellBtnW
                        && mouse.Y >= sellBtnY && mouse.Y <= sellBtnY + 11;

                    Color sellBg = sellHovered ? new Color(120, 40, 40, 255) : new Color(80, 30, 30, 255);
                    Raylib.DrawRectangle(ux + 2, sellBtnY, sellBtnW, 11, sellBg);
                    UIRenderer.DrawTextSmall($"SELL ${sellPrice}", ux + 4, sellBtnY + 2,
                        sellHovered ? Color.White : Color.LightGray);

                    if (sellHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                        SellWeapon(state, i);

                    // Right-click to sell when hovered on the card
                    if (hovered && Raylib.IsMouseButtonPressed(MouseButton.Right))
                        SellWeapon(state, i);
                }
            }
        }

        // === BUY NEW WEAPONS & ITEMS (bottom section) ===
        bool slotsFull = state.EquippedWeapons.Count >= Constants.MaxWeaponSlots;
        UIRenderer.DrawTextSmall("BUY", 20, 122, Color.SkyBlue);
        if (slotsFull)
            UIRenderer.DrawTextSmall("(sell a weapon to make room)", 40, 122, new Color(255, 150, 150, 255));

        int cardW = 110, cardH = 72;
        int totalW = _shopItems.Count > 0 ? _shopItems.Count * (cardW + 6) - 6 : 0;
        int startX = Constants.LogicalWidth / 2 - totalW / 2;
        int cardY = 134;

        for (int i = 0; i < _shopItems.Count; i++)
        {
            int cx = startX + i * (cardW + 6);
            bool hovered = mouse.X >= cx && mouse.X <= cx + cardW && mouse.Y >= cardY && mouse.Y <= cardY + cardH;
            bool selected = _navRow == 1 && _navCol == i;

            if (hovered && Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                _navRow = 1;
                _navCol = i;
            }

            Color bg = (hovered || selected) ? new Color(70, 50, 35, 255) : new Color(50, 35, 25, 255);
            Raylib.DrawRectangle(cx, cardY, cardW, cardH, bg);
            Color cardBorder = selected ? Color.Gold : hovered ? Color.Gold : Color.Gray;
            Raylib.DrawRectangleLines(cx, cardY, cardW, cardH, cardBorder);

            if (_shopItems[i] is WeaponDef weapon)
            {
                // Weapon type label
                string typeLabel = weapon.Type == WeaponType.Melee ? "[MELEE]"
                    : weapon.ExplosionRadius > 0 || weapon.IsMine || weapon.IsLockOn ? "[TACTICAL]"
                    : "[GUN]";
                Color typeColor = weapon.Type == WeaponType.Melee ? Color.Orange
                    : weapon.ExplosionRadius > 0 || weapon.IsMine || weapon.IsLockOn ? new Color(255, 180, 80, 255)
                    : Color.SkyBlue;
                UIRenderer.DrawTextSmall(typeLabel, cx + 4, cardY + 3, typeColor);

                state.Assets.Weapons.Draw(weapon.SpriteIndex, cx + cardW / 2 - 12, cardY + 12, Color.White);
                UIRenderer.DrawTextSmall(weapon.Name, cx + 4, cardY + 36, Color.White);
                UIRenderer.DrawTextSmall($"DMG:{weapon.BaseDamage:F0} SPD:{weapon.FireRate:F1}", cx + 4, cardY + 46, Color.LightGray);

                bool canAfford = state.Gold >= weapon.Cost;
                bool canBuy = canAfford && !slotsFull;
                UIRenderer.DrawTextSmall($"${weapon.Cost}", cx + 4, cardY + 58,
                    canBuy ? Color.Green : Color.Red);
                if (slotsFull && canAfford)
                    UIRenderer.DrawTextSmall("FULL", cx + cardW - 26, cardY + 58, Color.Red);

                if (hovered && Raylib.IsMouseButtonPressed(MouseButton.Left) && canBuy)
                {
                    _navRow = 1;
                    _navCol = i;
                    TryBuyItem(state, i);
                }
            }
            else if (_shopItems[i] is ItemDef item)
            {
                state.Assets.Weapons.Draw(item.SpriteIndex, cx + cardW / 2 - 12, cardY + 3, Color.White);
                UIRenderer.DrawTextSmall(item.Name, cx + 4, cardY + 28, Color.White);
                UIRenderer.DrawTextSmall(item.Description, cx + 4, cardY + 38, Color.LightGray);
                UIRenderer.DrawTextSmall($"${item.Cost}", cx + 4, cardY + 52,
                    state.Gold >= item.Cost ? Color.Green : Color.Red);

                if (hovered && Raylib.IsMouseButtonPressed(MouseButton.Left) && state.Gold >= item.Cost)
                {
                    _navRow = 1;
                    _navCol = i;
                    TryBuyItem(state, i);
                }
            }
        }

        // Reroll button
        bool rerollSelected = _navRow == 2 && _navCol == 0;
        if (UIRenderer.DrawButton($"REROLL (${_rerollCost})",
            Constants.LogicalWidth / 2 - 60, cardY + cardH + 10, 120, 20, new Color(80, 60, 40, 255), rerollSelected))
        {
            if (state.Gold >= _rerollCost)
            {
                state.Gold -= _rerollCost;
                _rerollCost += 3;
                GenerateShopItems(state);
                state.Assets.PlaySoundVariant("select", 0.5f);
            }
        }

        // Next wave button
        bool nextWaveSelected = _navRow == 2 && _navCol == 1;
        if (UIRenderer.DrawButton("NEXT WAVE",
            Constants.LogicalWidth / 2 - 55, Constants.LogicalHeight - 40, 110, 24, new Color(60, 100, 60, 255), nextWaveSelected))
        {
            NextWave(state, manager);
        }

        string hint = InputHelper.GamepadAvailable
            ? "D-Pad navigate, A buy/upgrade"
            : "Click buy/upgrade, Right-click sell";
        UIRenderer.DrawTextSmall(hint,
            Constants.LogicalWidth / 2 - hint.Length * 5 / 2, Constants.LogicalHeight - 8, Color.Gray);
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
