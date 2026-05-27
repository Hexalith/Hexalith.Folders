namespace Hexalith.Folders.Queries.Folders;

public interface IBranchRefPolicyReadModel
{
    Task<BranchRefPolicyReadModelResult> GetAsync(
        BranchRefPolicyReadModelRequest request,
        CancellationToken cancellationToken = default);
}
