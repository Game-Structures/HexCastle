using UnityEngine;

[CreateAssetMenu(menuName = "HexaCastle/Enemy Type", fileName = "EnemyType_")]
public sealed class EnemyType : ScriptableObject
{
    [Header("Id (for debug)")]
    public string id = "basic";

    [Header("Prefab (body/visual)")]
    public GameObject prefab;

    [Header("Stats")]
    public EnemyStats stats;

    [Header("Spawn rules")]
    [Min(1)] public int minWave = 1;
    [Min(0)] public int maxWave = 0; // 0 = no limit
    [Min(0f)] public float weight = 1f;
}
