using UnityEngine;

public enum ZoneType
{
    Start,       // stepping on it starts the lasers
    End,         // stepping on it stops the lasers
    Checkpoint,        // full heal + jump boost
    SendToCheckpoint   // teleports the player to the "Send To" object (e.g. MidCheckpoint)
}

// Put this on the platform's parent object (StartLevel, EndLevel, MidCheckpoint).
// It uses the combined bounds of all child renderers, so no colliders/triggers are needed.
public class PlatformZone : MonoBehaviour
{
    public ZoneType type;
    [Tooltip("Only for SendToCheckpoint: where to send the player (drag MidCheckpoint here)")]
    public Transform sendTo;
    public float jumpSpeedIncrease = 1f;   // added to the current jump speed

    PlayerController player;
    PlayerHealth health;
    LaserSpawner spawner;
    Bounds bounds;
    bool playerInside;
    int usedAtRespawn = -1;   // which life the checkpoint was used in

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        health = FindFirstObjectByType<PlayerHealth>();
        spawner = FindFirstObjectByType<LaserSpawner>();

        Renderer[] rends = GetComponentsInChildren<Renderer>();
        bounds = rends.Length > 0 ? rends[0].bounds : new Bounds(transform.position, Vector3.one * 2f);
        for (int i = 1; i < rends.Length; i++)
            bounds.Encapsulate(rends[i].bounds);
    }

    void Update()
    {
        if (player == null) return;

        bool nowInside = IsPlayerOnPlatform();
        if (nowInside && !playerInside)
            OnPlayerEnter();
        playerInside = nowInside;
    }

    bool IsPlayerOnPlatform()
    {
        Bounds pb = player.Controller.bounds;
        Vector3 p = pb.center;
        float feet = pb.min.y;

        bool insideXZ = p.x >= bounds.min.x && p.x <= bounds.max.x &&
                        p.z >= bounds.min.z && p.z <= bounds.max.z;
        bool rightHeight = feet >= bounds.min.y - 0.5f && feet <= bounds.max.y + 1.5f;
        return insideXZ && rightHeight;
    }

    void OnPlayerEnter()
    {
        switch (type)
        {
            case ZoneType.Start:
                if (spawner != null) spawner.StartSpawning();
                health.ShowMessage("Get ready!", 3f);
                break;

            case ZoneType.End:
                if (spawner != null) spawner.StopSpawning(true);
                health.ShowMessage("You reached the end! Lasers off.", 4f);
                break;

            case ZoneType.SendToCheckpoint:
                SendPlayerToTarget();
                break;

            case ZoneType.Checkpoint:
                if (usedAtRespawn == player.RespawnCount)
                {
                    health.ShowMessage("Checkpoint already used", 2f);
                    break;
                }
                usedAtRespawn = player.RespawnCount;
                health.HealFull();
                player.AddJumpBonus(jumpSpeedIncrease);
                health.ShowMessage("Checkpoint! HP restored + jump boost", 3f);
                break;
        }
    }

    void SendPlayerToTarget()
    {
        if (sendTo == null)
        {
            Debug.LogError("PlatformZone: 'Send To' is not assigned on " + name);
            return;
        }

        Renderer[] rends = sendTo.GetComponentsInChildren<Renderer>();
        Vector3 target = sendTo.position;
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            target = new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);   // on top of the platform
        }

        player.TeleportTo(target);
        health.ShowMessage("Sent back to the checkpoint!", 2.5f);
    }
}