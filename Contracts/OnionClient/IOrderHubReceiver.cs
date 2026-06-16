namespace Aucy.Contracts.OnionClient
{
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
}

