using System.ComponentModel.DataAnnotations;

namespace Shopping.Web.Options;

public sealed class ShopperOptions
{
    public const string SectionName = "Shopper";

    [Required]
    public string UserName { get; init; } = default!;

    public Guid CustomerId { get; init; }
}
