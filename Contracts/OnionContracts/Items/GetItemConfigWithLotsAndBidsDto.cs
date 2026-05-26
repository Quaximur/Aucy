using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class GetItemConfigWithLotsAndBidsDto
    {
        [Key(0)] public string ItemConfigId { get; set; }

        public GetItemConfigWithLotsAndBidsDto(string itemConfigId)
        {
            ItemConfigId = itemConfigId;
        }
    }
}
