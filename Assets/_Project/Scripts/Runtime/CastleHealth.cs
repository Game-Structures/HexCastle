using UnityEngine;

public sealed class CastleHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 3000;
    private int hp;
    private bool isDead;

public int CurrentHp => hp;
public int MaxHp => maxHp;
    private void Awake()
    {
        GameState.Reset();
        hp = maxHp;
        Debug.Log($"Castle HP: {hp}");
    }

    public void Damage(int amount)
    {
        if (isDead) return;
        if (GameState.IsGameOver) return;

        hp -= amount;
        Debug.Log($"Castle HP: {hp}");

        if (hp <= 0)
        {
            hp = 0;
            isDead = true;

            GameState.IsGameOver = true;
            Debug.Log("GAME OVER");

            if (GameOverUI.Instance != null)
                GameOverUI.Instance.Show();
        }
    }
}
