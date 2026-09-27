using System.Security.Cryptography;

public static class LobbyCodeUtility
{
    public const int CodeLength = 6;
    private const string HexCharacters = "0123456789ABCDEF";

    public static string Generate()
    {
        byte[] randomBytes = new byte[CodeLength];

        using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
        {
            generator.GetBytes(randomBytes);
        }

        char[] characters = new char[CodeLength];

        for (int i = 0; i < characters.Length; i++)
            characters[i] = HexCharacters[randomBytes[i] & 0x0F];

        return new string(characters);
    }

    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    public static bool IsValid(string value)
    {
        string normalized = Normalize(value);

        if (normalized.Length != CodeLength)
            return false;

        for (int i = 0; i < normalized.Length; i++)
        {
            if (HexCharacters.IndexOf(normalized[i]) < 0)
                return false;
        }

        return true;
    }
}
