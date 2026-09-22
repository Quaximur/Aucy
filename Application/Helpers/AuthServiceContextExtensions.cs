using Grpc.Core;
using Aucy.Application.Constants;
using MagicOnion.Server;
using System.Security.Claims;
using MagicOnion;

namespace Aucy.Application.Helpers;

public static class AuthServiceContextExtensions
{
    public static Guid GetUserId(this ServiceContext context)
    {
        var userIdText = context.CallContext.GetHttpContext()?.User.FindFirstValue("user_id");

        if (string.IsNullOrWhiteSpace(userIdText))
            throw new ReturnStatusException(StatusCode.PermissionDenied, AuctionErrorCodes.INVALID_TOKEN);

        if (!Guid.TryParse(userIdText, out var userGuid))
            throw new ReturnStatusException(StatusCode.PermissionDenied, AuctionErrorCodes.INVALID_TOKEN);

        return userGuid;
    }
}
