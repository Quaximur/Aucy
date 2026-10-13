namespace Aucy.Contracts.OnionClient
{
    public interface IOrderHubReceiver : IWorkerHubReceiver
    {
        public void OnJoin(OwnLotsDto ownLots, OwnBidsDto ownBids, InventoryDto inventory);
        public void OnLotCreated(LotCreatedDto lotCreated);
        public void OnLotCollected(LotCollectedDto lotCollected);
        public void OnLotRemoved(LotRemovedDto lotRemoved);
        public void OnBidCreated(BidCreatedDto bidCreated);
        public void OnBidCollected(BidCollectedDto bidCollected);
        public void OnBidRemoved(BidRemovedDto bidRemoved);
    }
}

