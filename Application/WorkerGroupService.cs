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

    public void Dispose()
    {
        _group?.Dispose();
    }
}