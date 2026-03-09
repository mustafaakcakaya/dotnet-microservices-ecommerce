namespace Catalog.API.Products.CreateProduct;

public record CreateProductRequest(string Name, List<string> Category, string Description, string ImageFile, decimal Price);

public record CreateProductResponse(Guid Id);

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/products",
                async (CreateProductRequest request, ISender sender, CancellationToken ct) =>
                {
                    var command = request.Adapt<CreateProductCommand>();

                    var result = await sender.Send(command, ct);

                    var response = result.Adapt<CreateProductResponse>();

                    return Results.Created($"/products/{response.Id}", response);
                })
            .WithName("CreateProduct")
            .Produces<CreateProductResponse>(StatusCodes.Status201Created)
            .WithSummary("Create a new product")
            .WithDescription("Create a new product");
    }
}
