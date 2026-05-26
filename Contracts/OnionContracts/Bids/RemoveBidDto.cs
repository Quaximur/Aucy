using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class RemoveBidDto
    {
        [Key(0)] public string BidId { get; set; }

        public RemoveBidDto(string bidId)
        {
            BidId = bidId;
        }
    }
}
