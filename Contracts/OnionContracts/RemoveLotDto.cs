using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class RemoveLotDto
{
    [Key(0)] public string LotId { get; set; }
    [Key(1)] public string OwnerUserId { get; set; }

    public RemoveLotDto(string lotId, string ownerUserId)
    {
        LotId = lotId;
        OwnerUserId = ownerUserId;
    }
}
