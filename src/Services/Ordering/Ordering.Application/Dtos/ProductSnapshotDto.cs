namespace Ordering.Application.Dtos;

/// <summary>A product's name and price at the moment an order was placed.</summary>
public record ProductSnapshotDto(Guid ProductId, string Name, decimal Price);
