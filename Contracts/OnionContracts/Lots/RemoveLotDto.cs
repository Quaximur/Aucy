using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class RemoveLotDto
    {
        [Key(0)] public string LotId { get; set; }

        public RemoveLotDto(string lotId)
        {
            LotId = lotId;
        }
    }
}
