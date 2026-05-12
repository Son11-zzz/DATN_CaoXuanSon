using UnityEngine;

/// <summary>Carries teleport override from a save file across an async scene load.</summary>
public static class GameResumeContext
{
    public static Vector3? PendingWorldPosition { get; private set; }
    public static bool PendingPositionValid { get; private set; }

    public static void SetPendingTeleport(Vector3 worldPosition)
    {
        PendingWorldPosition = worldPosition;
        PendingPositionValid = true;
    }

    /// <returns>True if a position override was consumed.</returns>
    public static bool TryConsumePendingTeleport(out Vector3 worldPosition)
    {
        if (!PendingPositionValid || !PendingWorldPosition.HasValue)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = PendingWorldPosition.Value;
        Clear();
        return true;
    }

    public static void Clear()
    {
        PendingWorldPosition = null;
        PendingPositionValid = false;
    }
}
