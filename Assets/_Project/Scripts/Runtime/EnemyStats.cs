using UnityEngine;

[CreateAssetMenu(menuName = "HexaCastle/Enemy Stats", fileName = "EnemyStats")]
public sealed class EnemyStats : ScriptableObject
{
    public int maxHp = 100;
    public float speed = 1f;

    public int attackDamage = 50;
    public float attackInterval = 5f;
}
