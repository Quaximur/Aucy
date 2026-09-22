using Aucy.Contracts.OnionClient;
using Aucy.Application.Constants;
using Aucy.Data;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Aucy.Application;

[Authorize]
public class ItemSearchService(AucyDbContext aucyDbContext, 
    IMapper mapper, 
    ILogger<ItemSearchService> logger) : ServiceBase<IItemSearchService>,
#pragma warning disable CS8644 
    IItemSearchService
#pragma warning restore CS8644
{
    private readonly AucyDbContext _aucyDb = aucyDbContext;
    private readonly IMapper _mapper = mapper;
    private readonly ILogger<ItemSearchService> _logger = logger;

    public async UnaryResult<ItemConfigsDto> GetItemConfigs(GetItemConfigsDto request)
    {
        if (request.Page <= 0)
            throw new ReturnStatusException(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_PAGE_NUMBER);

        if (request.PageSize <= 0)
            throw new ReturnStatusException(StatusCode.InvalidArgument, AuctionErrorCodes.INVALID_PAGE_SIZE);

        var query = _aucyDb.ItemConfigs.AsQueryable();

        // TODO: process path filter correctly (e.g. with splitting by '>' and checking each part separately, or with regex)
        // CAUTION: with substring filter, "Weapon>Sword" will also match "Weapon>SwordAndShield", which may not be desired
        if (!string.IsNullOrWhiteSpace(request.CategoryPathFilter))
            query = query.Where(x => x.CategoryPath.Contains(request.CategoryPathFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.NameFilter))
            query = query.Where(x => x.Name.Contains(request.NameFilter, StringComparison.OrdinalIgnoreCase));

        var itemConfigsList = await query
            .Where(x => !x.IsUnary || x.Lots.Any()) // No zombie configs
            .OrderBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => _mapper.Map<ItemConfigDto>(x))
            .ToListAsync();

        return new ItemConfigsDto(itemConfigsList);
    }
}