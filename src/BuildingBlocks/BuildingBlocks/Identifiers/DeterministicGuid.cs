using System.Security.Cryptography;
using System.Text;

namespace BuildingBlocks.Identifiers;

/// <summary>
/// Creates RFC 4122 version 5 (name based) GUIDs.
/// Seed data derives its ids this way so a reset database always produces the
/// same values: examples, saved requests and tests keep working, and seeding
/// stays idempotent.
/// </summary>
public static class DeterministicGuid
{
    /// <summary>
    /// Derives a stable GUID from a namespace and a name. The same pair always
    /// yields the same GUID, on any platform - the Go port of this project
    /// derives its seed ids from the identical namespace and names.
    /// </summary>
    public static Guid Create(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var namespaceBytes = namespaceId.ToByteArray();
        // .NET lays the first three fields out little-endian; RFC 4122 hashes
        // them in network order, so both the input and the result are swapped.
        SwapByteOrder(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);

        var payload = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(payload, 0);
        nameBytes.CopyTo(payload, namespaceBytes.Length);

        // SHA-1 is mandated by the version 5 algorithm; it is a naming scheme
        // here, not a security primitive.
        var hash = SHA1.HashData(payload);

        var result = new byte[16];
        Array.Copy(hash, result, 16);

        result[6] = (byte)((result[6] & 0x0F) | 0x50); // version 5
        result[8] = (byte)((result[8] & 0x3F) | 0x80); // RFC 4122 variant

        SwapByteOrder(result);

        return new Guid(result);
    }

    private static void SwapByteOrder(byte[] guid)
    {
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        (guid[4], guid[5]) = (guid[5], guid[4]);
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }
}
