using Aucy.Contracts.OnionClient;
using Cysharp.Runtime.Multicast;
using StackExchange.Redis;

namespace Aucy.Application;

public class WorkerGroupService(
    IMulticastGroupProvider groupProvider,
    IConnectionMultiplexer redis) : IDisposable
{
    private readonly IMulticastAsyncGroup<Guid, IWorkerHubReceiver> _group = groupProvider
        .GetOrAddGroup<Guid, IWorkerHubReceiver>("Worker");

    private readonly IDatabase _redis = redis.GetDatabase();

    // Redis key: "user-conns:{userId}" → Set of connectionId strings
    private static RedisKey UserConnKey(Guid userId) => $"user_conns_{userId}";
    private static readonly LuaScript RemoveMemberScript = LuaScript.Prepare(@"
        local key = @key
        local member = @member
    
        redis.call('SREM', key, member)
        local remaining = redis.call('SCARD', key)
    
        if remaining == 0 then
            redis.call('DEL', key)
            return 1
        end
        return 0
    ");

    public void NotifySold(Guid userId, Guid lotId, int soldQuantity, long extraMoney)
    {
        Task.Run(async () =>
        {
            var connIds = await GetConnectionIdsAsync(userId);

            // _group.Only(...) fans out across ALL nodes that hold those connectionIds
            // We just need to resolve which connectionIds belong to this user
            if (connIds != null)
                _group.Only(connIds).OnSold(lotId, soldQuantity, extraMoney);
        });
    }

    public void NotifyBought(Guid userId, Guid bidId, int boughtQuantity, long extraMoney)
    {
        Task.Run(async () =>
        {
            var connIds = await GetConnectionIdsAsync(userId);

            if (connIds != null)
                _group.Only(connIds).OnBought(bidId, boughtQuantity, extraMoney);
        });
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
        await _redis.ScriptEvaluateAsync(RemoveMemberScript, new
        {
            key = UserConnKey(userId),
            member = (RedisValue)connectionId.ToString()
        });
    }

    private async Task<IEnumerable<Guid>?> GetConnectionIdsAsync(Guid userId)
    {
        var members = await _redis.SetMembersAsync(UserConnKey(userId));
        if (members == null || members.Length == 0)
            return null;

        return members
            .Select(m => Guid.TryParse((ReadOnlySpan<byte>)m, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value);
    }

    public void Dispose()
    {
        _group?.Dispose();
    }
}