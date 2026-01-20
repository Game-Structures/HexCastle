using System.Collections.Generic;
using UnityEngine;

public class BuildingEffectsManager : MonoBehaviour
{
    public static BuildingEffectsManager Instance { get; private set; }

    [Header("Tuning")]
    public int bankCoinsPerBuildRound = 5;
    public int quarryMaxHpBonusPerWall = 100;
    public float forgeHealAtCombatEnd = 50f;

    public float telescopeRangeBonusPercent = 10f;

    private readonly Dictionary<BuildingId, int> _counts = new Dictionary<BuildingId, int>();

    private bool _artilleryUnlocked;

    public bool IsArtilleryUnlocked => _artilleryUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterBuilding(CastleBuilding b)
    {
        if (b == null) return;
        Add(b.id, +1);
        ApplyPersistentEffects();
        Debug.Log($"[BuildingEffects] Register {b.id} -> count={GetCount(b.id)}");
    }

    public void UnregisterBuilding(CastleBuilding b)
    {
        if (b == null) return;
        Add(b.id, -1);
        ApplyPersistentEffects();
        Debug.Log($"[BuildingEffects] Unregister {b.id} -> count={GetCount(b.id)}");
    }

    public int GetCount(BuildingId id)
    {
        int v;
        return _counts.TryGetValue(id, out v) ? v : 0;
    }

    private void Add(BuildingId id, int delta)
    {
        int v = GetCount(id) + delta;
        if (v < 0) v = 0;
        _counts[id] = v;
    }

    public void OnBuildRoundStart()
    {
        int banks = GetCount(BuildingId.Bank);
        int add = bankCoinsPerBuildRound * banks;

        if (add > 0)
            GoldBank.Add(add);

        Debug.Log($"[BuildingEffects] BuildRoundStart: banks={banks}, addGold={add}, totalGold={GoldBank.Gold}");
    }

    public void OnCombatRoundEnd()
    {
        int forges = GetCount(BuildingId.Forge);
        if (forges <= 0) return;

        float heal = forgeHealAtCombatEnd * forges;
        bool ok = WallTileLink.HealRandomDamagedWall(heal);

        Debug.Log($"[BuildingEffects] CombatRoundEnd: forges={forges}, heal={heal}, applied={ok}");
    }

    private void ApplyPersistentEffects()
    {
        // Каменоломня
        int quarries = GetCount(BuildingId.Quarry);
        float wallBonus = quarries * quarryMaxHpBonusPerWall;
        WallTileLink.SetGlobalMaxHpBonus(wallBonus);

        // Телескоп
        int telescopes = GetCount(BuildingId.Telescope);
        float mul = 1f + (telescopeRangeBonusPercent / 100f) * telescopes;
        TowerShooter.SetGlobalRangeMultiplier(mul);

        // Разблок артиллерии
        _artilleryUnlocked = GetCount(BuildingId.ArtilleryUnlock) > 0;

        Debug.Log($"[BuildingEffects] Range multiplier = {mul:0.00} (telescopes={telescopes}) | ArtilleryUnlocked={_artilleryUnlocked}");
    }
}
