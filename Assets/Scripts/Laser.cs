using UnityEngine;

// Added automatically by LaserSpawner to each spawned laser.
public class Laser : MonoBehaviour
{
    float speed;
    int damage;
    Vector3 direction;
    float maxDistance;
    float traveled;
    bool hasHit;

    Renderer[] renderers;
    PlayerController player;
    PlayerHealth health;

    public void Init(float speed, int damage, Vector3 direction, float maxDistance,
                     PlayerController player, PlayerHealth health)
    {
        this.speed = speed;
        this.damage = damage;
        this.direction = direction;
        this.maxDistance = maxDistance;
        this.player = player;
        this.health = health;
        renderers = GetComponentsInChildren<Renderer>();

        // Make the laser "hollow": no physical collision, so it can never push or block the player.
        // Damage is detected by the check below, not by colliders.
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            Destroy(rb);
    }

    void Update()
    {
        // Move in small steps so a very fast laser can't jump over the player between frames
        const float maxStep = 0.25f;
        float remaining = speed * Time.deltaTime;

        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, maxStep);
            remaining -= step;

            transform.position += direction * step;
            traveled += step;

            if (traveled >= maxDistance)
            {
                Destroy(gameObject);
                return;
            }

            // Each laser can only hurt the player once as it passes through
            if (!hasHit && player != null && TouchesPlayer())
            {
                hasHit = true;
                health.TakeDamage(damage);
            }
        }
    }

    // Accurate even when the laser is tilted: checks points along the player's body
    // against each renderer's box in the laser's own (rotated) space.
    bool TouchesPlayer()
    {
        CharacterController cc = player.Controller;
        float radius = cc.radius;
        float half = Mathf.Max(0f, cc.height * 0.5f - radius);

        for (int s = 0; s < 4; s++)
        {
            float t = s / 3f;
            Vector3 p = cc.transform.TransformPoint(cc.center + Vector3.up * Mathf.Lerp(-half, half, t));

            if (renderers.Length == 0)
            {
                if (Vector3.Distance(transform.position, p) < 1f) return true;
                continue;
            }

            foreach (Renderer rend in renderers)
                if (PointNear(rend, p, radius)) return true;
        }
        return false;
    }

    static bool PointNear(Renderer rend, Vector3 worldPoint, float radius)
    {
        Transform rt = rend.transform;
        Vector3 local = rt.InverseTransformPoint(worldPoint);
        Vector3 s = rt.lossyScale;
        Vector3 pad = new Vector3(
            radius / Mathf.Max(Mathf.Abs(s.x), 0.0001f),
            radius / Mathf.Max(Mathf.Abs(s.y), 0.0001f),
            radius / Mathf.Max(Mathf.Abs(s.z), 0.0001f));

        Bounds b = rend.localBounds;
        b.Expand(pad * 2f);
        return b.Contains(local);
    }
}