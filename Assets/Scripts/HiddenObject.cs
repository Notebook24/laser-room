using UnityEngine;

// Put this on any object (or a parent group). It is INVISIBLE until the player's flashlight is
// ON and the object is inside the light cone (and not behind a wall).
//  - Clue: once revealed, walk into it to collect it. Collecting applies a RANDOM debuff.
//  - Solid Only When Revealed: a "hidden bridge/platform" that only exists while lit.
public class HiddenObject : MonoBehaviour
{
    public static int Total;
    public static int Found;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Total = 0; Found = 0; }

    [Tooltip("Can be collected once revealed (counts toward the Clues counter)")]
    public bool isClue = true;
    [Tooltip("Colliders are only active while the object is revealed (hidden platform)")]
    public bool solidOnlyWhenRevealed = false;
    [Tooltip("Stays visible this many seconds after the light leaves it")]
    public float lingerSeconds = 0.35f;
    [Tooltip("The clue comes back after the player dies")]
    public bool comesBackAfterDeath = true;

    [Header("Random debuff on pickup (33% each)")]
    [Tooltip("Untick to make this clue harmless")]
    public bool applyRandomDebuff = true;
    [Tooltip("HP lost when the roll is Lose Health")]
    public int healthLoss = 10;
    [Tooltip("Speed multiplier while slowed (0.2 = 20% speed)")]
    public float slowMultiplier = 0.2f;
    [Tooltip("Seconds for Slow / Disable Jump")]
    public float debuffSeconds = 5f;

    Renderer[] renderers;
    Collider[] colliders;
    Bounds bounds;
    Flashlight flashlight;
    PlayerController player;
    PlayerHealth health;

    float visibleUntil;
    bool visible;
    bool discovered;
    bool collected;
    int collectedAtRespawn;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

        bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        if (isClue) Total++;

        // Clues are walk-through; hidden platforms start non-solid
        if (isClue && !solidOnlyWhenRevealed)
            foreach (Collider c in colliders) c.enabled = false;

        SetVisible(false);
    }

    void Start()
    {
        flashlight = Flashlight.Instance;
        player = FindFirstObjectByType<PlayerController>();
        health = FindFirstObjectByType<PlayerHealth>();
    }

    void Update()
    {
        if (flashlight == null) flashlight = Flashlight.Instance;
        if (player == null) return;

        if (collected)
        {
            if (comesBackAfterDeath && player.RespawnCount != collectedAtRespawn)
            {
                collected = false;
                discovered = false;
                Found--;
            }
            return;
        }

        bool lit = flashlight != null &&
                   flashlight.IsInBeam(bounds.center, bounds.extents.magnitude, transform);
        if (lit) visibleUntil = Time.time + lingerSeconds;

        bool nowVisible = Time.time < visibleUntil;
        if (nowVisible != visible) SetVisible(nowVisible);

        if (visible && !discovered)
        {
            discovered = true;
            if (health != null) health.ShowMessage("Hidden object detected!", 1.5f);
        }

        // Only collectable while it is revealed
        if (isClue && visible)
        {
            Bounds grab = bounds;
            grab.Expand(0.6f);
            if (player.Controller.bounds.Intersects(grab))
                Collect();
        }
    }

    void Collect()
    {
        collected = true;
        collectedAtRespawn = player.RespawnCount;
        Found++;
        SetVisible(false);

        string msg = "You stepped on a trap!";
        bool loseHealth = false;

        if (applyRandomDebuff)
        {
            // Random.Range(int, int) excludes the max, so this gives 0, 1 or 2 with equal chance
            switch (Random.Range(0, 3))
            {
                case 0:
                    loseHealth = true;
                    msg += $"  -{healthLoss} HP!";
                    break;
                case 1:
                    player.ApplySlow(slowMultiplier, debuffSeconds);
                    msg += $"  Slowed for {debuffSeconds}s!";
                    break;
                case 2:
                    player.DisableJump(debuffSeconds);
                    msg += $"  Can't jump for {debuffSeconds}s!";
                    break;
            }
        }

        if (health != null)
        {
            health.ShowMessage(msg, 2.5f);
            // Damage last: if it kills the player, Die() sets its own message,
            // which then replaces this one instead of being overwritten by it.
            if (loseHealth) health.TakeDamage(healthLoss);
        }
    }

    void SetVisible(bool value)
    {
        visible = value;
        foreach (Renderer r in renderers) r.enabled = value;
        if (solidOnlyWhenRevealed)
            foreach (Collider c in colliders) c.enabled = value;
    }
}