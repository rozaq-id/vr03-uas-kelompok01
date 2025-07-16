using UnityEngine;

public class ChargingZone : MonoBehaviour
{
    public float rechargeRate = 2f; // per detik

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player")) // pastikan robot ditag 'Player'
        {
            BatterySystem battery = other.GetComponent<BatterySystem>();
            if (battery != null)
            {
                battery.RechargeBattery(rechargeRate * Time.deltaTime);
            }
        }
    }
}
