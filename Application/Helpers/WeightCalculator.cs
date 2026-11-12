namespace Aucy.Application.Helpers;

public static class WeightCalculator
{
    /// <summary>
    /// Float maths ignores <c>checked</c>, so overflow (Infinity/NaN) is detected manually.
    /// </summary>
    public static float Calculate(float unitWeight, int quantity)
    {
        var weight = unitWeight * quantity;

        if (!float.IsFinite(weight))
            throw new OverflowException();

        return weight;
    }
}
