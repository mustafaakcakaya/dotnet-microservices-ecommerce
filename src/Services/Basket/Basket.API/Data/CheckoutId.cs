using BuildingBlocks.Identifiers;

namespace Basket.API.Data;

/// <summary>
/// Derives a checkout's event id from the user and the exact basket version being
/// checked out.
///
/// Two requests racing on the same basket read the same version, derive the same
/// id and so upsert one outbox message instead of writing two - a double click
/// cannot create a second order. A basket filled again after a checkout is a new
/// version, so its checkout gets a new id. The id is also the broker MessageId,
/// which is what Ordering's inbox deduplicates on.
/// </summary>
public static class CheckoutId
{
    private static readonly Guid Namespace = new("3b7f0c52-9a1e-4d63-8f2b-6c4e1a0d9b75");

    public static Guid For(string userName, Guid basketVersion) =>
        DeterministicGuid.Create(Namespace, $"{userName}:{basketVersion:N}");
}
