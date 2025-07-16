using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game Settings")]
    public bool gameRunning = true;
    public float gameTime = 0f;
    public int score = 0;
    public int totalDebris = 0;

    [Header("References")]
    public GameObject player;
    public GameObject enemy;
    public Transform respawnPoint;

    [Header("UI References")]
    public Text scoreText;
    public GameObject gameStatusPanel;
    public Text gameStatusText;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Find player and enemy if not assigned
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (enemy == null)
        {
            enemy = GameObject.Find("Enemy");
        }

        // Set respawn point to player's starting position
        if (respawnPoint == null && player != null)
        {
            GameObject respawnObj = new GameObject("Respawn Point");
            respawnObj.transform.position = player.transform.position;
            respawnPoint = respawnObj.transform;
        }

        // Find UI elements if not assigned
        if (scoreText == null)
        {
            scoreText = GameObject.Find("Score Text")?.GetComponent<Text>();
        }

        if (gameStatusPanel == null)
        {
            gameStatusPanel = GameObject.Find("Game Status Panel");
        }

        if (gameStatusText == null)
        {
            gameStatusText = GameObject.Find("Game Status Text")?.GetComponent<Text>();
        }

        // Count total debris in the scene
        GameObject[] debrisObjects = GameObject.FindGameObjectsWithTag("Debu");
        SetTotalDebris(debrisObjects.Length);

        // Initialize UI
        UpdateScoreUI();
    }

    void Update()
    {
        if (gameRunning)
        {
            gameTime += Time.deltaTime;
        }

        // Restart game with R key
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
        }

        // Quit game with Q key
        if (Input.GetKeyDown(KeyCode.Q))
        {
            QuitGame();
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void RespawnPlayer()
    {
        if (player != null && respawnPoint != null)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.transform.position = respawnPoint.position;
                player.transform.rotation = respawnPoint.rotation;
                controller.enabled = true;
            }
            else
            {
                player.transform.position = respawnPoint.position;
                player.transform.rotation = respawnPoint.rotation;
            }
        }
    }

    public void AddScore(int points)
    {
        score += points;
        Debug.Log("Score: " + score);
        UpdateScoreUI();

        // Check if all debris collected
        if (score >= totalDebris && totalDebris > 0)
        {
            Debug.Log("All debris collected! Victory!");
            GameVictory();
        }
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score + "/" + totalDebris;
        }
    }

    public void GameVictory()
    {
        gameRunning = false;
        Debug.Log("Victory! You collected all debris! Press R to restart or Q to quit.");
        ShowGameStatus("Victory! You collected all debris! Press R to restart or Q to quit.");
    }

    public void GameOver()
    {
        gameRunning = false;
        Debug.Log("Game Over! Press R to restart or Q to quit.");
        ShowGameStatus("Game Over! Press R to restart or Q to quit.");
    }

    void ShowGameStatus(string message)
    {
        if (gameStatusPanel != null)
        {
            gameStatusPanel.SetActive(true);
        }

        if (gameStatusText != null)
        {
            gameStatusText.text = message;
        }
    }

    public void SetTotalDebris(int total)
    {
        totalDebris = total;
        Debug.Log("Total debris in level: " + totalDebris);
    }
}