using MagicOnion;
using MessagePack;

namespace Aucy.Contracts.OnionContracts
{
    public interface IInventoryService : IService<IInventoryService>
    {
        public UnaryResult<InventoryDto> CreateInventory(CreateInventoryDto request);
        public UnaryResult<InventoryDto> GetOwnInventory(GetInventoryDto request);
        public UnaryResult<Nil> AddItemStack(AddItemStackDto request);
        public UnaryResult<Nil> RemoveItemStack(RemoveItemStackDto request);
        public UnaryResult<long> AddMoney(long moneyToAdd);
        public UnaryResult<long> RemoveMoney(long moneyToTake);
    }
}