using UnityEngine;

namespace TarExtractorMod
{
    /// <summary>
    /// Works out whether a position is inside a tar pit location (TarPit1/2/3).
    /// Uses the spawned Location objects, so it still works after a pit has been drained.
    /// </summary>
    internal static class TarPit
    {
        private const string LocationPrefix = "TarPit";
        private const float RefreshInterval = 2f;

        private static Location[] _cache = new Location[0];
        private static float _lastRefresh = -100f;

        internal static bool IsInTarPit(Vector3 position)
        {
            Refresh();

            foreach (Location location in _cache)
            {
                if (location == null) continue;

                // Compare on the horizontal plane only.
                Vector3 offset = location.transform.position - position;
                offset.y = 0f;
                if (offset.magnitude <= location.m_exteriorRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Refresh()
        {
            if (Time.time - _lastRefresh < RefreshInterval) return;
            _lastRefresh = Time.time;

            var found = new System.Collections.Generic.List<Location>();
            foreach (Location location in Object.FindObjectsByType<Location>(FindObjectsSortMode.None))
            {
                if (location.name.StartsWith(LocationPrefix))
                {
                    found.Add(location);
                }
            }
            _cache = found.ToArray();
        }
    }
}
