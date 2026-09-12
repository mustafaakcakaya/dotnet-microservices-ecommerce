namespace Shopping.Web.Services;

public interface ICatalogService
{
    [Get("/catalog-service/products")]
    Task<GetProductsResponse> GetProducts(
        [AliasAs("pageNumber")] int? pageNumber = 1,
        [AliasAs("pageSize")] int? pageSize = 10,
        CancellationToken cancellationToken = default);

    [Get("/catalog-service/products/{id}")]
    Task<GetProductByIdResponse> GetProduct(Guid id, CancellationToken cancellationToken = default);

    [Get("/catalog-service/products/category/{category}")]
    Task<GetProductByCategoryResponse> GetProductsByCategory(
        string category,
        CancellationToken cancellationToken = default);
}
