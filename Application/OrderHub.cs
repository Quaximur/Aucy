using Aucy.Application.Exceptions;
using Aucy.Application.Helpers;
using Aucy.Contracts.OnionClient;
using Aucy.Data;
using Aucy.Domain.Entities;
using AutoMapper;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Medallion.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Data;

namespace Aucy.Application;

// OrderHub for User-Server duplex streaming
// WorkerHub for Server-Sent Events
// Order: Subscribe for Worker Group events -> Store Worker Events -> OrderHub.Connent -> Apply OnJoin ->
// -> Apply unapplied Worker Events -> continue listening for new Worker Events

// 2 different Hubs: WorkerHub disconnection leads to events loss. Reconnection needs OrderHub.Join.

[Authorize]
public class OrderHub(WorkerGroupService workerGroupService,
    IOrderBookService orderBookService,
    IDbContextFactory<AucyDbContext> aucyDbFactory,
    IMapper mapper) : StreamingHubBase<IOrderHub, IOrderHubReceiver>, IOrderHub
{
    private readonly WorkerGroupService _workerGroupService = workerGroupService; // for SSE (global group)
    private readonly IOrderBookService _orderBookService = orderBookService;
    private readonly IDbContextFactory<AucyDbContext> _aucyDbFactory = aucyDbFactory;
    private readonly IMapper _mapper = mapper;

    //private IGroup<IOrderHubReceiver>? _room;
    private Guid _userId;

    protected override async ValueTask OnConnected()
    {
        _userId = Context.GetUserId();

        await _workerGroupService.AddMember(_userId, ConnectionId, Client);

        //_room = await Group.AddAsync(_userId.ToString());

        using var aucyDb = await _aucyDbFactory.CreateDbContextAsync();

        var inventory = await aucyDb.Inventories
                .Where(x => x.Id == _userId)
                .Select(x => new InventoryDto
                {
                    Capacity = x.Capacity,
                    UsedCapacity = x.UsedCapacity,
                    WeightLimit = x.WeightLimit,
                    UsedWeight = x.UsedWeight,
                    ItemStacks = x.ItemStacks
                        .OrderBy(x => x.CreatedAt)
                        .Select(i => new ItemStackDto
                        {
                            ItemConfig = _mapper.Map<ItemConfigDto>(i.Config),
                            Quantity = i.Quantity,
                        })
                        .ToArray()
                })
                .FirstOrDefaultAsync() ?? throw new RpcException(new Status(StatusCode.NotFound,
                    "Inventory doesn't exist"));

        var lotsWithItemConfigs = await aucyDb.Lots
           .Where(l => l.OwnerUserId == _userId)
           .Include(l => l.ItemConfig)
           .Select(l => _mapper.Map<OwnLotDto>(l))
           .ToListAsync();

        var lots = new OwnLotsDto(lotsWithItemConfigs);

        var ownBids = await aucyDb.Bids
            .Where(b => b.BidderUserId == _userId)
            .Include(b => b.ItemConfig)
            .Select(b => _mapper.Map<OwnBidDto>(b))
            .ToListAsync();

        var bids = new OwnBidsDto(ownBids);

        Client.OnJoin(lots, bids, inventory);
    }

    protected override async ValueTask OnDisconnected()
    {
        // _room is Disposed by the Hub

        await _workerGroupService.RemoveMember(_userId, ConnectionId);
    }

    public async ValueTask CreateBid(CreateBidDto request)
    {
        var bidCreated = await _orderBookService.CreateBid(request);

        Client.OnBidCreated(bidCreated);
    }

    public async ValueTask CollectBid(CollectBidDto request)
    {
        var bidCollected = await _orderBookService.CollectBid(request);

        Client.OnBidCollected(bidCollected);
    }

    public async ValueTask RemoveBid(RemoveBidDto request)
    {
        var bidRemoved = await _orderBookService.RemoveBid(request);

        Client.OnBidRemoved(bidRemoved);
    }

    public async ValueTask CreateLot(CreateLotDto request)
    {
        var lotCreated = await _orderBookService.CreateLot(request);

        Client.OnLotCreated(lotCreated);
    }

    public async ValueTask CollectLot(CollectLotDto request)
    {
        var lotCollected = await _orderBookService.CollectLot(request);

        Client.OnLotCollected(lotCollected);
    }

    public async ValueTask RemoveLot(RemoveLotDto request)
    {
        var lotRemoved = await _orderBookService.RemoveLot(request);

        Client.OnLotRemoved(lotRemoved);
    }
}
