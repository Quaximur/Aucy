using MessagePack;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class RawItemListingDto
    {
        [Key(0)] public RawPriceLevelTuple[] Levels { get; set; }
    }
}
