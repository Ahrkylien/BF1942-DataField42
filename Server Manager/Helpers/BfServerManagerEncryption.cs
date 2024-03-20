using System.Text;

static class BfServerManagerEncryption
{
    private static readonly byte[] _xorArray =
    [
        0x6E, 0xFA, 0x87, 0xC5, 0x2A, 0x09, 0x36, 0x3D,
        0x47, 0x6B, 0xC4, 0x17, 0x23, 0x27, 0xC3, 0x13,
        0x27, 0xA7, 0x71, 0x48, 0x7A, 0x45, 0xD0, 0x75,
        0x6B, 0x08, 0x66, 0x18, 0x10, 0xC9, 0xF4, 0xA9
    ];

    static public string Decrypt(byte[] encryptedBytes)
    {
        byte[] rawBytes = ApplyXor(encryptedBytes);
        int zeroIndex = Array.IndexOf(rawBytes, (byte)0);
        if (zeroIndex != -1)
            rawBytes = rawBytes.Take(zeroIndex).ToArray();
        return Encoding.UTF8.GetString(rawBytes);
    }

    static public byte[] Encrypt(string stringToEncrypt)
    {
        if (stringToEncrypt.Length > 31)
            throw new ArgumentOutOfRangeException(nameof(stringToEncrypt), "The string to encrypt must not exceed 31 characters, since the server does not support 32 bytes");

        byte[] bytesToEncrypt = Encoding.UTF8.GetBytes(stringToEncrypt);
        return ApplyXor(bytesToEncrypt);
    }

    static public byte[] ApplyXor(byte[] bytesIn)
    {
        if (bytesIn.Length > 32)
            throw new ArgumentOutOfRangeException(nameof(bytesIn), "The byte list to xor must not exceed 32 items");

        byte[] bytesOut = [.. bytesIn, .. new byte[32 - bytesIn.Length]];
        for (int i = 0; i < 32; i++)
        {
            bytesOut[i] ^= _xorArray[i];
        }
        return bytesOut;
    }
}