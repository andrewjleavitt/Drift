namespace CloneTato.Data;

public enum HeroType
{
    Gunslinger,  // Gun hero (default)
    BladeDancer, // Sword hero (melee focus)
    Drifter,     // Starter Pack hero (glass cannon + companion)
}

public class CharacterDef
{
    public string Name = "";
    public string Description = "";
    public HeroType HeroType;
    public int SpriteIndex; // legacy Kenney sprite index (fallback)
    public Stats BaseStats;
    public int StartingWeaponIndex;
}

public static class CharacterDatabase
{
    public static readonly CharacterDef[] Characters =
    {
        new()
        {
            Name = "Gunslinger",
            Description = "Balanced ranged fighter",
            HeroType = HeroType.Gunslinger,
            SpriteIndex = 0,
            BaseStats = Stats.Default(),
            StartingWeaponIndex = 0, // Pistol
        },
        new()
        {
            Name = "Blade Dancer",
            Description = "Melee powerhouse, weak ranged",
            HeroType = HeroType.BladeDancer,
            SpriteIndex = 0,
            BaseStats = new Stats
            {
                MaxHP = 120, MoveSpeed = 110f, DamageMultiplier = 1.4f,
                AttackSpeedMultiplier = 1.0f, CritChance = 0.10f, CritDamage = 1.8f,
                Armor = 2, DodgeChance = 0.05f, PickupRange = 50f, XPMultiplier = 1.0f,
                ReloadSpeedMultiplier = 0.7f, // slower reload = ranged penalty
                RangedDamageMultiplier = 0.7f, // -30% ranged damage
                MeleeDamageMultiplier = 1.4f,  // +40% melee damage
            },
            StartingWeaponIndex = 5, // Melee weapon (machete)
        },
        new()
        {
            Name = "Drifter",
            Description = "Glass cannon + companion",
            HeroType = HeroType.Drifter,
            SpriteIndex = 0,
            BaseStats = new Stats
            {
                MaxHP = 60, MoveSpeed = 120f, DamageMultiplier = 1.5f,
                AttackSpeedMultiplier = 1.1f, CritChance = 0.08f, CritDamage = 1.5f,
                Armor = 0, DodgeChance = 0.05f, PickupRange = 70f, XPMultiplier = 1.1f,
                ReloadSpeedMultiplier = 1.0f,
            },
            StartingWeaponIndex = 0, // Pistol
        },
    };
}
