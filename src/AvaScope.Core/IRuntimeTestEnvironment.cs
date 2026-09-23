using AvaScope.Protocol;

namespace AvaScope.Core;

internal interface IRuntimeTestEnvironment : IAsyncDisposable
{
    IReadOnlyDictionary<string, string> EnvironmentVariables { get; }
    CancellationToken FailureToken { get; }
    ProtocolError? UnexpectedExit { get; }
    RuntimeEnvironmentEvidence Evidence { get; }
    Task<CoreResult<RuntimeEnvironmentEvidence>> StartAsync(CancellationToken cancellationToken = default);
    void RetainForRecovery();
}
