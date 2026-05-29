using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class CreateLotDto
    {
        [Key(0)] public ItemConfigDto ItemConfig { get; set; }
        [Key(1)] public long UnitPrice { get; set; }
        [Key(2)] public int TotalQuantity { get; set; }

        public CreateLotDto(ItemConfigDto itemConfig, long unitPrice, int totalQuantity)
        {
            ItemConfig = itemConfig;
            UnitPrice = unitPrice;
            TotalQuantity = totalQuantity;
        }
    }
}
