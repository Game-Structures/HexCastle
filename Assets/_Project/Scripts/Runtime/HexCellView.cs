using UnityEngine;

public sealed class HexCellView : MonoBehaviour
{
    public int q;
    public int r;

    [HideInInspector] public Renderer rend;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend == null) rend = GetComponent<Renderer>();
    }
}
