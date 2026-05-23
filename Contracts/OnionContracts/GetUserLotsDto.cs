using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class GetUserLotsDto
{
    [Key(0)] public string UserId { get; set; }

    public GetUserLotsDto(string userId)
    {
        UserId = userId;
    }
}