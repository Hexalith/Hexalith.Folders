using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class FoldersTenantEventOptionsValidator : IValidateOptions<FoldersTenantEventOptions>
{
    public ValidateOptionsResult Validate(string? name, FoldersTenantEventOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Enum.IsDefined(options.ProjectionWriter)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Folders tenant-event ProjectionWriter must be one of {string.Join(", ", Enum.GetNames<FoldersTenantEventProjectionWriter>())}.");
    }
}
