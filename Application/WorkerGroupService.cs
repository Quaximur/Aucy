using Aucy.Contracts.OnionClient;
using Cysharp.Runtime.Multicast;
using System.Collections.Concurrent;

namespace Aucy.Application;

public class WorkerGroupService(IMulticastGroupProvider groupProvider) : IDisposable
{
    private readonly IMulticastAsyncGroup<Guid, IWorkerHubReceiver> _group = groupProvider
        .GetOrAddGroup<Guid, IWorkerHubReceiver>("Worker");

    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _userConnectionsMap = new();

    public void NotifySold(Guid userId, Guid lotId, int soldQuantity, long extraMoney)
    {
        if (_userConnectionsMap.TryGetValue(userId, out var connections))
        {
            _group.Only(connections).OnSold(lotId: lotId,
                soldQuantity: soldQuantity,
                extraMoney: extraMoney);
        }
    }

    public void NotifyBought(Guid userId, Guid bidId, int boughtQuantity, long extraMoney)
    {
        if (_userConnectionsMap.TryGetValue(userId, out var connections))
        {
            _group.Only(connections).OnBought(bidId: bidId,
                boughtQuantity: boughtQuantity,
                extraMoney: extraMoney);
        }
    }

    public async ValueTask AddMember(Guid userId, Guid connectionId, IWorkerHubReceiver receiver)
    {
        if (_userConnectionsMap.TryGetValue(userId, out var userConnections))
            userConnections.Add(connectionId);
        else
            _userConnectionsMap[userId] = [connectionId];

        await _group.AddAsync(connectionId, receiver);
    }

    public async ValueTask RemoveMember(Guid userId, Guid connectionId)
    {
        if (!_userConnectionsMap.TryGetValue(userId, out var userConnections))
        {
            userConnections!.Remove(connectionId);
            if (userConnections.Count == 0)
                _userConnectionsMap.Remove(userId, out _);
        }

        await _group.RemoveAsync(userId);
    }

    public void Dispose()
    {
        _group.Dispose();
    }
}