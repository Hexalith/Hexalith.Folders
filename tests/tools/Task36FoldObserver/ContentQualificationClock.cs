using System.Globalization;

namespace Task36FoldObserver;

/// <summary>Advances only the real SDK content-disposition clock in an explicitly opted-in disposable host.</summary>
internal sealed class ContentQualificationClock(string path) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
        => DateTimeOffset.UtcNow.AddDays(int.Parse(File.ReadAllText(path), CultureInfo.InvariantCulture));
}
