using Raylib_cs;

namespace CloneTato.Data;

public enum WeaponType
{
    Auto,   // fires automatically when enemies are in range
    Manual, // fires on mouse click (grenades, mines)
    Melee,  // instant arc damage, no projectiles
}

public enum FireMode
{
    HoldAuto,    // hold button to fire continuously (SMG, Minigun, Laser)
    TapSemi,     // each press = one shot (Pistol, Shotgun, Sniper)
    TapCooldown, // one activation then cooldown (Grenade, Rocket — secondaries)
}

public enum WeaponSlot
{
    Primary,   // main weapon, RT / left click
    Secondary, // tactical weapon, LT / right click, cooldown-based
}

public class WeaponDef
{
    public string Name = "";
    public int SpriteIndex;
    public WeaponType Type = WeaponType.Auto;
    public FireMode FireMode = FireMode.HoldAuto;
    public WeaponSlot Slot = WeaponSlot.Primary;
    public float BaseDamage;
    public float FireRate; // attacks per second
    public float ProjectileSpeed;
    public float Range;
    public int PierceCount;
    public float Spread; // radians, for shotgun-type
    public int BurstCount = 1;
    public int ShopTier = 1;
    public int Cost = 65;
    public Color ProjectileColor = Color.Yellow;
    public int ClipSize = 0;       // 0 = unlimited (melee, mines)
    public float ReloadTime = 1.0f; // seconds to reload
    public int MaxUpgradeLevel = 5;
    public float CooldownTime = 0f; // seconds, for TapCooldown secondaries (0 = use 1/FireRate)

    // Melee specific
    public float MeleeArc = MathF.PI * 0.6f; // swing arc width in radians

    // Explosive specific (grenade/rocket)
    public float ExplosionRadius; // 0 = no explosion, >0 = AOE on impact/expiry

    // Mine specific
    public bool IsMine; // placed at feet, detonates on enemy proximity
    public float MineProximity = 25f; // trigger radius

    // Lock-on missile specific
    public bool IsLockOn; // fires homing missiles that lock onto enemies
    public int MissileCount = 10; // total missiles per volley
    public float MissileTurnRate = 6f; // radians/sec steering
}

/// <summary>
/// A mutable weapon instance the player carries. Wraps a WeaponDef and tracks upgrade level.
/// </summary>
public class WeaponInstance
{
    public WeaponDef Def;
    public int UpgradeLevel; // 0 = base, up to Def.MaxUpgradeLevel

    public WeaponInstance(WeaponDef def)
    {
        Def = def;
        UpgradeLevel = 0;
    }

    // Effective stats — accelerating curve: each level worth more than the last
    // Level 1: +25%, Level 2: +55%, Level 3: +90%, Level 4: +130%, Level 5: +175%
    private float DamageScale => 1f + UpgradeLevel * 0.25f + UpgradeLevel * UpgradeLevel * 0.05f;
    private float RateScale => 1f + UpgradeLevel * 0.12f + UpgradeLevel * UpgradeLevel * 0.02f;

    public float Damage => Def.BaseDamage * DamageScale;
    public float FireRate => Def.FireRate * RateScale;
    public float ProjectileSpeed => Def.ProjectileSpeed * (1f + UpgradeLevel * 0.06f);
    public float Range => Def.Range * (1f + UpgradeLevel * 0.10f);
    public int PierceCount => Def.PierceCount + (UpgradeLevel + 1) / 2; // +1 at level 1, +1 more at level 3, etc.
    public int BurstCount => Def.BurstCount + (Def.BurstCount > 1 ? (UpgradeLevel + 1) / 2 : 0);
    public float MeleeArc => Def.MeleeArc * (1f + UpgradeLevel * 0.08f);
    public float ExplosionRadius => Def.ExplosionRadius * (1f + UpgradeLevel * 0.15f);
    public int MissileCount => Def.MissileCount + UpgradeLevel * 2; // +2 missiles per upgrade
    public int ClipSize => Def.ClipSize + UpgradeLevel * 2; // +2 per upgrade
    public float ReloadTime => Def.ReloadTime * (1f - UpgradeLevel * 0.10f); // faster reload with upgrades

    public bool CanUpgrade => UpgradeLevel < Def.MaxUpgradeLevel;
    public int UpgradeCost => Def.Cost * 2 / 3 + UpgradeLevel * 20;

