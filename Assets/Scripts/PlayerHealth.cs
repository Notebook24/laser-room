using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 25;

    PlayerController player;
    int baseMaxHealth;
    string message = "";
    float messageUntil;
    string bigText = "";

    public int CurrentHealth { get; private set; }

    void Awake()
    {
        player = GetComponent<PlayerController>();
        baseMaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (CurrentHealth <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (CurrentHealth == 0)
            Die();
    }

    public void HealFull()
    {
        CurrentHealth = maxHealth;
    }

    // Heals up to the max (never above it)
    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    // Raises the max HP and the current HP by the same amount (lost again on death)
    public void AddBonusHealth(int amount)
    {
        maxHealth += amount;
        CurrentHealth += amount;
    }

    public void ShowMessage(string text, float seconds)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    public void SetBigText(string text)
    {
        bigText = text;
    }

    void Die()
    {
        LaserSpawner spawner = FindFirstObjectByType<LaserSpawner>();
        if (spawner != null) spawner.StopSpawning(true);

        player.Respawn();
        maxHealth = baseMaxHealth;
        CurrentHealth = maxHealth;
        ShowMessage("You died! Step on the switch to try again.", 3f);
    }

    void OnGUI()
    {
        int fontSize = Mathf.Max(18, Screen.height / 25);

        GUIStyle hp = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };
        hp.normal.textColor = CurrentHealth <= 10 ? Color.red : Color.white;
        GUI.Label(new Rect(20, 20, 500, fontSize * 2), $"HP: {CurrentHealth} / {maxHealth}", hp);

        if (!string.IsNullOrEmpty(bigText))
        {
            GUIStyle big = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize * 4,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            big.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, fontSize * 5), bigText, big);
        }

        if (Time.time < messageUntil)
        {
            GUIStyle msg = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            msg.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, fontSize * 2), message, msg);
        }
    }
}