using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aucy.Tests.Integration;

public sealed class TradingHouseConcurrencyTests(TradingBackendFixture fixture)
    : IClassFixture<TradingBackendFixture>
{
    [Fact]
    public async Task UsersCanCollectMatchedBidAndLotQuantities()
    {
        var seller = fixture.Users[0];
        var buyer = fixture.Users[1];
        var itemConfig = fixture.ItemConfigs[0];
        var helper = fixture.Services.GetRequiredService<IOrderBookHelper>();
        const long price = 100;
        const int quantity = 25;

        var lot = await helper.CreateLot(seller.UserId, new CreateLotDto(itemConfig.Id, price, quantity));
        var bid = await helper.CreateBid(buyer.UserId, new CreateBidDto(itemConfig.Id, price, quantity));

        await using (var matchScope = fixture.Services.CreateAsyncScope())
        {
            var db = matchScope.ServiceProvider.GetRequiredService<AucyDbContext>();

            await db.Lots
                .Where(x => x.Id == lot.LotId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(l => l.RemainingQuantity, l => l.RemainingQuantity - quantity)
                    .SetProperty(l => l.IsMatched, true));

            await db.Bids
                .Where(x => x.Id == bid.BidId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(b => b.RemainingQuantity, b => b.RemainingQuantity - quantity)
                    .SetProperty(b => b.IsMatched, true));
        }

        var collectedBid = await helper.CollectBid(buyer.UserId, new CollectBidDto(bid.BidId, quantity));
        var collectedLot = await helper.CollectLot(seller.UserId, new CollectLotDto(lot.LotId, quantity));

        Assert.Equal(quantity, collectedBid.CollectedQuantity);
        Assert.Equal(quantity, collectedLot.CollectedQuantity);
        Assert.Equal(price * quantity, collectedLot.ReturningMoney);
    }

    private static long PriceForIndex(int index)
    {
        return 50 + index % 20 * 50;
    }

    private sealed record OrderHandle(OrderKind Kind, Guid UserId, Guid OrderId)
    {
        public static OrderHandle Lot(Guid userId, Guid lotId) => new(OrderKind.Lot, userId, lotId);
        public static OrderHandle Bid(Guid userId, Guid bidId) => new(OrderKind.Bid, userId, bidId);
    }

    private enum OrderKind
    {
        Lot,
        Bid
    }
}

