using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class CreateLotDto
    {
        [Key(0)] public string OwnerUserId { get; set; }
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public long ItemConfigPrice { get; set; }
        [Key(3)] public int ItemConfigAmount { get; set; }

        public CreateLotDto(string ownerUserId, ItemConfigDto itemConfig, long itemConfigPrice, int itemConfigAmount)
        {
            OwnerUserId = ownerUserId;
            ItemConfig = itemConfig;
            ItemConfigPrice = itemConfigPrice;
            ItemConfigAmount = itemConfigAmount;
        }
    }
}
