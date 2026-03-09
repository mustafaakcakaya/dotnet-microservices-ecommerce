using Catalog.API.Models;
using BuildingBlocks.CQRS;

namespace Catalog.API.Products.CreateProduct;

public record CreateProductCommand(string Name, List<string> Category, string Description, string ImageFile, decimal Price)
    : ICommand<CreateProductResult>;

public record CreateProductResult(Guid Id);

internal class CreateProductHandler : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Category = request.Category,
            Description = request.Description,
            ImageFile = request.ImageFile,
            Price = request.Price,
        };

        //await _productRepository.AddAsync(product, cancellationToken);

        return new CreateProductResult(product.Id);
    }
}
