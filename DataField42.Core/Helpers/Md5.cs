public static class Md5
{
    public static string Hash(string inputString)
    {
        var inputBytes = System.Text.Encoding.ASCII.GetBytes(inputString);
        var hashBytes = System.Security.Cryptography.MD5.Create().ComputeHash(inputBytes);
        var hashString = Convert.ToHexString(hashBytes);
        if (hashString.Length != 32)
            throw new Exception($"MD5 calcuation of {inputString} gave incorrect length: {hashBytes}");
        return hashString;
    }
}