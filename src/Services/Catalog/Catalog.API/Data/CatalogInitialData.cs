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

    private static IEnumerable<Product> GetPreconfiguredProducts() =>
        new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "iPhone X",
                Description = "This phone is the company's biggest change to its flagship smartphone in years.",
                ImageFile = "product-1.png",
                Price = 950.00M,
                Category = new List<string> { "Smart Phone" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Samsung 10",
                Description = "This phone is the company's biggest change to its flagship smartphone in years.",
                ImageFile = "product-2.png",
                Price = 840.00M,
                Category = new List<string> { "Smart Phone" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Huawei Plus",
                Description = "This phone is the company's biggest change to its flagship smartphone in years.",
                ImageFile = "product-3.png",
                Price = 650.00M,
                Category = new List<string> { "Smart Phone" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Xiaomi Mi 9",
                Description = "This phone is the company's biggest change to its flagship smartphone in years.",
                ImageFile = "product-4.png",
                Price = 470.00M,
                Category = new List<string> { "Smart Phone" }
            }
        };
}
