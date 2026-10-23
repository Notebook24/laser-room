using System.Collections.Generic;
using UnityEngine;

// A patrolling guard/drone. Put it on a Cylinder/Capsule (or any object). Its blue arrow (Z axis) is "forward".
//
// Detection uses all of these:
//   Physics.CheckBox      -> broad phase: is the player inside the box that wraps the vision cone?
//   Raycast               -> line of sight: from the eye to the player's head/chest, walls block it
//   Physics.CheckSphere   -> hearing (player close AND moving) and attack range
//   Physics.CheckCapsule  -> body contact: bumping into the guard alerts it instantly
//
// States: PATROL (walks waypoints, looks around) -> CHASE (turns red, runs at you, attacks in range)
// The player's object must be on a layer called "Player" (see setup notes).
public class StealthHazard : MonoBehaviour
{
    public static readonly List<StealthHazard> All = new List<StealthHazard>();

    // =====================================================================
    //  EDIT THESE NUMBERS (constants, always win over the Inspector)
    // =====================================================================
    const float HEAR_RADIUS = 9f;              // meters
    const float HEAR_MIN_SPEED = 1f;          // player must move faster than this to make noise
    const float HEAR_GAIN = 0.5f;             // awareness per second from noise
    const float SIGHT_GAIN = 2.5f;            // awareness per second from sight (x2 when very close)
    const float AWARENESS_DECAY = 0.4f;       // awareness lost per second when nothing is sensed (patrol only)
    const float FLASHLIGHT_RANGE_MULT = 1.5f; // guards see you from farther away when your light is ON
    const float BODY_RADIUS = 0.6f;           // contact capsule
    const float BODY_HEIGHT = 1.8f;
    const float TURN_SPEED = 180f;            // degrees per second while patrolling
    const float CHASE_TURN_SPEED = 360f;
    const float BOX_HALF_HEIGHT = 3f;         // vertical size of the vision broad-phase box

    const float ATTACK_RANGE = 3f;          // CheckSphere radius: inside this the guard attacks
    const int   ATTACK_DAMAGE = 8;
    const float ATTACK_COOLDOWN = 1f;         // seconds between hits
    const float STOP_DISTANCE = 1.4f;         // chasing guard stops this close to you
    const float LOSE_TIME = 3f;               // seconds without seeing/hearing you before it gives up

    const float CONE_LIGHT_INTENSITY = 220f;  // brightness of the vision cone light
    const float EMISSION_BOOST = 2f;          // how much the body glows (makes the green clearly visible)
    // =====================================================================

    [Header("Patrol")]
    [Tooltip("Empty objects the guard walks between (ping-pong). Leave empty to patrol along Fallback Axis.")]
    public Transform[] waypoints;
    public bool loop = false;
    public float moveSpeed = 3f;
    [Tooltip("Seconds spent looking around at each end")]
    public float waitTime = 1.5f;
    [Tooltip("How far (degrees) the guard sweeps left/right while waiting")]
    public float scanAngle = 50f;
    public Vector3 fallbackAxis = Vector3.right;
    public float fallbackDistance = 8f;

    [Header("Chase")]
    [Tooltip("Keep this lower than the player's move speed (8) so you can escape")]
    public float chaseSpeed = 5f;

    [Header("Vision")]
    public float viewRange = 12f;
    [Range(10f, 170f)] public float viewAngle = 70f;
    public float eyeHeight = 0.5f;
    [Tooltip("Creates a visible spot light showing the vision cone (green -> yellow -> red)")]
    public bool showConeLight = true;
    [Tooltip("Optional: renderer that changes color with awareness. Defaults to this object's renderer.")]
    public Renderer indicator;
    public bool debugRays = true;

    [Header("Layers")]
    [Tooltip("Set to the 'Player' layer (filled automatically if a layer called Player exists)")]
    public LayerMask playerMask;

    public float Awareness { get; private set; }
    public bool IsChasing => chasing;

    PlayerController player;
    PlayerHealth health;
    Light coneLight;
    Vector3[] points;
    int index;
    int initialIndex;
    int step = 1;
    bool waiting;
    float waitStart;
    float arriveYaw;
    int lastRespawn;

    bool chasing;
    float lostTimer;
    float nextAttackTime;
    Vector3 lastKnownPos;

    Vector3 startPosition;
    Quaternion startRotation;

    Vector3 EyePos => transform.position + Vector3.up * eyeHeight;
    float EffectiveRange =>
        viewRange * ((Flashlight.Instance != null && Flashlight.Instance.IsOn) ? FLASHLIGHT_RANGE_MULT : 1f);

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        health = FindFirstObjectByType<PlayerHealth>();
        if (player != null) lastRespawn = player.RespawnCount;

        startPosition = transform.position;
        startRotation = transform.rotation;

