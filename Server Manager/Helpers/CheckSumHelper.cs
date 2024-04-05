public static class CheckSumHelper
{
    public static UInt32 Adler32(byte[] data)
    {
        const int mod = 65521;
        UInt32 a = 1, b = 0;
        foreach (var c in data)
        {
            a = (a + c) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }
}
