using UnityEngine;

public sealed class TowerProgress : MonoBehaviour
{
    [Header("Progress")]
    [SerializeField, Range(0, 3)] private int tier = 0;
    [SerializeField] private int xp = 0;

    [Header("XP thresholds per tier (0->1, 1->2, 2->3)")]
    [SerializeField] private int[] xpThresholds = new int[] { 100, 200, 400 };

    public int Tier => tier;
    public int Xp => xp;

    public int XpToNext
    {
        get
        {
            if (tier >= 3) return 0;
            EnsureThresholds();
            return xpThresholds[tier];
        }
    }

    public bool CanUpgrade => tier < 3 && xp >= XpToNext;

    public void AddXP(int amount)
    {
        if (amount <= 0) return;
        if (tier >= 3) return;

        xp += amount;
        if (xp < 0) xp = 0;
    }

    /// <summary>
    /// Call this AFTER player picked 1 upgrade option.
    /// Consumes XP for current tier and increases tier by 1. Keeps leftover XP.
    /// </summary>
    public bool ConsumeUpgrade()
    {
        if (!CanUpgrade) return false;

        int cost = XpToNext;
        xp -= cost;
        if (xp < 0) xp = 0;

        tier = Mathf.Clamp(tier + 1, 0, 3);
        return true;
    }

    public void ResetProgress()
    {
        tier = 0;
        xp = 0;
    }

    private void OnValidate()
    {
        tier = Mathf.Clamp(tier, 0, 3);
        if (xp < 0) xp = 0;
        EnsureThresholds();
        for (int i = 0; i < xpThresholds.Length; i++)
            xpThresholds[i] = Mathf.Max(1, xpThresholds[i]);
    }

    private void EnsureThresholds()
    {
        if (xpThresholds == null || xpThresholds.Length < 3)
            xpThresholds = new int[] { 100, 200, 400 };
    }
}