        if (playerMask.value == 0)
        {
            int layer = LayerMask.NameToLayer("Player");
            if (layer >= 0) playerMask = 1 << layer;
            else Debug.LogError("StealthHazard: create a layer called 'Player' and put the Person object on it.");
        }

        if (player != null && (playerMask.value & (1 << player.gameObject.layer)) == 0)
            Debug.LogError("StealthHazard: the Person object is NOT on the layer in Player Mask. Set Person's Layer to 'Player' (and change children).");

        // Path: waypoint positions (captured now), or two points along the fallback axis
        if (waypoints != null && waypoints.Length > 0)
        {
            points = new Vector3[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++) points[i] = waypoints[i].position;
        }
        else
        {
            Vector3 axis = fallbackAxis.normalized * fallbackDistance * 0.5f;
            points = new[] { transform.position - axis, transform.position + axis };
            index = 1;
        }
        initialIndex = index;

        if (indicator == null) indicator = GetComponentInChildren<Renderer>();

        if (showConeLight)
        {
            GameObject go = new GameObject("VisionCone");
            go.transform.SetParent(transform, false);
            go.transform.position = EyePos;
            go.transform.rotation = transform.rotation;
            coneLight = go.AddComponent<Light>();
            coneLight.type = LightType.Spot;
            coneLight.spotAngle = viewAngle;
            coneLight.innerSpotAngle = viewAngle * 0.7f;
            coneLight.range = viewRange;
            coneLight.intensity = CONE_LIGHT_INTENSITY;
        }
    }

    void Update()
    {
        if (player == null || points == null) return;

        // Player died -> guard goes back to its start and calms down
        if (player.RespawnCount != lastRespawn)
        {
            lastRespawn = player.RespawnCount;
            ResetGuard();
        }

        bool touching = TouchingPlayer();
        bool seen = CanSeePlayer(out float seenDist);
        bool heard = CanHearPlayer();

        if (touching)
        {
            Awareness = 1f;
        }
        else if (seen)
        {
            float closeness = Mathf.Lerp(2f, 1f, seenDist / Mathf.Max(0.01f, EffectiveRange));
            Awareness += SIGHT_GAIN * closeness * Time.deltaTime;
        }
        else if (heard)
        {
            Awareness += HEAR_GAIN * Time.deltaTime;
        }
        else if (!chasing)
        {
            Awareness -= AWARENESS_DECAY * Time.deltaTime;
        }
        Awareness = Mathf.Clamp01(Awareness);

        if (!chasing && Awareness >= 1f)
            StartChase();

        if (chasing) Chase(seen || touching, heard);
        else Patrol();

        UpdateVisuals();
    }

    // ------------------------------------------------------------------ chase + attack
    void StartChase()
    {
        chasing = true;
        lostTimer = 0f;
        waiting = false;
        lastKnownPos = player.Controller.bounds.center;
        if (health != null) health.ShowMessage($"{name} spotted you!", 1.5f);
    }

    void StopChase()
    {
        chasing = false;
        Awareness = 0.5f;   // still suspicious for a moment

        // Resume patrol from the nearest point
        float best = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float d = (points[i] - transform.position).sqrMagnitude;
            if (d < best) { best = d; index = i; }
        }
        waiting = false;
    }

    void Chase(bool seen, bool heard)
    {
        Awareness = 1f;   // stays red while hunting
        float dt = Time.deltaTime;

        if (seen || heard)
        {
            lostTimer = 0f;
            lastKnownPos = player.Controller.bounds.center;   // seeing OR hearing you reveals where you are
        }
        else
        {
            lostTimer += dt;
            if (lostTimer >= LOSE_TIME)
            {
                StopChase();
                return;
            }
        }

        // Run toward where the player is (or was last known), staying at the guard's own height
        Vector3 target = lastKnownPos;
        target.y = transform.position.y;
        Vector3 to = target - transform.position;
        float dist = to.magnitude;

        if (dist > STOP_DISTANCE)
            transform.position = Vector3.MoveTowards(transform.position, target, chaseSpeed * dt);

        to.y = 0f;
        if (to.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), CHASE_TURN_SPEED * dt);

        // Attack: CheckSphere around the guard. Player inside it = hit, every ATTACK_COOLDOWN seconds
        if (Time.time >= nextAttackTime &&
            Physics.CheckSphere(transform.position, ATTACK_RANGE, playerMask, QueryTriggerInteraction.Ignore))
        {
            nextAttackTime = Time.time + ATTACK_COOLDOWN;
            if (health != null)
            {
                health.ShowMessage($"{name} hit you!  -{ATTACK_DAMAGE} HP", 1.5f);
                health.TakeDamage(ATTACK_DAMAGE);
            }
        }
    }

    void ResetGuard()
    {
        chasing = false;
        Awareness = 0f;
        lostTimer = 0f;
        waiting = false;
        index = initialIndex;
        step = 1;
        transform.SetPositionAndRotation(startPosition, startRotation);
    }

    // ------------------------------------------------------------------ patrol movement
    void Patrol()
    {
        float dt = Time.deltaTime;

        if (waiting)
        {
            // Look left and right while standing at the point
            float t = waitTime > 0f ? (Time.time - waitStart) / waitTime : 1f;
            float yaw = arriveYaw + Mathf.Sin(t * Mathf.PI * 2f) * scanAngle;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, yaw, 0f), TURN_SPEED * dt);

            if (Time.time >= waitStart + waitTime)
            {
                waiting = false;
                NextPoint();
            }
            return;
        }

        Vector3 target = points[index];
        Vector3 to = target - transform.position;
        if (to.magnitude <= 0.1f)
        {
            waiting = true;
            waitStart = Time.time;
            arriveYaw = transform.eulerAngles.y;
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * dt);

        to.y = 0f;
        if (to.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TURN_SPEED * dt);
    }

    void NextPoint()
    {
        if (points.Length < 2) return;
        if (loop)
        {
            index = (index + 1) % points.Length;
        }
        else
        {
            if (index + step >= points.Length || index + step < 0) step = -step;
            index += step;
        }
    }

    // ------------------------------------------------------------------ detection
    bool CanSeePlayer(out float distance)
    {
        distance = 0f;
        float range = EffectiveRange;
        Vector3 eye = EyePos;
        Vector3 fwd = transform.forward;

        // 1) Broad phase: a box wrapped around the vision cone (cheap yes/no)
        float halfWidth = Mathf.Min(range, range * Mathf.Tan(viewAngle * 0.5f * Mathf.Deg2Rad));
        Vector3 boxCenter = eye + fwd * (range * 0.5f);
        Vector3 boxHalf = new Vector3(halfWidth, BOX_HALF_HEIGHT, range * 0.5f);
        if (!Physics.CheckBox(boxCenter, boxHalf, transform.rotation, playerMask, QueryTriggerInteraction.Ignore))
            return false;

        // 2) Narrow phase: angle + raycast to the player's chest and head
        Bounds pb = player.Controller.bounds;
        Vector3 chest = pb.center;
        Vector3 head = pb.center + Vector3.up * (pb.extents.y * 0.8f);

        return RayToPlayer(eye, fwd, chest, range, out distance) ||
               RayToPlayer(eye, fwd, head, range, out distance);
    }

    bool RayToPlayer(Vector3 eye, Vector3 fwd, Vector3 target, float range, out float distance)
    {
        distance = 0f;
        Vector3 to = target - eye;
        float d = to.magnitude;
        if (d > range || d < 0.01f) return false;
        if (Vector3.Angle(fwd, to) > viewAngle * 0.5f) return false;

        // Raycast: the FIRST thing the ray touches (ignoring this guard) must be the player
        bool hitSomething = StealthUtil.FirstHit(eye, to / d, d + 0.2f, transform, null, out RaycastHit hit);
        bool clear = hitSomething && hit.collider == player.Controller;

        if (debugRays) Debug.DrawLine(eye, target, clear ? Color.red : Color.gray);

        if (clear) distance = d;
        return clear;
    }

    bool CanHearPlayer()
    {
        // CheckSphere: is the player inside the hearing radius? (works through walls)
        if (!Physics.CheckSphere(transform.position, HEAR_RADIUS, playerMask, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 v = player.Controller.velocity;
        v.y = 0f;
        return v.magnitude > HEAR_MIN_SPEED;   // standing still is silent
    }

    bool TouchingPlayer()
    {
        // CheckCapsule: the guard's own body
        float half = BODY_HEIGHT * 0.5f - BODY_RADIUS;
        Vector3 bottom = transform.position + Vector3.down * half;
        Vector3 top = transform.position + Vector3.up * half;
        return Physics.CheckCapsule(bottom, top, BODY_RADIUS, playerMask, QueryTriggerInteraction.Ignore);
    }

    // ------------------------------------------------------------------ visuals
    void UpdateVisuals()
    {
        Color c = Awareness < 0.5f ? Color.Lerp(Color.green, Color.yellow, Awareness * 2f)
                                   : Color.Lerp(Color.yellow, Color.red, (Awareness - 0.5f) * 2f);
        if (indicator != null)
        {
            Material m = indicator.material;
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * EMISSION_BOOST);   // glow, so green stands out in the dark
        }
        if (coneLight != null) coneLight.color = c;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, HEAR_RADIUS);
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, ATTACK_RANGE);

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Gizmos.matrix = Matrix4x4.TRS(eye, transform.rotation, Vector3.one);
        Gizmos.color = Color.red;
        float h = viewAngle * 0.5f * Mathf.Deg2Rad;
        Gizmos.DrawLine(Vector3.zero, new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h)) * viewRange);
        Gizmos.DrawLine(Vector3.zero, new Vector3(-Mathf.Sin(h), 0f, Mathf.Cos(h)) * viewRange);
    }
}