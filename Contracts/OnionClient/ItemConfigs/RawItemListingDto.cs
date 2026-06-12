using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class RawItemListingDto
    {
        [Key(0)] public RawLotTuple[] RawLots { get; set; }
        [Key(1)] public RawBidTuple[] RawBids { get; set; }
    }
}
