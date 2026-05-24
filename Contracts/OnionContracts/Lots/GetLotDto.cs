using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class GetLotDto
{
    [Key(0)] public string LotId { get; set; }

    public GetLotDto(string lotId)
    {
        LotId = lotId;
    }
}