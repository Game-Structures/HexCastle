using UnityEngine;

public static class TowerUpgradeApplier
{
    public static void Apply(TowerUpgradeId id, TowerShooter shooter)
    {
        if (shooter == null) return;

        switch (id)
        {
            case TowerUpgradeId.DamagePlus10:
                shooter.ModifyDamageMultiplier(1.10f);
                break;

            case TowerUpgradeId.FireRatePlus10:
                shooter.ModifyFireRateMultiplier(1.10f);
                break;

            case TowerUpgradeId.RangePlus1:
                shooter.AddRangeTiles(1);
                break;

            // Криты добавим отдельным шагом, когда введём параметры критов в TowerShooter
            case TowerUpgradeId.CritChancePlus10:
            case TowerUpgradeId.CritDamagePlus25:
                break;
        }
    }
}
