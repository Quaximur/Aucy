using Aucy.Contracts.OnionClient;
using Aucy.Domain.Entities;
using AutoMapper;

namespace Aucy.Application.Mappings;

public class LotMapping : Profile
{
    public LotMapping()
    {
        CreateMap<Lot, LotDto>()
            .ConstructUsing(src => new LotDto(
                id: src.Id,
                ownerUserId: src.OwnerUserId,
                itemConfigId: src.ItemConfigId,
                unitPrice: src.UnitPrice,
                remainingQuantity: src.RemainingQuantity
            ));

        CreateMap<Lot, OwnLotDto>()
            .ConstructUsing(src => new OwnLotDto(
                id: src.Id,
                ownerUserId: src.OwnerUserId,
                itemConfigId: src.ItemConfigId,
                unitPrice: src.UnitPrice,
                remainingQuantity: src.RemainingQuantity,
                totalQuantity: src.TotalQuantity,
                createdAt: src.CreatedAt,
                itemConfig: null,
                extraSoldoutMoney: src.ExtraSoldoutMoney
            ))
            .ForMember(dest => dest.ItemConfig, opt =>
            {
                opt.Condition(src => src.ItemConfig != null && src.ItemConfig.IsUnary);
                opt.MapFrom(src => src.ItemConfig);
            });
    }
}