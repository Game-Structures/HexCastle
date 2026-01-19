public static class TowerSlotBlocker
{
    public static bool IsBlocked { get; private set; }

    public static void SetBlocked(bool blocked)
    {
        IsBlocked = blocked;
    }
}
