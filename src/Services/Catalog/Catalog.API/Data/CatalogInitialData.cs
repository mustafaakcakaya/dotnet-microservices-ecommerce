using BuildingBlocks.Identifiers;
using Catalog.API.Models;
using Marten.Schema;

namespace Catalog.API.Data;

public class CatalogInitialData : IInitialData
{
    public async Task Populate(IDocumentStore store, CancellationToken cancellation)
    {
        using var session = store.OpenSession();

        if (await session.Query<Product>().AnyAsync(cancellation))
            return;

        // Marten UPSERT will care for existing records
        session.Store<Product>(GetPreconfiguredProducts());
        await session.SaveChangesAsync(cancellation);
    }

    /// <summary>
    /// Namespace for the sample product ids. Deriving them from the product name
    /// keeps them stable across database resets, so documented examples, saved
    /// requests and tests do not break every time the data is recreated.
    /// The Go port uses this same namespace and names, so both produce identical ids.
    /// </summary>
    private static readonly Guid SeedNamespace = new("6f2c1e64-9d0f-4a1e-9c1a-2d1f2b8f4c31");

    private static IEnumerable<Product> GetPreconfiguredProducts() =>
        new List<Product>
        {
            new()
            {
                Id = DeterministicGuid.Create(SeedNamespace, "iPhone X"),
                Name = "iPhone X",
                Description = "This phone is the company's biggest change to its flagship smartphone in years.",
                ImageFile = "product-1.png",
                Price = 950.00M,
                Category = new List<string> { "Smart Phone" }
            },
            new()
            {
                Id = DeterministicGuid.Create(SeedNamespace, "Samsung 10"),
                Name = "Samsung 10",
                Description = "This phone is the company's biggest change to its flagship smartphone in years.",
                ImageFile = "product-2.png",
                Price = 840.00M,
                Category = new List<string> { "Smart Phone" }
            },
            new()
            {
                Id = DeterministicGuid.Create(SeedNamespace, "Huawei Plus"),
                Name = "Huawei Plus",
                Description = "This phone is the company's biggest change to its flagship smartphone in years.",
                ImageFile = "product-3.png",
                Price = 650.00M,
                Category = new List<string> { "Smart Phone" }
            },
            new()
            {
                Id = DeterministicGuid.Create(SeedNamespace, "Xiaomi Mi 9"),
                Name = "Xiaomi Mi 9",
                Description = "This phone is the company's biggest change to its flagship smartphone in years.",
                ImageFile = "product-4.png",
                Price = 470.00M,
                Category = new List<string> { "Smart Phone" }
            }
        };
}
