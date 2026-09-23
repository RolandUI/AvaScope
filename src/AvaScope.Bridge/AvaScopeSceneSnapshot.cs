using Avalonia;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

/// <summary>
/// A synchronous UI-thread declaration of a custom scene. Revision must change whenever its
/// semantic state or camera changes; each object generation must change when an id is reused.
/// Capture should return quickly and without UI side effects. Only the first 256 objects are scanned.
/// </summary>
public sealed record AvaScopeSceneSnapshot(string Revision, IReadOnlyList<RuntimeSceneObject> Objects,
    Matrix SceneToCanvas, bool IsComplete = true, string? UnavailableReason = null);
