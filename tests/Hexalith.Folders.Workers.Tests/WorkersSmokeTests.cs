using Hexalith.Folders.Workers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class WorkersSmokeTests
{
    [Fact]
    public void WorkersModuleRemainsPlaceholderOnly() => FoldersWorkersModule.Name.ShouldBe("Hexalith.Folders.Workers");
}
