using UnityEngine;
using UnityEngine.InputSystem;

// Put this on the Person (player) object. Press F to toggle the flashlight.
// It creates a spot light on the camera, and exposes IsInBeam() so other scripts
// (HiddenObject) can ask "is this point inside the light cone?".
// It also draws the stealth HUD (detection meter, battery, clue counter).
public class Flashlight : MonoBehaviour
{
    // =====================================================================
    //  EDIT THESE NUMBERS (constants, always win over the Inspector)
    // =====================================================================
    
    const float FOG_END_OFF = 16f;         // you can see this far with the light OFF
    const float FOG_END_ON = 26f;          // you can see this far with the light ON
    const float FOG_START = 3f;            // fog begins right at the camera
    const float FOG_BLEND_SPEED = 4f;      // how quickly the view distance changes
    const float DARK_AMBIENT = 0.25f;      // ambient light (0 = pitch black)
    const float DARK_SUN = 0.2f;           // directional light intensity (the "moon")
    const float RANGE = 20f;              // how far the beam reaches (meters)
    const float SPOT_ANGLE = 60f;         // full cone angle (degrees)
    const float INTENSITY = 160f;         // brightness of the visible light (URP spot lights need big numbers)
    const float BATTERY_SECONDS = 40f;    // seconds of light from a full battery
    const float RECHARGE_SPEED = 0.5f;    // recharge rate while off (1 = same speed as draining)
    const float RESTART_LEVEL = 0.2f;     // after running dry, battery must reach this (0-1) to turn on again
    // =====================================================================

    public static Flashlight Instance { get; private set; }
    public bool IsOn { get; private set; }
    public float Battery01 => battery / BATTERY_SECONDS;

    Light beam;
    Transform cam;
    float battery;
    bool exhausted;
    float fogEnd = FOG_END_OFF;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    void Awake()
    {
        Instance = this;
        battery = BATTERY_SECONDS;
    }

    void Start()
    {
        Camera c = GetComponentInChildren<Camera>();
        if (c == null) c = Camera.main;
        cam = c.transform;

        GameObject go = new GameObject("FlashlightBeam");
        go.transform.SetParent(cam, false);
        beam = go.AddComponent<Light>();
        beam.type = LightType.Spot;
        beam.range = RANGE;
        beam.spotAngle = SPOT_ANGLE;
        beam.innerSpotAngle = SPOT_ANGLE * 0.6f;
        beam.intensity = INTENSITY;
        beam.color = new Color(1f, 0.96f, 0.85f);
        beam.shadows = LightShadows.Soft;
        beam.enabled = false;

        // Make the world dark
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white * DARK_AMBIENT;
        RenderSettings.ambientIntensity = DARK_AMBIENT;
        RenderSettings.reflectionIntensity = 0f;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.intensity = DARK_SUN;

        // Fog hides everything past your sight distance
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogStartDistance = FOG_START;
        RenderSettings.fogEndDistance = fogEnd;

        // Black background instead of the skybox (fog doesn't affect skyboxes)
        Camera camComp = cam.GetComponent<Camera>();
        camComp.clearFlags = CameraClearFlags.SolidColor;
        camComp.backgroundColor = Color.black;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || cam == null) return;

        if (kb.fKey.wasPressedThisFrame)
        {
            if (IsOn) IsOn = false;
            else if (!exhausted) IsOn = true;
        }

        if (IsOn)
        {
            battery -= Time.deltaTime;
            if (battery <= 0f)
            {
                battery = 0f;
                IsOn = false;
                exhausted = true;
            }
        }
        else
        {
            battery = Mathf.Min(BATTERY_SECONDS, battery + Time.deltaTime * RECHARGE_SPEED);
            if (exhausted && Battery01 >= RESTART_LEVEL) exhausted = false;
        }

        float targetFog = IsOn ? FOG_END_ON : FOG_END_OFF;
        fogEnd = Mathf.Lerp(fogEnd, targetFog, Time.deltaTime * FOG_BLEND_SPEED);
        RenderSettings.fogEndDistance = fogEnd;

        beam.enabled = IsOn;
    }

    // True if a sphere (center + radius) is inside the light cone AND not blocked by a wall.
    // "ignore" is the object being tested (so its own collider doesn't block the ray).
    public bool IsInBeam(Vector3 center, float radius, Transform ignore)
    {
        if (!IsOn || cam == null) return false;

        Vector3 origin = cam.position;
        Vector3 to = center - origin;
        float dist = to.magnitude;
        if (dist > RANGE + radius) return false;
        if (dist < 0.05f) return true;

        // Angle test (padded by the object's size so big objects light up when partly in the cone)
        float pad = Mathf.Asin(Mathf.Clamp01(radius / dist)) * Mathf.Rad2Deg;
        if (Vector3.Angle(cam.forward, to) > SPOT_ANGLE * 0.5f + pad) return false;

        // Line of sight: a wall between the light and the object blocks it
        Vector3 dir = to / dist;
        return !StealthUtil.FirstHit(origin, dir, Mathf.Max(0f, dist - 0.05f), ignore, transform, out _);
    }

    // ------------------------------------------------------------------ HUD
    void OnGUI()
    {
        int fontSize = Mathf.Max(16, Screen.height / 35);
        GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold };
        GUIStyle center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };

        // Detection meter (highest awareness of any guard), top center
        float worst = 0f;
        foreach (StealthHazard h in StealthHazard.All)
            worst = Mathf.Max(worst, h.Awareness);

        float barW = Screen.width * 0.3f;
        float barH = fontSize * 0.9f;
        float barX = (Screen.width - barW) * 0.5f;
        Color c = worst < 0.5f ? Color.Lerp(Color.green, Color.yellow, worst * 2f)
                               : Color.Lerp(Color.yellow, Color.red, (worst - 0.5f) * 2f);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, 10, Screen.width, fontSize * 1.5f), worst >= 0.99f ? "SPOTTED!" : "DETECTION", center);
        DrawBar(new Rect(barX, 10 + fontSize * 1.6f, barW, barH), worst, c);

        // Flashlight + battery, bottom left
        float y = Screen.height - fontSize * 3.2f;
        string state = IsOn ? "ON" : (exhausted ? "BATTERY DEAD" : "OFF");
        GUI.Label(new Rect(20, y, 600, fontSize * 1.5f), $"[F] Flashlight: {state}", label);
        DrawBar(new Rect(20, y + fontSize * 1.5f, barW * 0.6f, barH), Battery01,
                Battery01 < 0.25f ? Color.red : new Color(1f, 0.9f, 0.4f));

        // Hidden clues found, top right
        if (HiddenObject.Total > 0)
        {
            GUIStyle right = new GUIStyle(label) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(Screen.width - 520, 20, 500, fontSize * 2), $"Clues: {HiddenObject.Found} / {HiddenObject.Total}", right);
        }
    }

    static void DrawBar(Rect r, float fill, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(fill), r.height - 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}