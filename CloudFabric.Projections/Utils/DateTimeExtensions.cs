namespace CloudFabric.Projections.Utils;

public static class DateTimeExtensions
{
    public static DateTime RoundToMicroseconds(this DateTime dt)
    {
        return dt.AddTicks(dt.Nanosecond / 100 * -1);
    }
}
