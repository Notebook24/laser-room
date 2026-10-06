using UnityEngine;

public enum PlatformMotion
{
    MoveX,   // slides left <-> right along the X axis
    MoveY,   // moves up <-> down along the Y axis
    SpinX,   // rotates around the X axis
    SpinZ    // rotates around the Z axis
}

// Put this on a platform (or on the parent group of a platform), then pick a Motion type.
public class MovingPlatform : MonoBehaviour
{
    public PlatformMotion motion = PlatformMotion.MoveX;

    [Header("Move (MoveX / MoveY)")]
    [Tooltip("Speed in meters per second")]
    public float moveSpeed = 3f;
    [Tooltip("Total distance travelled between the two ends (meters). The platform starts in the middle.")]
    public float distance = 6f;
    [Tooltip("Shift the timing (seconds) so platforms don't all move in sync")]
    public float timeOffset = 0f;

    [Header("Spin (SpinX / SpinZ)")]
    [Tooltip("Degrees per second. Use a negative number to spin the other way.")]
    public float spinSpeed = 90f;

    Vector3 startPosition;
    Renderer[] renderers;
    PlayerController player;

    void Start()
    {
        startPosition = transform.position;
        renderers = GetComponentsInChildren<Renderer>();
        player = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        switch (motion)
        {
            case PlatformMotion.MoveX:
                MoveAlong(Vector3.right);
                break;

            case PlatformMotion.MoveY:
                MoveAlong(Vector3.up);
                break;

            case PlatformMotion.SpinX:
                transform.Rotate(Vector3.right, spinSpeed * Time.deltaTime, Space.Self);
                break;

            case PlatformMotion.SpinZ:
                transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);
                break;
        }
    }

    void MoveAlong(Vector3 axis)
    {
        // Was the player standing on the platform before it moves?
        bool playerOnTop = IsPlayerOnTop();

        // Ping-pong around the start position: constant speed, turns around at each end
        float t = Time.time + timeOffset;
        float offset = Mathf.PingPong(t * moveSpeed + distance * 0.5f, distance) - distance * 0.5f;

        Vector3 oldPosition = transform.position;
        transform.position = startPosition + axis * offset;

        // Carry the player along, otherwise the platform slides out from under them
        if (playerOnTop && player != null)
            player.Controller.Move(transform.position - oldPosition);
    }

    bool IsPlayerOnTop()
    {
        if (player == null || renderers.Length == 0) return false;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        Bounds pb = player.Controller.bounds;
        Vector3 p = pb.center;
        float feet = pb.min.y;

        bool insideXZ = p.x >= b.min.x && p.x <= b.max.x &&
                        p.z >= b.min.z && p.z <= b.max.z;
        bool onSurface = feet >= b.max.y - 0.3f && feet <= b.max.y + 0.4f;
        return insideXZ && onSurface;
    }
}