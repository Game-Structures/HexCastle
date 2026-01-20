using UnityEngine;

public class CastleBuilding : MonoBehaviour
{
    public BuildingId id;

    private void OnEnable()
    {
        if (BuildingEffectsManager.Instance != null)
            BuildingEffectsManager.Instance.RegisterBuilding(this);
    }

    private void OnDisable()
    {
        if (BuildingEffectsManager.Instance != null)
            BuildingEffectsManager.Instance.UnregisterBuilding(this);
    }
}
