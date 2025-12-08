using UnityEngine;

public class EnergyPickup : MonoBehaviour
{
    [Tooltip("Amount of energy to restore")]
    public int energyAmount = 1;

    [Tooltip("Sound played when picking up energy (optional, falls back to energy restore sound)")]
    [SerializeField] private AudioClip pickupSound;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerEnergy playerEnergy = other.GetComponent<PlayerEnergy>();
            if (playerEnergy != null)
            {
                // Play pickup sound if assigned, otherwise rely on energy restore sound from PlayerEnergy
                if (pickupSound != null)
                {
                    // Play at reduced volume to match energy restore sound
                    SoundManager.GetInstance()?.PlaySound2D(pickupSound, 0.01f);
                }

                playerEnergy.AddEnergy(energyAmount);
                Destroy(gameObject);
            }
        }
    }
}
