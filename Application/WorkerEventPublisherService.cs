using Grpc.Core;
using Aucy.Contracts.ProtoServer;

namespace Aucy.Application;

public class WorkerEventPublisherService(WorkerGroupService workerGroupService)
    : WorkerEventPublisher.WorkerEventPublisherBase
{
    public override Task<PublishAck> PublishSold(SoldEvent request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || !Guid.TryParse(request.LotId, out var lotId))
            return Task.FromResult(new PublishAck { Success = false, Error = "invalid guid" });

        workerGroupService.NotifySold(userId, lotId, request.SoldQuantity, request.ExtraMoney);
        return Task.FromResult(new PublishAck { Success = true });
    }

    public override Task<PublishAck> PublishBought(BoughtEvent request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || !Guid.TryParse(request.BidId, out var bidId))
            return Task.FromResult(new PublishAck { Success = false, Error = "invalid guid" });

        workerGroupService.NotifyBought(userId, bidId, request.BoughtQuantity, request.ExtraMoney);
        return Task.FromResult(new PublishAck { Success = true });
    }
}
