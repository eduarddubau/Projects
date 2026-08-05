using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Security;

/// <summary>Bearer tokens handed to a client and stored only as a hash.</summary>
public static class SecureToken
{
    private const int ByteLength = 64;

    public static (string Raw, string Hash) Generate()
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength));
        return (raw, Hash(raw));
    }

    // High-entropy token, so a fast hash suffices; only the hash is ever stored.
    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
