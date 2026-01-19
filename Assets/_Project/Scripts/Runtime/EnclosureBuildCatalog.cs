using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "HexCastle/Enclosure Build Catalog", fileName = "EnclosureBuildCatalog")]
public sealed class EnclosureBuildCatalog : ScriptableObject
{
    public List<EnclosureBuildOption> options = new List<EnclosureBuildOption>();
}

[System.Serializable]
public sealed class EnclosureBuildOption
{
    public Sprite icon;
    public string title;
    [TextArea(2, 4)] public string description;

    public int cost = 15;

    // Пока вместо реальных построек – материал клетки (визуальная “постройка”).
    public Material tileMaterial;

    // На следующий шаг: реальный префаб постройки.
    public GameObject prefab;
}
