namespace Aucy.Application.Constants;

/// <summary>
/// Centralized error code identifiers for RPC exceptions.
/// </summary>
public static class AuctionErrorCodes
{
    // General Validation
    public const string INVALID_PAGE_NUMBER = "INVALID_PAGE_NUMBER";
    public const string INVALID_PAGE_SIZE = "INVALID_PAGE_SIZE";
    public const string INVALID_ARGUMENT = "INVALID_ARGUMENT";

    // Inventory errors
    public const string INVENTORY_NOT_FOUND = "INVENTORY_NOT_FOUND";
    public const string INVENTORY_CAPACITY_EXCEEDED = "INVENTORY_CAPACITY_EXCEEDED";
    public const string INVENTORY_WEIGHT_LIMIT_EXCEEDED = "INVENTORY_WEIGHT_LIMIT_EXCEEDED";
    public const string INSUFFICIENT_INVENTORY = "INSUFFICIENT_INVENTORY";
    public const string INVENTORY_ALREADY_EXISTS = "INVENTORY_ALREADY_EXISTS";
    public const string FAILED_TO_CREATE_INVENTORY = "FAILED_TO_CREATE_INVENTORY";
    public const string FAILED_TO_ADD_MONEY = "FAILED_TO_ADD_MONEY";
    public const string FAILED_TO_TAKE_MONEY = "FAILED_TO_TAKE_MONEY";

    // Item Stack errors
    public const string ITEM_STACK_NOT_FOUND = "ITEM_STACK_NOT_FOUND";
    public const string ITEM_ALREADY_IN_PROCESSING = "ITEM_ALREADY_IN_PROCESSING"; // "Some of your operations with this item is already processing"
    public const string FAILED_TO_ADD_ITEM_STACK = "FAILED_TO_ADD_ITEM_STACK";
    public const string FAILED_TO_REMOVE_ITEMSTACK = "FAILED_TO_REMOVE_ITEMSTACK";

    // Item Config errors (validation)
    public const string INVALID_ITEM_CONFIG = "INVALID_ITEM_CONFIG";
    public const string ITEM_CONFIG_NOT_FOUND = "ITEM_CONFIG_NOT_FOUND"; // "Item config doesn`t exist"
    public const string INVALID_QUANTITY = "INVALID_QUANTITY"; // "Quantity should be greater than 0"
    public const string INVALID_UNIT_PRICE = "INVALID_UNIT_PRICE";
    public const string INVALID_TOTAL_QUANTITY = "INVALID_TOTAL_QUANTITY";
    public const string INVALID_QUANTITY_FOR_UNARY_ITEM = "INVALID_QUANTITY_FOR_UNARY_ITEM"; // "Total quantity should be 1 for unary item"
    public const string INVALID_PRICE_RANGE = "INVALID_PRICE_RANGE"; // $"Price should be within range [{itemConfigDto.MinPrice}, {itemConfigDto.MaxPrice}]"
    public const string INVALID_PRICE_TIER = "INVALID_PRICE_TIER"; // $"Price should belong to one of configured tiers. Provided price {request.BidPrice} is not a valid tier representative."
    public const string AMOUNT_TOO_LARGE = "AMOUNT_TOO_LARGE";

    // Lot errors
    public const string LOT_NOT_FOUND = "LOT_NOT_FOUND"; // "Lot doesn`t exist"
    public const string LOT_ALREADY_EXISTS = "LOT_ALREADY_EXISTS";
    public const string LOT_QUANTITY_EXCEEDS_MAX = "LOT_QUANTITY_EXCEEDS_MAX";
    public const string LOT_PRICE_TOO_LARGE = "LOT_PRICE_TOO_LARGE"; // "Lot total price is too large"
    public const string CANNOT_CREATE_LOT_WHILE_BUYING = "CANNOT_CREATE_LOT_WHILE_BUYING"; // "You can`t create a lot for the item you are buying"
    public const string NO_LOT_MODIFICATION_PERMISSION = "NO_LOT_MODIFICATION_PERMISSION"; // "You have no permission to modify this lot"
    public const string NOTHING_TO_REMOVE_LOT = "NOTHING_TO_REMOVE_LOT"; // "Nothing to remove or Lot doesn`t exist"
    public const string LOT_REMOVING_CONFLICT = "LOT_REMOVING_CONFLICT"; // Lot`s remaining quantity has been changed during operation 
    public const string NOT_ENOUGH_TO_COLLECT_LOT = "NOT_ENOUGH_TO_COLLECT_LOT"; // "Lot doesn`t exist or remaining quantity is less than needed"
    public const string FAILED_TO_CREATE_LOT = "FAILED_TO_CREATE_LOT";
    public const string FAILED_TO_REMOVE_LOT = "FAILED_TO_REMOVE_LOT";
    public const string FAILED_TO_COLLECT_LOT = "FAILED_TO_COLLECT_LOT"; // An error occurred while collecting the lot

    // Bid errors
    public const string BID_NOT_FOUND = "BID_NOT_FOUND";
    public const string BID_ALREADY_EXISTS = "BID_ALREADY_EXISTS"; // "You already have a bid with this item"
    public const string BID_QUANTITY_EXCEEDS_MAX = "BID_QUANTITY_EXCEEDS_MAX"; // $"Can`t create a bid for more than {itemConfigDto.MaxBuyQuantity} items"
    public const string BID_PRICE_TOO_LARGE = "BID_PRICE_TOO_LARGE"; // "Bid total price is too large"
    public const string INVALID_UNARY_BID_PRICE = "INVALID_UNARY_BID_PRICE";// "Bid price should be equal to lot price for unary item"
    public const string QUANTITY_MUST_BE_UNARY = "QUANTITY_MUST_BE_UNARY";
    public const string CANNOT_CREATE_BID_WHILE_SELLING = "CANNOT_CREATE_BID_WHILE_SELLING"; // "You can`t create a bid for the item you are selling"
    public const string NOTHING_TO_REMOVE_BID = "NOTHING_TO_REMOVE_BID"; // "Nothing to remove or Bid doesn`t exist"
    public const string BID_REMOVING_CONFLICT = "BID_REMOVING_CONFLICT"; // "Bid`s remaining quantity has been changed during operation"
    public const string NOT_ENOUGH_TO_COLLECT_BID = "NOT_ENOUGH_TO_COLLECT_BID";  // "There are no purchased items in the bid"
    public const string FAILED_TO_CREATE_BID = "FAILED_TO_CREATE_BID"; // An error occurred while creating the bid
    public const string FAILED_TO_REMOVE_BID = "FAILED_TO_REMOVE_BID"; // An error occurred while removing the lot
    public const string FAILED_TO_COLLECT_BID = "FAILED_TO_COLLECT_BID";

    // Money/Account errors
    public const string INSUFFICIENT_MONEY = "INSUFFICIENT_MONEY";

    // Authentication errors
    public const string INVALID_TOKEN = "INVALID_TOKEN";

    // Internal/System errors
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    public const string LISTING_STREAM_INTERRUPTED = "LISTING_STREAM_INTERRUPTED";
}
