using System.Collections.Generic;

using UnityEngine;

namespace TarExtractorMod;

/// <summary>
/// Works out whether a position is inside a tar pit location (TarPit1/2/3).
/// Uses the spawned Location objects, so it still works after a pit has been drained.
/// </summary>
internal static class TarPit
{
    private const string LocationPrefix = "TarPit";
    private const float RefreshInterval = 2f;

    private static Location[] _cache = [];
    private static float _lastRefresh = -100f;

    internal static bool IsInTarPit(Vector3 position)
    {
        return Find(position) != null;
    }

    /// <summary>The tar pit the position is in, or null.</summary>
    internal static Location Find(Vector3 position)
    {
        Refresh();

        foreach (Location location in _cache)
        {
            if (location != null && Contains(location, position))
            {
                return location;
            }
        }

        return null;
    }

    /// <summary>Compares on the horizontal plane only.</summary>
    internal static bool Contains(Location location, Vector3 position)
    {
        Vector3 offset = location.transform.position - position;
        offset.y = 0f;
        return offset.magnitude <= location.m_exteriorRadius;
    }

    /// <summary>
    /// Why a Tar Extractor can't be placed here (a translation token), or null if it can: it must be in a tar pit,
    /// and the pit must have room under the Max Per Pit setting.
    /// </summary>
    internal static string PlacementProblem(Vector3 position)
    {
        Location pit = Find(position);
        if (pit == null)
        {
            return "$msg_tarextractor_needstarpit";
        }

        int max = TarExtractorPlugin._maxPerPit.Value;
        return max > 0 && TarExtractor.CountIn(pit) >= max ? "$msg_tarextractor_pitfull" : null;
    }

    private static void Refresh()
    {
        if (Time.time - _lastRefresh < RefreshInterval)
        {
            return;
        }

        _lastRefresh = Time.time;

        List<Location> found = [];
        foreach (Location location in Object.FindObjectsByType<Location>(FindObjectsSortMode.None))
        {
            if (location.name.StartsWith(LocationPrefix, System.StringComparison.Ordinal))
            {
                found.Add(location);
            }
        }
        _cache = [.. found];
    }
}
