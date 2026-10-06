using UnityEngine;

[System.Serializable]
public class LaserType
{
    public GameObject template;                   // the model that gets copied (drag RedLaser / BlueLaser here)
    [HideInInspector] public float speed;         // set from the numbers in LaserSpawner.cs
    [HideInInspector] public int damage;          // set from the numbers in LaserSpawner.cs
}

public class LaserSpawner : MonoBehaviour
{
    // =====================================================================
    //  EDIT THESE NUMBERS. They are constants, so they ALWAYS win:
    //  Unity does not save them in the scene and they don't show in the Inspector.
    // =====================================================================
    const float RED_SPEED = 45f;
    const int   RED_DAMAGE = 5;
    const float BLUE_SPEED = 60f;
    const int   BLUE_DAMAGE = 10;

    const float SPAWN_INTERVAL = 2f;          // seconds between waves
    const float BLUE_CHANCE = 0.4f;           // 0 to 1, chance a laser is blue
    const float SIMULTANEOUS_CHANCE = 0.35f;  // 0 to 1, chance of an extra laser in the same wave
    const int   MAX_LASERS_PER_WAVE = 3;

    const float MIN_ANGLE = -90f;             // random tilt around travel direction (degrees)
    const float MAX_ANGLE = 90f;
    const float MIN_HEIGHT_OFFSET = -1f;      // random height relative to Spawn Point's Y (meters)
    const float MAX_HEIGHT_OFFSET = 1.5f;

    const float COUNTDOWN_SECONDS = 5f;      // player is frozen this long after stepping on start
    const float SPAWN_OFFSET = 0f;            // push spawn point along travel direction (meters)
    // =====================================================================

    [Header("Corridor (drag these in the Inspector)")]
    public Transform startPlatform;
    public Transform endPlatform;
    [Tooltip("Optional: an empty object on the end platform where lasers appear. If empty, the center of the end platform is used.")]
    public Transform spawnPoint;

    [Header("Laser models (drag these in the Inspector)")]
    public LaserType redLaser;
    public LaserType blueLaser;

    bool counting;
    float countdownRemaining;
    bool spawning;
    float timer;
    Transform container;
    PlayerController player;
    PlayerHealth health;

    void Awake()
    {
        // Apply the numbers from the code, overriding anything saved in the scene
        redLaser.speed = RED_SPEED;
        redLaser.damage = RED_DAMAGE;
        blueLaser.speed = BLUE_SPEED;
        blueLaser.damage = BLUE_DAMAGE;

        container = new GameObject("SpawnedLasers").transform;
        player = FindFirstObjectByType<PlayerController>();
        health = FindFirstObjectByType<PlayerHealth>();

        // Hide the templates, only the clones are shown
        if (redLaser.template != null) redLaser.template.SetActive(false);
        if (blueLaser.template != null) blueLaser.template.SetActive(false);
    }

    void Update()
    {
        // Countdown: player is frozen, but lasers keep spawning and moving
        if (counting)
        {
            countdownRemaining -= Time.deltaTime;
            if (countdownRemaining <= 0f)
            {
                counting = false;
                if (player != null) player.canMove = true;
                if (health != null)
                {
                    health.SetBigText("");
                    health.ShowMessage("GO! Run!", 2f);
                }
            }
            else if (health != null)
            {
                health.SetBigText(Mathf.CeilToInt(countdownRemaining).ToString());
            }
        }

        if (!spawning) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SpawnWave();
            timer = SPAWN_INTERVAL;
        }
    }

    public void StartSpawning()
    {
        if (spawning) return;

        // Lasers start right away
        spawning = true;
        timer = 0.5f;

        // Player is frozen for the countdown
        counting = true;
        countdownRemaining = COUNTDOWN_SECONDS;
        if (player != null) player.canMove = false;
    }

    public void StopSpawning(bool clearExisting)
    {
        counting = false;
        spawning = false;
        if (player != null) player.canMove = true;
        if (health != null) health.SetBigText("");

        if (!clearExisting) return;
        foreach (Transform child in container)
            Destroy(child.gameObject);
    }

    // Uses the real bounds of the rendered meshes, since group/ProBuilder pivots are often at (0,0,0)
    static Bounds GetBounds(Transform t)
    {
        Renderer[] rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(t.position, Vector3.one);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        return b;
    }

    void SpawnWave()
    {
        int count = 1;
        while (count < MAX_LASERS_PER_WAVE && Random.value < SIMULTANEOUS_CHANCE)
            count++;

        for (int i = 0; i < count; i++)
            SpawnLaser();
    }

    void SpawnLaser()
    {
        LaserType type = Random.value < BLUE_CHANCE ? blueLaser : redLaser;
        if (type.template == null)
        {
            Debug.LogError("LaserSpawner: a laser Template is not assigned!");
            return;
        }
        if (startPlatform == null || endPlatform == null)
        {
            Debug.LogError("LaserSpawner: Start Platform or End Platform is not assigned!");
            return;
        }

        Bounds startBounds = GetBounds(startPlatform);
        Bounds endBounds = GetBounds(endPlatform);

        // Travel direction: end platform -> start platform (flat)
        Vector3 dir = startBounds.center - endBounds.center;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            Debug.LogError("LaserSpawner: start and end platforms are at the same spot, so there is no direction to travel.");
            return;
        }
        dir.Normalize();

        // Position: spawn point (or end platform center), with random height
        Transform t = type.template.transform;
        Vector3 pos = spawnPoint != null
            ? spawnPoint.position
            : new Vector3(endBounds.center.x, t.position.y, endBounds.center.z);
        pos += dir * SPAWN_OFFSET;
        pos.y += Random.Range(MIN_HEIGHT_OFFSET, MAX_HEIGHT_OFFSET);

        // Rotation: random tilt around the travel direction
        Quaternion rot = Quaternion.AngleAxis(Random.Range(MIN_ANGLE, MAX_ANGLE), dir) * t.rotation;

        GameObject clone = Instantiate(type.template, pos, rot, container);
        clone.transform.localScale = t.lossyScale;
        clone.SetActive(true);

        // Lasers disappear at the near EDGE of the start platform
        float halfDepth = Mathf.Abs(dir.x) * startBounds.extents.x + Mathf.Abs(dir.z) * startBounds.extents.z;
        float maxDistance = Vector3.Dot(startBounds.center - pos, dir) - halfDepth;
        maxDistance = Mathf.Max(maxDistance, 0.1f);

        Laser laser = clone.AddComponent<Laser>();
        laser.Init(type.speed, type.damage, dir, maxDistance, player, health);
    }
}