using UnityEngine;

[ExecuteAlways]
public sealed class TowerUpgradeIndicator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TowerProgress progress;
    [SerializeField] private GameObject indicator;

    private bool lastState;

    private void OnEnable()
    {
        AutoBind();
        ApplyState(force: true);
    }

    private void Update()
    {
        // В Edit Mode Update тоже вызывается из-за ExecuteAlways.
        ApplyState(force: false);
    }

    private void OnValidate()
    {
        AutoBind();
        ApplyState(force: true);
    }

    private void AutoBind()
    {
        if (progress == null)
            progress = GetComponent<TowerProgress>();

        if (indicator == null)
        {
            var t = transform.Find("UpgradeCheck");
            if (t != null) indicator = t.gameObject;
        }
    }

    private void ApplyState(bool force)
    {
        bool state = (progress != null && progress.CanUpgrade);

        if (!force && state == lastState)
            return;

        lastState = state;

        if (indicator != null)
            indicator.SetActive(state);
    }
}
