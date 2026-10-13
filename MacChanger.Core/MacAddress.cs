using System.Security.Cryptography;

namespace MacChanger.Core;

public static class MacAddress
{
    public static bool TryParse(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var hex = value.Replace(":", "").Replace("-", "").Replace(".", "").Trim();
        if (hex.Length != 12 || hex.Any(c => !Uri.IsHexDigit(c))) return false;
        var bytes = Convert.FromHexString(hex);
        if ((bytes[0] & 1) != 0) return false;
        normalized = string.Join(":", bytes.Select(b => b.ToString("X2")));
        return true;
    }

    public static string GenerateRandom(bool use02FirstOctet = false)
    {
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        bytes[0] = use02FirstOctet ? (byte)0x02 : (byte)((bytes[0] & 0xFC) | 0x02);
        return string.Join(":", bytes.ToArray().Select(b => b.ToString("X2")));
    }
}
