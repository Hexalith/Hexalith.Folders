using Hexalith.EventStore.DomainService.DurableTasks;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Admits only the synthetic, recorded fixture actor and scope.</summary>
internal sealed class ProvisioningQualificationAdmission : IDurableTaskAdmissionAuthorizer
{
    public ValueTask<DurableTaskAdmissionAuthority> AuthorizeAsync(DurableTaskScope scope, string requestSha256,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool allowed = scope.TenantId == ProvisioningQualificationFixture.Tenant
            && scope.ActorId == ProvisioningQualificationFixture.Actor
            && scope.Operation == ProvisioningQualificationFixture.Operation;
        return ValueTask.FromResult(new DurableTaskAdmissionAuthority(allowed, false, null));
    }
}
