using UnityEngine;
using TMPro;

public class BatterySystem : MonoBehaviour
{
    [Header("Battery Settings")]
    public float maxBattery = 100f;
    public float currentBattery;
    public float batteryDrainPerSecond = 2f;

    [Header("UI")]
    public TMP_Text batteryText;

    private RobotMovement robot; // referensi ke script gerakan

    void Start()
    {
        currentBattery = maxBattery;
        robot = GetComponent<RobotMovement>();
    }

    void Update()
    {
        if (robot != null && robot.IsMoving())
        {
            currentBattery -= batteryDrainPerSecond * Time.deltaTime;
            currentBattery = Mathf.Clamp(currentBattery, 0f, maxBattery);
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        if (batteryText != null)
        {
            batteryText.text = "Battery: " + Mathf.RoundToInt(currentBattery) + "%";
        }
    }

    public bool IsBatteryEmpty()
    {
        return currentBattery <= 0f;
    }

    public void RechargeBattery(float amount)
    {
        currentBattery += amount;
        currentBattery = Mathf.Clamp(currentBattery, 0f, maxBattery);
    }
}
