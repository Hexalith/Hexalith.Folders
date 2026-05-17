namespace Hexalith.Folders.Client.Tests;

internal static class EnumerableExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> values)
        where T : class
    {
        foreach (T? value in values)
        {
            if (value is not null)
            {
                yield return value;
            }
        }
    }
}
