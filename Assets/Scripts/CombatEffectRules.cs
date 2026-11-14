using System;

// Pure source rules from Divyanshu@b9147af; PvP streak adaptation explicitly approved.
public static class CombatEffectRules
{
    public static int AddArmor(int current, int amount, int maximum) =>
        (int)Math.Min(Math.Max(0, maximum), (long)Math.Max(0, current) + Math.Max(0, amount));
    public static int AbsorbArmor(ref int armor, int damage)
    {
        int absorbed = Math.Min(Math.Max(0, armor), Math.Max(0, damage));
        armor = Math.Max(0, armor - absorbed);
        return Math.Max(0, damage) - absorbed;
    }
    public static int NextStreak(int current) => current < int.MaxValue ? Math.Max(0, current) + 1 : int.MaxValue;
    public static bool EarnsReward(int streak) => streak > 0 && streak % 5 == 0;
    // Source Random.Range(1,5): Shield, Speed, Jump, Damage; excludes new armor/invisibility.
    public static int OriginalReward(int roll) => roll >= 1 && roll <= 4 ? roll : 0;
}
