namespace Hexalith.Folders.Authorization;

public interface IEffectivePermissionsReadModel
{
    Task<EffectivePermissionsReadModelResult> GetAsync(
        EffectivePermissionsReadModelRequest request,
        CancellationToken cancellationToken = default);
}
