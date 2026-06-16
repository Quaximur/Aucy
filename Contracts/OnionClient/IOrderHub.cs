using MagicOnion;
using System;
using System.Threading.Tasks;

namespace Aucy.Contracts.OnionClient;

public interface IOrderHub : IStreamingHub<IOrderHub, IOrderHubReceiver>
{
    ValueTask CreateLot(CreateLotDto request);
    ValueTask CollectLot(CollectLotDto request);
    ValueTask RemoveLot(RemoveLotDto request);
    ValueTask CreateBid(CreateBidDto request);
    ValueTask CollectBid(CollectBidDto request);
    ValueTask RemoveBid(RemoveBidDto request);
}

public interface IOrderHubReceiver : IWorkerHubReceiver
{
    void OnJoin(OwnLotsDto ownLots, OwnBidsDto ownBids, InventoryDto inventory);
    void OnLotCreated(LotCreatedDto lotCreated);
    void OnLotCollected(LotCollectedDto lotCollected);
    void OnLotRemoved(LotRemovedDto lotRemoved);
    void OnBidCreated(BidCreatedDto bidCreated);
    void OnBidCollected(BidCollectedDto bidCollected);
    void OnBidRemoved(BidRemovedDto bidRemoved);
}

public interface IWorkerHub : IStreamingHub<IWorkerHub, IWorkerHubReceiver>
{
}

public interface IWorkerHubReceiver
{
    void OnSold(Guid lotId, int soldQuantity, long extraMoney);
    void OnBought(Guid bidId, int boughtQuantity, long extraMoney);
}

public class LotCreatedDto()
{
    // Remove TotalQuantity ItemStacks with ItemConfig.Id, decrease Weight and mb UsedCapacity
    public Guid LotId { get; set; }
    public ItemConfigDto ItemConfig { get; set; }
    public long UnitPrice { get; set; }
    public int TotalQuantity { get; set; }
}

public class LotCollectedDto()
{
    public Guid LotId { get; set; }
    public long ReturningMoney { get; set; } // Add to Money
    public int DecreasingTotalQuantity { get; set; }
}

public class LotRemovedDto()
{
    // Add ReturningQuantity ItemStacks with ItemConfig.Id, increase Weight and mb UsedCapacity
    public Guid LotId { get; set; }
    public ItemConfigDto ItemConfig { get; set; }
    public long ReturningMoney { get; set; } // Add to Money
    public int ReturningQuantity { get; set; }
}

public class BidCreatedDto()
{
    // Remove RemainingQuantity ItemStacks with ItemConfig.Id, decrease Weight and mb UsedCapacity
    public Guid LotId { get; set; }
    public ItemConfigDto ItemConfig { get; set; }
    public long BidPrice { get; set; }
    public int TotalQuantity { get; set; }
}

public class BidCollectedDto()
{
    public Guid LotId { get; set; }
    public ItemConfigDto ItemConfig { get; set; }
    public int CollectedQuantity { get; set; } // Add to Inventory
    public int DecreasingTotalQuantity { get; set; }
}

public class BidRemovedDto()
{
    // Add ReturningQuantity ItemStacks with ItemConfig.Id, increase Weight and mb UsedCapacity
    public Guid LotId { get; set; }
    public ItemConfigDto ItemConfig { get; set; }
    public long ReturningMoney { get; set; } // Add to Money
    public int ReturningQuantity { get; set; }
}
