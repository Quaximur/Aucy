using Grpc.Core;
using MagicOnion;
namespace Aucy.Application.Exceptions;

public class NoRetryReturnStatusException : ReturnStatusException
{
    public NoRetryReturnStatusException(StatusCode statusCode, string detail) : base(statusCode, detail)
    {
    }
}
