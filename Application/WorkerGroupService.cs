using Aucy.Contracts.OnionClient;
using Cysharp.Runtime.Multicast;

namespace Aucy.Application;

public class WorkerGroupService(IMulticastGroupProvider groupProvider) : IDisposable
{
    private readonly IMulticastAsyncGroup<Guid, IWorkerHubReceiver> _group = groupProvider
        .GetOrAddGroup<Guid, IWorkerHubReceiver>("Worker");

    public void NotifySold(Guid userId, Guid lotId, int soldQuantity, long extraMoney)
    {
        _group.Single(userId).OnSold(lotId: lotId,
            soldQuantity: soldQuantity,
            extraMoney: extraMoney);
    }

    public void NotifyBought(Guid userId, Guid bidId, int boughtQuantity, long extraMoney)
    {
        _group.Single(userId).OnBought(bidId: bidId,
            boughtQuantity: boughtQuantity,
            extraMoney: extraMoney);
    }

    public async ValueTask AddMember(Guid userId, IWorkerHubReceiver receiver)
    {
        await _group.AddAsync(userId, receiver);
    }

    public async ValueTask RemoveMember(Guid userId)
    {
        await _group.RemoveAsync(userId);
    }

    public void Dispose()
    {
        _group.Dispose();
    }
}