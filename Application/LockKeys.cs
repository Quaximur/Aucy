namespace Aucy.Application;

public static class LockKeys
{
    /// <summary>
    /// Queue with worker. NonUnary only
    /// </summary>
    public static string GetItemMatchingLock(Guid itemConfigId) => $"item_matching_{itemConfigId}";

    /// <summary>
    /// Queue for Unary items processing. No worker processing
    /// </summary>
    public static string GetUnaryLock(Guid itemConfigId) => $"item_unary_{itemConfigId}";

    /// <summary>
    /// Queue for user individual operations with non-unary items processing (like buying/selling non-unary items, etc.)
    /// </summary>
    public static string GetNonUnaryUserLock(Guid itemConfigId, Guid userId) => $"item_non_unary_user_{itemConfigId}_{userId}";

    /// <summary>
    /// Queue for user individual operations with unary items processing
    /// </summary>
    public static string GetUnaryUserLock(Guid itemConfigId, Guid userId) => $"item_unary_user_{itemConfigId}_{userId}";
}
