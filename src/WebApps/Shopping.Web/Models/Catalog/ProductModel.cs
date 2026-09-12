namespace Shopping.Web.Models.Catalog;

public sealed class ProductModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public List<string> Category { get; set; } = [];
    public string Description { get; set; } = default!;
    public string ImageFile { get; set; } = default!;
    public decimal Price { get; set; }
}

public sealed record GetProductsResponse(IEnumerable<ProductModel> Products);
public sealed record GetProductByCategoryResponse(IEnumerable<ProductModel> Products);
public sealed record GetProductByIdResponse(ProductModel Product);
