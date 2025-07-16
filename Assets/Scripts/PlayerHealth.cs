using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    
    [Header("UI Elements")]
    public Slider healthSlider;
    public Text healthText;
    
    [Header("Effects")]
    public GameObject damageEffect;
    public AudioClip damageSound;
    
    private AudioSource audioSource;
    
    void Start()
    {
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();
        UpdateHealthUI();
    }
    
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        Debug.Log("Player took " + damage + " damage! Current health: " + currentHealth);
        
        // Play damage sound
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
        
        // Show damage effect
        if (damageEffect != null)
        {
            GameObject effect = Instantiate(damageEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        
        UpdateHealthUI();
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHealthUI();
    }
    
    void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth / maxHealth;
        }
        
        if (healthText != null)
        {
            healthText.text = "Health: " + currentHealth.ToString("F0") + "/" + maxHealth.ToString("F0");
        }
    }
    
    void Die()
    {
        Debug.Log("Player died!");
        // You can add death effects, restart level, etc. here
        
        // Simple respawn after 3 seconds
        Invoke("Respawn", 3f);
    }
    
    void Respawn()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
        Debug.Log("Player respawned!");
    }
}