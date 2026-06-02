using Grpc.Core;
using MagicOnion.Server;
using System.Security.Claims;

namespace Aucy.Application.Helpers;

public static class AuthServiceContextExtensions
{
    public static Guid GetUserId(this ServiceContext context)
    {
        var userIdText = context.CallContext.GetHttpContext()?.User.FindFirstValue("user_id");

        if (string.IsNullOrWhiteSpace(userIdText))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Token is not valid"));

        if (!Guid.TryParse(userIdText, out var userGuid))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Token is not valid"));

        return userGuid;
    }
}
