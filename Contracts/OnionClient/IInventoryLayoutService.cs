using MagicOnion;

namespace Aucy.Contracts.OnionClient
{
    public interface IInventoryLayoutService : IService<IInventoryLayoutService>
    {
        public UnaryResult<InventoryDto> GetOwnInventory(GetInventoryDto request);
    }
}
