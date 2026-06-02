using Grpc.Core;
namespace Aucy.Application.Exceptions;

public class RetryRpcException : RpcException
{
    public RetryRpcException(Status status) : base(status)
    {
    }

    public RetryRpcException(Status status, string message)
        : base(status, Metadata.Empty, message)
    {
    }
}
