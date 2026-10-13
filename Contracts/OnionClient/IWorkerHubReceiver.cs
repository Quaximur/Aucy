using System;

namespace Aucy.Contracts.OnionClient
{
    public interface IWorkerHubReceiver
    {
        public void OnSold(Guid lotId, int soldQuantity, long extraMoney);
        public void OnBought(Guid bidId, int boughtQuantity, long extraMoney);
    }
}
