namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Signals that inherited Git configuration makes an isolated native operation inadmissible.
/// </summary>
internal sealed class ForgejoAmbientConfigurationException : Exception
{
}