    public string StatsText()
    {
        string s = $"DMG:{Damage:F0} SPD:{FireRate:F1}";
        if (ClipSize > 0) s += $" CLIP:{ClipSize}";
        if (PierceCount > 0) s += $" PRC:{PierceCount}";
        if (BurstCount > 1) s += $" x{BurstCount}";
        return s;
    }

    public string UpgradePreview()
    {
        if (!CanUpgrade) return "MAX LEVEL";
        int next = UpgradeLevel + 1;
        float nextDmg = Def.BaseDamage * (1f + next * 0.25f + next * next * 0.05f);
        float nextRate = Def.FireRate * (1f + next * 0.12f + next * next * 0.02f);
        return $"DMG:{Damage:F0}->{nextDmg:F0} SPD:{FireRate:F1}->{nextRate:F1}";
    }
}

public static class WeaponDatabase
{
    public static readonly WeaponDef[] Weapons =
    {
        // === PRIMARY GUNS (Tier 1) ===
        // Balanced for single-weapon system (was 6-weapon orbit). ~3.5x old damage.
        new()
        {
            Name = "Pistol", SpriteIndex = 0, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.TapSemi,
            BaseDamage = 38, FireRate = 3.5f, ProjectileSpeed = 280f,
            Range = 180f, PierceCount = 0, ClipSize = 12, ReloadTime = 0.9f,
            ShopTier = 1, Cost = 50, ProjectileColor = Color.Yellow,
        },
        new()
        {
            Name = "SMG", SpriteIndex = 1, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 14, FireRate = 8f, ProjectileSpeed = 260f,
            Range = 150f, PierceCount = 0, Spread = 0.12f, ClipSize = 30, ReloadTime = 0.8f,
            ShopTier = 1, Cost = 65, ProjectileColor = Color.Orange,
        },
        new()
        {
            Name = "Shotgun", SpriteIndex = 2, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.TapSemi,
            BaseDamage = 18, FireRate = 1.5f, ProjectileSpeed = 230f,
            Range = 110f, PierceCount = 0, Spread = 0.35f, BurstCount = 6, ClipSize = 6, ReloadTime = 1.2f,
            ShopTier = 1, Cost = 70, ProjectileColor = Color.Red,
        },
        new()
        {
            Name = "Crossbow", SpriteIndex = 3, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.TapSemi,
            BaseDamage = 75, FireRate = 1.2f, ProjectileSpeed = 340f,
            Range = 220f, PierceCount = 2, ClipSize = 1, ReloadTime = 0.6f,
            ShopTier = 1, Cost = 75, ProjectileColor = Color.SkyBlue,
        },

        // === MELEE (BladeDancer primary only) ===
        new()
        {
            Name = "Knife", SpriteIndex = 10, Type = WeaponType.Melee,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 30, FireRate = 4.0f, Range = 30f,
            MeleeArc = MathF.PI * 0.5f, ShopTier = 1, Cost = 45,
        },
        new()
        {
            Name = "Sword", SpriteIndex = 11, Type = WeaponType.Melee,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 55, FireRate = 2.2f, Range = 38f,
            MeleeArc = MathF.PI * 0.7f, ShopTier = 1, Cost = 65,
        },
        new()
        {
            Name = "Spear", SpriteIndex = 12, Type = WeaponType.Melee,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 48, FireRate = 2.8f, Range = 48f,
            MeleeArc = MathF.PI * 0.3f, ShopTier = 2, Cost = 95,
        },
        new()
        {
            Name = "Hammer", SpriteIndex = 13, Type = WeaponType.Melee,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 120, FireRate = 1.0f, Range = 36f,
            MeleeArc = MathF.PI * 0.9f, ShopTier = 2, Cost = 120,
        },

        // === PRIMARY GUNS (Tier 2) ===
        new()
        {
            Name = "Rifle", SpriteIndex = 4, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.TapSemi,
            BaseDamage = 55, FireRate = 2.8f, ProjectileSpeed = 320f,
            Range = 200f, PierceCount = 1, ClipSize = 15, ReloadTime = 1.0f,
            ShopTier = 2, Cost = 115, ProjectileColor = Color.Gold,
        },
        new()
        {
            Name = "Dual Pistols", SpriteIndex = 5, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 28, FireRate = 5.5f, ProjectileSpeed = 280f,
            Range = 160f, PierceCount = 0, Spread = 0.08f, ClipSize = 24, ReloadTime = 0.9f,
            ShopTier = 2, Cost = 105, ProjectileColor = Color.Yellow,
        },
        new()
        {
            Name = "Sniper", SpriteIndex = 6, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.TapSemi,
            BaseDamage = 150, FireRate = 0.8f, ProjectileSpeed = 450f,
            Range = 280f, PierceCount = 3, ClipSize = 4, ReloadTime = 1.4f,
            ShopTier = 2, Cost = 125, ProjectileColor = Color.White,
        },

        // === SECONDARY WEAPONS (Tier 2-3, cooldown-based) ===
        new()
        {
            Name = "Grenade", SpriteIndex = 14, Type = WeaponType.Manual,
            Slot = WeaponSlot.Secondary, FireMode = FireMode.TapCooldown,
            BaseDamage = 100, FireRate = 0.8f, ProjectileSpeed = 200f,
            Range = 180f, ExplosionRadius = 50f, CooldownTime = 2.5f,
            ShopTier = 2, Cost = 100, ProjectileColor = Color.DarkGreen,
        },
        new()
        {
            Name = "Mine Layer", SpriteIndex = 15, Type = WeaponType.Manual,
            Slot = WeaponSlot.Secondary, FireMode = FireMode.TapCooldown,
            BaseDamage = 140, FireRate = 0.5f, Range = 30f,
            ExplosionRadius = 45f, IsMine = true, MineProximity = 25f, CooldownTime = 3.0f,
            ShopTier = 2, Cost = 90, ProjectileColor = Color.Gray,
        },
        new()
        {
            Name = "Bomb", SpriteIndex = 16, Type = WeaponType.Manual,
            Slot = WeaponSlot.Secondary, FireMode = FireMode.TapCooldown,
            BaseDamage = 220, FireRate = 0.3f, ProjectileSpeed = 150f,
            Range = 140f, ExplosionRadius = 65f, CooldownTime = 4.5f,
            ShopTier = 3, Cost = 165, ProjectileColor = Color.Orange,
        },

        // === PRIMARY GUNS (Tier 3) ===
        new()
        {
            Name = "Minigun", SpriteIndex = 7, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 15, FireRate = 12f, ProjectileSpeed = 260f,
            Range = 160f, PierceCount = 0, Spread = 0.18f, ClipSize = 60, ReloadTime = 1.6f,
            ShopTier = 3, Cost = 175, ProjectileColor = Color.Orange,
        },
        new()
        {
            Name = "Laser", SpriteIndex = 9, Type = WeaponType.Auto,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 10, FireRate = 18f, ProjectileSpeed = 400f,
            Range = 200f, PierceCount = 2, ClipSize = 40, ReloadTime = 1.2f,
            ShopTier = 3, Cost = 165, ProjectileColor = Color.Lime,
        },

        // === SECONDARY WEAPONS (Tier 3, cooldown-based) ===
        new()
        {
            Name = "Rocket", SpriteIndex = 8, Type = WeaponType.Auto,
            Slot = WeaponSlot.Secondary, FireMode = FireMode.TapCooldown,
            BaseDamage = 180, FireRate = 0.4f, ProjectileSpeed = 170f,
            Range = 220f, PierceCount = 0, ExplosionRadius = 55f, CooldownTime = 3.5f,
            ShopTier = 3, Cost = 190, ProjectileColor = Color.Red,
        },
        new()
        {
            Name = "Missile Launcher", SpriteIndex = 18, Type = WeaponType.Auto,
            Slot = WeaponSlot.Secondary, FireMode = FireMode.TapCooldown,
            BaseDamage = 35, FireRate = 0.25f, ProjectileSpeed = 140f,
            Range = 320f, PierceCount = 0, ExplosionRadius = 35f,
            CooldownTime = 5.0f,
            ShopTier = 3, Cost = 200, ProjectileColor = Color.Red,
            IsLockOn = true, MissileCount = 12, MissileTurnRate = 7f,
        },

        // === MELEE (Tier 3, BladeDancer only) ===
        new()
        {
            Name = "Cleaver", SpriteIndex = 17, Type = WeaponType.Melee,
            Slot = WeaponSlot.Primary, FireMode = FireMode.HoldAuto,
            BaseDamage = 160, FireRate = 1.4f, Range = 42f,
            MeleeArc = MathF.PI * 0.8f, ShopTier = 3, Cost = 150,
        },
    };
}
