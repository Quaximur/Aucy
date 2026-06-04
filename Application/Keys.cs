namespace Aucy.Application;

public static class Keys
{
    public static string GetItemMatchingLock(Guid itemConfigId) => $"item_matching_{itemConfigId}";
}
