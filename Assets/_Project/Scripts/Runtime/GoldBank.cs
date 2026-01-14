public static class GoldBank
{
    public static int Gold { get; private set; }

    public static void Reset(int startGold = 0)
    {
        Gold = startGold;
    }

    public static void Add(int amount)
    {
        if (amount > 0) Gold += amount;
    }

    public static bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }
}
