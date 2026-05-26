using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class ItemConfigWithLotsAndBidsDto
    {
        [Key(0)] public string Id { get; set; }
        [Key(1)] public string Definition { get; set; }
        [Key(2)] public string Name { get; set; }
        [Key(3)] public List<LotDto> Lots { get; set; }
        [Key(4)] public List<BidDto> Bids { get; set; }

        public ItemConfigWithLotsAndBidsDto(string id, string definition, string name, List<LotDto> lots, List<BidDto> bids)
        {
            Id = id;
            Definition = definition;
            Name = name;
            Lots = lots;
            Bids = bids;
        }
    }
}
