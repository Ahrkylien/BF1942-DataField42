using System.Text;

public static class Bf1942Encoding
{
    private static readonly char[] _charSet = { ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', '!', '"', '#', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '<', '=', '>', '?', '@', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '[', '\\', ']', '^', '_', '`', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '{', '|', '}', '~', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', '¡', '¢', '£', '¤', '¥', '¦', '§', '¨', '©', 'ª', '«', '¬', '­', '®', '¯', '°', '±', '²', '³', '´', 'µ', '¶', '·', '¸', '¹', 'º', '»', '¼', '½', '¾', '¿', 'À', 'Á', 'Â', 'Ã', 'Ä', 'Å', 'Æ', 'Ç', 'È', 'É', 'Ê', 'Ë', 'Ì', 'Í', 'Î', 'Ï', 'Ð', 'Ñ', 'Ò', 'Ó', 'Ô', 'Õ', 'Ö', '×', 'Ø', 'Ù', 'Ú', 'Û', 'Ü', 'Ý', 'Þ', 'ß', 'à', 'á', 'â', 'ã', 'ä', 'å', 'æ', 'ç', 'è', 'é', 'ê', 'ë', 'ì', 'í', 'î', 'ï', 'ð', 'ñ', 'ò', 'ó', 'ô', 'õ', 'ö', '÷', 'ø', 'ù', 'ú', 'û', 'ü', 'ý', 'þ', 'ÿ' };
    private static readonly char[] _nonLatinCharsCyrillic = { 'А', 'Б', 'В', 'Г', 'Д', 'Е', 'Ж', 'З', 'И', 'Й', 'К', 'Л', 'М', 'Н', 'О', 'П', 'Р', 'С', 'Т', 'У', 'Ф', 'Х', 'Ц', 'Ч', 'Ш', 'Щ', 'Ъ', 'Ы', 'Ь', 'Э', 'Ю', 'Я', 'а', 'б', 'в', 'г', 'д', 'е', 'ж', 'з', 'и', 'й', 'к', 'л', 'м', 'н', 'о', 'п', 'р', 'с', 'т', 'у', 'ф', 'х', 'ц', 'ч', 'ш', 'щ', 'ъ', 'ы', 'ь', 'э', 'ю', 'я' };

    private static bool IsUnprintableCharacter(byte character) => (0 <= character && character <= 31) || (127 <= character && character <= 159);
    private static bool IsLatinCharacter(byte character) => (65 <= character && character <= 90) || (97 <= character && character <= 122);
    private static bool IsNonLatinCharacter(byte character) => 192 <= character && character <= 255;

    private static char GetDefaultChar(byte b) => _charSet[b];
    private static char GetCyrillicChar(byte b) => IsNonLatinCharacter(b) ? _nonLatinCharsCyrillic[b - 192] : GetDefaultChar(b);


    public static string Decode(byte[] bytes, char replacingChar = ' ', bool applySmartEncodingDetection = false)
    {
        bool useDefaultChars = applySmartEncodingDetection ? !IsCryllicEncoding(bytes) : true;

        StringBuilder sb = new();
        foreach (byte b in bytes)
        {
            if (IsUnprintableCharacter(b))
                sb.Append(replacingChar);
            else
            {
                if (useDefaultChars)
                    sb.Append(GetDefaultChar(b));
                else
                    sb.Append(GetCyrillicChar(b));
            }
        }
        return sb.ToString();
    }

    public static byte[] Encode(string value)
    {
        var bytes = new byte[value.Length];
        int i = 0;
        foreach (char b in value)
        {
            if (_charSet.Contains(b))
                bytes[i] = (byte)Array.IndexOf(_charSet, b);
            else
                bytes[i] = 0x20; // space
            i++;
        }
        return bytes;
    }

    private static bool IsCryllicEncoding(byte[] bytes)
    {
        int numLatinChars = 0;
        int numNonLatinChars = 0;
        foreach (byte b in bytes)
        {
            if (IsLatinCharacter(b))
                numLatinChars++;
            else if (IsNonLatinCharacter(b))
                numNonLatinChars++;
        }
        return (numNonLatinChars + numLatinChars) != 0 && (numNonLatinChars / (float)(numNonLatinChars + numLatinChars)) >= 0.8;
    }
}