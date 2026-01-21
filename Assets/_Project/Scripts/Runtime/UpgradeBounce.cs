using UnityEngine;

public sealed class UpgradeBounce : MonoBehaviour
{
    [Header("Bounce")]
    [SerializeField] private bool animateInEditMode = false;
    [SerializeField] private float amplitude = 0.12f;
    [SerializeField] private float frequency = 2.2f;
    [SerializeField] private float rotationWobble = 8f;

    private Vector3 startLocalPos;
    private Quaternion startLocalRot;

    private void OnEnable()
    {
        CacheBase();
    }

    private void Start()
    {
        CacheBase();
    }

    private void OnValidate()
    {
        // чтобы после ручных правок база пересчиталась
        CacheBase();
    }

    private void CacheBase()
    {
        startLocalPos = transform.localPosition;
        startLocalRot = transform.localRotation;
    }

    private void Update()
    {
        if (!Application.isPlaying && !animateInEditMode)
            return;

        float t = Time.time;

        float y = Mathf.Sin(t * frequency * Mathf.PI * 2f) * amplitude;
        float r = Mathf.Sin((t * frequency * 0.8f) * Mathf.PI * 2f) * rotationWobble;

        transform.localPosition = startLocalPos + new Vector3(0f, y, 0f);
        transform.localRotation = startLocalRot * Quaternion.Euler(0f, 0f, r);
    }
}
