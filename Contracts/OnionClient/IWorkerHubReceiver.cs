using System;

namespace Aucy.Contracts.OnionClient
{
    public interface IWorkerHubReceiver
    {
        void OnSold(Guid lotId, int soldQuantity, long extraMoney);
        void OnBought(Guid bidId, int boughtQuantity, long extraMoney);
    }
}
