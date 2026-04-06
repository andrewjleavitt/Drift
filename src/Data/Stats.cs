using System.Numerics;

namespace CloneTato.Data;

public struct Stats
{
    public int MaxHP;
    public float MoveSpeed;
    public float DamageMultiplier;
    public float AttackSpeedMultiplier;
    public float CritChance;
    public float CritDamage;
    public int Armor;
    public float DodgeChance;
    public float PickupRange;
    public float XPMultiplier;
    public float ReloadSpeedMultiplier;
    public float DashSpeedBonus;
    public float DashCooldownReduction;
    public float DashDurationBonus;
    public float PostDashAttackSpeed;  // bonus attack speed multiplier after dash
    public float PostDashMoveSpeed;    // bonus move speed multiplier after dash
    public float PostDashInvuln;       // seconds of invulnerability after dash
    public float RangedDamageMultiplier; // multiplier for ranged weapon damage (0 = no modifier)
    public float MeleeDamageMultiplier;  // multiplier for melee weapon damage (0 = no modifier)

    public static Stats Default() => new()
    {
        MaxHP = 100,
        MoveSpeed = 105f,
        DamageMultiplier = 1.0f,
        AttackSpeedMultiplier = 1.0f,
        CritChance = 0.05f,
        CritDamage = 1.5f,
        Armor = 0,
        DodgeChance = 0f,
        PickupRange = 50f,
        XPMultiplier = 1.0f,
        ReloadSpeedMultiplier = 1.0f,
        DashSpeedBonus = 0f,
        DashCooldownReduction = 0f,
        DashDurationBonus = 0f,
    };

    public static Stats operator +(Stats a, Stats b) => new()
    {
        MaxHP = a.MaxHP + b.MaxHP,
        MoveSpeed = a.MoveSpeed + b.MoveSpeed,
        DamageMultiplier = a.DamageMultiplier + b.DamageMultiplier,
        AttackSpeedMultiplier = a.AttackSpeedMultiplier + b.AttackSpeedMultiplier,
        CritChance = a.CritChance + b.CritChance,
        CritDamage = a.CritDamage + b.CritDamage,
        Armor = a.Armor + b.Armor,
        DodgeChance = a.DodgeChance + b.DodgeChance,
        PickupRange = a.PickupRange + b.PickupRange,
        XPMultiplier = a.XPMultiplier + b.XPMultiplier,
        ReloadSpeedMultiplier = a.ReloadSpeedMultiplier + b.ReloadSpeedMultiplier,
        DashSpeedBonus = a.DashSpeedBonus + b.DashSpeedBonus,
        DashCooldownReduction = a.DashCooldownReduction + b.DashCooldownReduction,
        DashDurationBonus = a.DashDurationBonus + b.DashDurationBonus,
        PostDashAttackSpeed = a.PostDashAttackSpeed + b.PostDashAttackSpeed,
        PostDashMoveSpeed = a.PostDashMoveSpeed + b.PostDashMoveSpeed,
        PostDashInvuln = a.PostDashInvuln + b.PostDashInvuln,
        RangedDamageMultiplier = a.RangedDamageMultiplier + b.RangedDamageMultiplier,
        MeleeDamageMultiplier = a.MeleeDamageMultiplier + b.MeleeDamageMultiplier,
    };
}
