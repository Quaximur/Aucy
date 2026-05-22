using AuctionGrpc;
using Grpc.Core;

namespace Aucy.Application;

public class AuctionService : AuctionGrpc.AuctionService.AuctionServiceBase
{
    public override Task<BuyoutLotResponse> BuyoutLot(BuyoutLotRequest request, ServerCallContext context)
    {
        return base.BuyoutLot(request, context);
    }

    public override Task<CreateBidResponse> CreateBid(CreateBidRequest request, ServerCallContext context)
    {
        return base.CreateBid(request, context);
    }

    public override Task<CreateLotResponse> CreateLot(CreateLotRequest request, ServerCallContext context)
    {
        return base.CreateLot(request, context);
    }

    public override Task<GetLotInfoResponse> GetLotInfo(GetLotInfoRequest request, ServerCallContext context)
    {
        return base.GetLotInfo(request, context);
    }

    public override Task<GetLotsResponse> GetLots(GetLotsRequest request, ServerCallContext context)
    {
        return base.GetLots(request, context);
    }

    public override Task<GetOwnLotsResponse> GetMyLots(GetOwnLotsRequest request, ServerCallContext context)
    {
        return base.GetMyLots(request, context);
    }

    public override Task<RemoveLotResponse> RemoveLot(RemoveLotRequest request, ServerCallContext context)
    {
        return base.RemoveLot(request, context);
    }
}
