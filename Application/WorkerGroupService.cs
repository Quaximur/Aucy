using Aucy.Contracts.OnionClient;
using Cysharp.Runtime.Multicast;
using StackExchange.Redis;

namespace Aucy.Application;

public class WorkerGroupService(
    IMulticastGroupProvider groupProvider,
    IConnectionMultiplexer redis) : IAsyncDisposable
{
    private readonly IMulticastAsyncGroup<Guid, IWorkerHubReceiver> _group = groupProvider
        .GetOrAddGroup<Guid, IWorkerHubReceiver>("Worker");

    private readonly IDatabase _redis = redis.GetDatabase();

    // Redis key: "user-conns:{userId}" → Set of connectionId strings
    private static RedisKey UserConnKey(Guid userId) => $"user-conns:{userId}";

    public void NotifySold(Guid userId, Guid lotId, int soldQuantity, long extraMoney)
    => Task.Run(() => NotifyUserAsync(userId, g => g.OnSold(lotId, soldQuantity, extraMoney)));

    public void NotifyBought(Guid userId, Guid bidId, int boughtQuantity, long extraMoney)
        => Task.Run(() => NotifyUserAsync(userId, g => g.OnBought(bidId, boughtQuantity, extraMoney)));

    private async Task NotifyUserAsync(Guid userId, Action<IWorkerHubReceiver> notify)
    {
        // _group.Only(...) fans out across ALL nodes that hold those connectionIds
        // We just need to resolve which connectionIds belong to this user
        var connIds = await GetConnectionIdsAsync(userId);
        if (connIds.Length > 0)
            notify(_group.Only(connIds));
    }

    public async ValueTask AddMember(Guid userId, Guid connectionId, IWorkerHubReceiver receiver)
    {
        // Register the receiver in the distributed group
        await _group.AddAsync(connectionId, receiver);

        // Track connectionId under this userId in Redis
        await _redis.SetAddAsync(UserConnKey(userId), connectionId.ToString());
    }

    public async ValueTask RemoveMember(Guid userId, Guid connectionId)
    {
        await _group.RemoveAsync(connectionId);
        await _redis.SetRemoveAsync(UserConnKey(userId), connectionId.ToString());

        // Clean up the key if the set is now empty
        //var remaining = await _redis.SetLengthAsync(UserConnKey(userId));
        //if (remaining == 0)
        //    await _redis.KeyDeleteAsync(UserConnKey(userId));
    }

    private async Task<Guid[]> GetConnectionIdsAsync(Guid userId)
    {
        var members = await _redis.SetMembersAsync(UserConnKey(userId));
        return members
            .Select(m => Guid.TryParse((ReadOnlySpan<byte>)m, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_group is IAsyncDisposable d)
            await d.DisposeAsync();
    }
}