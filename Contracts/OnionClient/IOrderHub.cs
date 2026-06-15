using System;
using System.Threading.Tasks;

namespace Aucy.Contracts.OnionClient;

public interface IOrderHub
{
    // THINK: how to notify client if Worker returns Extra money to the Bidder?
    ValueTask JoinAsync();
    ValueTask CreateLot(CreateLotDto request);
    ValueTask CollectLot(CollectLotDto request);
    ValueTask RemoveLot(RemoveLotDto request);
    ValueTask CreateBid(CreateBidDto request);
    ValueTask CollectBid(CollectBidDto request);
    ValueTask RemoveBid(RemoveBidDto request);
    ValueTask LeaveAsync();
    ValueTask SendMessageAsync(string message);
}

public interface IOrderHubReceiver
{
    void OnJoin(OwnLotsDto ownLots, OwnBidsDto ownBids, InventoryDto inventory);
    void OnLotCreated(LotCreatedDto lotCreated);
    void OnLotCollected(LotCreatedDto lotCreated);
    void OnLotRemoved(LotRemovedDto lotRemoved);
    void OnLeave();
    void OnMessage(string userName, string message);
}

public class LotCreatedDto()
{
    // Remove RemainingQuantity ItemStacks with ItemConfig.Id, decrease Weight and mb UsedCapacity
    public Guid LotId { get; set; }
    public ItemConfigDto ItemConfig { get; set; }
    public long UnitPrice { get; set; }
    public int RemainingQuantity { get; set; }
}

public class LotRemovedDto()
{
    // Add ReturningQuantity ItemStacks with ItemConfig.Id, increase Weight and mb UsedCapacity
    public Guid LotId { get; set; }
    public ItemConfigDto ItemConfig { get; set; }
    public long ReturningMoney { get; set; } // Add to Money
    public int ReturningQuantity { get; set; }
}

public class LotCollectedDto()
{
    public Guid LotId { get; set; }
    public long ReturningMoney { get; set; } // Add to Money
    public int DecreasingTotalQuantity { get; set; }
}