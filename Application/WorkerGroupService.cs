using Aucy.Contracts.OnionClient;
using Cysharp.Runtime.Multicast;
using System.Collections.Concurrent;

namespace Aucy.Application;

public class WorkerGroupService(IMulticastGroupProvider groupProvider) : IDisposable
{
    private readonly IMulticastAsyncGroup<Guid, IWorkerHubReceiver> _group = groupProvider
        .GetOrAddGroup<Guid, IWorkerHubReceiver>("Worker");

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _userConnectionsMap = new();

    public void NotifySold(Guid userId, Guid lotId, int soldQuantity, long extraMoney)
    {
        if (_userConnectionsMap.TryGetValue(userId, out var connections))
        {
            _group.Only(connections.Keys).OnSold(
                lotId: lotId,
                soldQuantity: soldQuantity,
                extraMoney: extraMoney);
        }
    }

    public void NotifyBought(Guid userId, Guid bidId, int boughtQuantity, long extraMoney)
    {
        if (_userConnectionsMap.TryGetValue(userId, out var connections))
        {
            _group.Only(connections.Keys).OnBought(
                bidId: bidId,
                boughtQuantity: boughtQuantity,
                extraMoney: extraMoney);
        }
    }

    public async ValueTask AddMember(Guid userId, Guid connectionId, IWorkerHubReceiver receiver)
    {
        var connections = _userConnectionsMap.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, byte>());
        connections.TryAdd(connectionId, 0);

        await _group.AddAsync(connectionId, receiver);
    }

    public async ValueTask RemoveMember(Guid userId, Guid connectionId)
    {
        if (_userConnectionsMap.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);

            if (connections.IsEmpty)
                _userConnectionsMap.TryRemove(userId, out _);
        }

        await _group.RemoveAsync(connectionId);
    }

    public void Dispose()
    {
        _group?.Dispose();
    }
}