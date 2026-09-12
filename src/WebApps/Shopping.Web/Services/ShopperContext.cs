using Microsoft.Extensions.Options;
using Shopping.Web.Options;

namespace Shopping.Web.Services;

public interface IShopperContext
{
    string UserName { get; }
    Guid CustomerId { get; }
}

public sealed class ShopperContext(IOptions<ShopperOptions> options) : IShopperContext
{
    public string UserName => options.Value.UserName;
    public Guid CustomerId => options.Value.CustomerId;
}
