using Grpc.Core;
namespace Aucy.Application.Exceptions;

public class NoRetryRpcException : RpcException
{
    public NoRetryRpcException(Status status) : base(status)
    {
    }

    public NoRetryRpcException(Status status, string message)
        : base(status, Metadata.Empty, message)
    {
    }
}
