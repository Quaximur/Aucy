using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class CreateLotDto
    {
        [Key(0)] public string OwnerUserId { get; set; }
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public long UnitPrice { get; set; }
        [Key(3)] public int ItemQuantity { get; set; }

        public CreateLotDto(string ownerUserId, ItemConfigDto itemConfig, long unitPrice, int itemQuantity)
        {
            OwnerUserId = ownerUserId;
            ItemConfig = itemConfig;
            UnitPrice = unitPrice;
            ItemQuantity = itemQuantity;
        }
    }
}
