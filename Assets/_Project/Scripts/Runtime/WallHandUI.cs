using UnityEngine;

public sealed class WallHandUI : MonoBehaviour
{
    [SerializeField] private WallHandManager hand;

    private void Awake()
    {
        if (hand == null)
            hand = FindFirstObjectByType<WallHandManager>();
    }

    // ВАЖНО: раздачу руки на старте НЕ делаем здесь.
    // Это делает WaveController (NewRoundHand в Start / начале волны).

    // Оставляем имя Deal3, чтобы не перенастраивать кнопку в инспекторе
    // (RefreshButton может быть привязан к WallHandUI.Deal3)
    public void Deal3()
    {
        if (hand == null) return;
        hand.RefreshHand();
    }
}
