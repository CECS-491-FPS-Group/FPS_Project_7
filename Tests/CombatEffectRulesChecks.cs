using System;

public static class CombatEffectRulesChecks
{
    private static int checks;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    public static void Main()
    {
        Check(CombatEffectRules.AddArmor(0, 100, 100) == 100, "source armor pickup grants 100");
        Check(CombatEffectRules.AddArmor(70, 100, 100) == 100, "stacking armor is capped");
        Check(CombatEffectRules.AddArmor(70, -1, 100) == 70, "negative grant cannot remove armor");
        Check(CombatEffectRules.AddArmor(int.MaxValue, int.MaxValue, 100) == 100, "armor addition cannot overflow");
        int armor = 100;
        Check(CombatEffectRules.AbsorbArmor(ref armor, 40) == 0 && armor == 60, "armor fully absorbs nonlethal hit");
        Check(CombatEffectRules.AbsorbArmor(ref armor, 80) == 20 && armor == 0, "remaining damage reaches HP");
        Check(CombatEffectRules.AbsorbArmor(ref armor, 40) == 40 && armor == 0, "no armor does not reduce damage");
        Check(CombatEffectRules.AbsorbArmor(ref armor, -40) == 0 && armor == 0, "invalid damage cannot grant armor");
        int streak = 0, rewards = 0;
        for (int kill = 1; kill <= 20; kill++)
        {
            streak = CombatEffectRules.NextStreak(streak);
            bool reward = CombatEffectRules.EarnsReward(streak);
            Check(reward == (kill % 5 == 0), "rewards only at 5/10/15/20");
            if (reward) rewards++;
        }
        Check(rewards == 4, "four five-kill thresholds");
        Check(!CombatEffectRules.EarnsReward(0) && !CombatEffectRules.EarnsReward(-5), "no initial/invalid reward");
        Check(CombatEffectRules.NextStreak(int.MaxValue) == int.MaxValue, "counter does not overflow");
        for (int roll = 1; roll <= 4; roll++) Check(CombatEffectRules.OriginalReward(roll) == roll, "original source reward pool");
        Check(CombatEffectRules.OriginalReward(0) == 0 && CombatEffectRules.OriginalReward(5) == 0 &&
            CombatEffectRules.OriginalReward(6) == 0, "armor and invisibility excluded from streak rewards");
        Console.WriteLine("Passed " + checks + " armor/streak rule checks.");
    }
}
