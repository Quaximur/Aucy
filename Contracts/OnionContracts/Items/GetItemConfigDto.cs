using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class GetItemConfigDto
    {
        [Key(0)] public string ItemConfigId { get; set; }

        public GetItemConfigDto(string itemConfigId)
        {
            ItemConfigId = itemConfigId;
        }
    }
}
