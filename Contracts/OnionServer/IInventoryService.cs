using Aucy.Contracts.OnionClient;
using MagicOnion;
using MessagePack;

namespace Aucy.Contracts.OnionServer
{
    public interface IInventoryService : IService<IInventoryService>
    {
        public UnaryResult<InventoryDto> CreateInventory(CreateInventoryDto request);
        public UnaryResult<Nil> AddItemStack(AddItemStackDto request);
        public UnaryResult<Nil> RemoveItemStack(RemoveItemStackDto request);
        public UnaryResult<Nil> AddMoney(long moneyToAdd);
        public UnaryResult<Nil> RemoveMoney(long moneyToTake);
    }
}