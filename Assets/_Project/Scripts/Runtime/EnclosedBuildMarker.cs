// Assets/_Project/Scripts/Runtime/EnclosedBuildMarker.cs
using UnityEngine;

public sealed class EnclosedBuildMarker : MonoBehaviour
{
    private Vector2Int key;

    public void Init(Vector2Int k) => key = k;

    private void OnMouseDown()
    {
        if (EnclosureDebug.Instance == null) return;
        EnclosureDebug.Instance.TryBuildAt(key);
    }
}
