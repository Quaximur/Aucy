using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class LotWithItemConfigDto : LotDto
    {
        [Key(6)] public ItemConfigDto ItemConfig { get; set; }

        public LotWithItemConfigDto(Guid id, 
            Guid ownerUserId, 
            Guid itemConfigId, 
            long unitPrice, 
            int itemQuantity, 
            DateTime createdAt, 
            ItemConfigDto itemConfig) : 
            
            base(id:  id, 
                ownerUserId: 
                ownerUserId, 
                itemConfigId: itemConfigId, 
                unitPrice: unitPrice, 
                itemQuantity: itemQuantity, 
                createdAt: createdAt)
        {
            ItemConfig = itemConfig;
        }
    }
}
