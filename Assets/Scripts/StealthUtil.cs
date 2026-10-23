using UnityEngine;

// Small shared helper: a raycast that returns the NEAREST hit while skipping colliders
// that belong to the objects we want to ignore (the guard itself, the player, the hidden object...).
public static class StealthUtil
{
    static readonly RaycastHit[] buffer = new RaycastHit[32];

    public static bool FirstHit(Vector3 origin, Vector3 direction, float distance,
                                Transform ignoreA, Transform ignoreB, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;

        int n = Physics.RaycastNonAlloc(origin, direction, buffer, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Transform t = buffer[i].collider.transform;
            if (ignoreA != null && t.IsChildOf(ignoreA)) continue;
            if (ignoreB != null && t.IsChildOf(ignoreB)) continue;

            if (buffer[i].distance < bestDist)
            {
                bestDist = buffer[i].distance;
                best = buffer[i];
                found = true;
            }
        }
        return found;
    }
}