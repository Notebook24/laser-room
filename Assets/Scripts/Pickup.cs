using UnityEngine;

public enum PickupBonus
{
    Random,      // picks ONE of the three below each time it is collected
    MoveSpeed,   // +1 movement speed
    JumpSpeed,   // +1 jump speed
    BonusHP      // +5 bonus HP (raises max HP too)
}

// Put this on any object. Touching it gives +10 HP and exactly ONE bonus.
public class Pickup : MonoBehaviour
{
    [Header("Always given")]
    public int healAmount = 10;

    [Header("Bonus (only ONE is given)")]
    public PickupBonus bonus = PickupBonus.Random;
    public float moveSpeedIncrease = 1f;
    public float jumpSpeedIncrease = 1f;
    public int bonusHpAmount = 5;

    [Header("Behavior")]
    [Tooltip("Turns off the object's colliders so the player walks through it instead of bumping into it")]
    public bool walkThrough = true;
    [Tooltip("The pickup shows up again after the player dies (the bonuses are lost on death too)")]
    public bool comesBackAfterDeath = true;

    PlayerController player;
    PlayerHealth health;
    Renderer[] renderers;
    Bounds bounds;
    bool collected;
    int collectedAtRespawn;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        health = FindFirstObjectByType<PlayerHealth>();

        renderers = GetComponentsInChildren<Renderer>();
        bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        bounds.Expand(0.2f);   // a little extra so "touching" counts

        if (walkThrough)
        {
            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;
        }
    }

    void Update()
    {
        if (player == null || health == null) return;

        if (collected)
        {
            // Bring it back after the player dies
            if (comesBackAfterDeath && player.RespawnCount != collectedAtRespawn)
            {
                collected = false;
                SetVisible(true);
            }
            return;
        }

        if (player.Controller.bounds.Intersects(bounds))
            Collect();
    }

    void Collect()
    {
        collected = true;
        collectedAtRespawn = player.RespawnCount;
        SetVisible(false);

        health.Heal(healAmount);
        string message = $"+{healAmount} HP";

        PickupBonus chosen = bonus == PickupBonus.Random
            ? (PickupBonus)Random.Range(1, 4)   // MoveSpeed, JumpSpeed or BonusHP
            : bonus;

        switch (chosen)
        {
            case PickupBonus.MoveSpeed:
                player.AddMoveSpeed(moveSpeedIncrease);
                message += $"  +{moveSpeedIncrease} move speed";
                break;

            case PickupBonus.JumpSpeed:
                player.AddJumpBonus(jumpSpeedIncrease);
                message += $"  +{jumpSpeedIncrease} jump speed";
                break;

            case PickupBonus.BonusHP:
                health.AddBonusHealth(bonusHpAmount);
                message += $"  +{bonusHpAmount} bonus HP";
                break;
        }

        health.ShowMessage(message, 2.5f);
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers)
            r.enabled = visible;
    }
}