using Marten;
using Payment.API.Cards.TokenizeCard;
using Payment.API.Data;
using Payment.API.Models;
using Testcontainers.PostgreSql;

namespace Payment.API.IntegrationTests;

/// <summary>Against a real PostgreSQL with the service's Marten schema.</summary>
public sealed class TokenizeCardTests : IAsyncLifetime
{
    private const string Cvv = "987";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .Build();

    private IDocumentStore _store = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _store = DocumentStore.For(options =>
            PaymentStoreConfiguration.Configure(options, _container.GetConnectionString()));
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _store.Dispose();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Tokenize_StoresTheCardUnderTheTokenWithoutTheSecurityCode()
    {
        var result = await TokenizeAsync("4111 1111 1111 1111");

        Assert.StartsWith("pt_", result.PaymentToken);
        Assert.Equal("1111", result.CardLast4);

        await using var session = _store.QuerySession();
        var card = await session.LoadAsync<StoredCard>(result.PaymentToken);
        Assert.NotNull(card);
        Assert.Equal("4111111111111111", card.CardNumber);

        // The raw JSON, not just the mapped type: no field may hold the CVV.
        var json = await session.Json.FindByIdAsync<StoredCard>(result.PaymentToken);
        Assert.DoesNotContain(Cvv, json);
        Assert.DoesNotContain("cvv", json, StringComparison.OrdinalIgnoreCase);
    }

    // Tokens are random rather than derived from the card, so they reveal nothing.
    [Fact]
    public async Task Tokenize_TheSameCardTwice_GivesTwoTokens()
    {
        var first = await TokenizeAsync("4111111111111111");
        var second = await TokenizeAsync("4111111111111111");

        Assert.NotEqual(first.PaymentToken, second.PaymentToken);
    }

    private async Task<TokenizeCardResult> TokenizeAsync(string cardNumber)
    {
        await using var session = _store.LightweightSession();
        return await new TokenizeCardCommandHandler(session, TimeProvider.System).Handle(
            new TokenizeCardCommand(Guid.NewGuid(), "Mustafa Akcakaya", cardNumber, "12/28", Cvv),
            CancellationToken.None);
    }
}
