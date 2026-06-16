using Aucy.Application.Helpers;
using Aucy.Contracts.OnionClient;
using MagicOnion.Server.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace Aucy.Application;

// OrderHub for User-Server duplex streaming
// WorkerHub for Server-Sent Events
// Order: Subscribe for Worker Group events -> Store Worker Events -> OrderHub.Connent -> Apply OnJoin ->
// -> Apply unapplied Worker Events -> continue listening for new Worker Events

// 2 different Hubs: WorkerHub disconnection leads to events loss. Reconnection needs OrderHub.Join.

[Authorize]
public class OrderHub(WorkerGroupService workerGroupService) : StreamingHubBase<IOrderHub, IOrderHubReceiver>, IOrderHub
{
    private readonly WorkerGroupService _workerGroupService = workerGroupService; // for SSE (global group)

    private IGroup<IOrderHubReceiver>? _room;
    private Guid _userId;

    protected override async ValueTask OnConnected()
    {
        _userId = Context.GetUserId();

        await _workerGroupService.AddMember(_userId, Client);
        
        _room = await Group.AddAsync(_userId.ToString());

        Client.OnJoin(null, null, null);
    }

    protected override async ValueTask OnDisconnected()
    {
        // _room is Disposed by the Hub

        await _workerGroupService.RemoveMember(_userId);
    }

    public ValueTask CollectBid(CollectBidDto request)
    {
        throw new NotImplementedException();
    }

    public ValueTask CollectLot(CollectLotDto request)
    {
        throw new NotImplementedException();
    }

    public ValueTask CreateBid(CreateBidDto request)
    {
        throw new NotImplementedException();
    }

    public ValueTask CreateLot(CreateLotDto request)
    {
        throw new NotImplementedException();
    }

    public ValueTask RemoveBid(RemoveBidDto request)
    {
        throw new NotImplementedException();
    }

    public ValueTask RemoveLot(RemoveLotDto request)
    {
        throw new NotImplementedException();
    }
}
