namespace Hexalith.Folders.Tests.Providers.Forgejo;

internal sealed class ForgejoFixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
