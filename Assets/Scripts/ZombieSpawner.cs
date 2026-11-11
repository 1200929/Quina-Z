using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Required for the GUI Text

public class ZombieSpawner : MonoBehaviour
{
    [Header("Wave Settings")]
    [Tooltip("Index 0 is Wave 1. If wave exceeds list, it uses the last number.")]
    public int[] zombiesPerWave = new int[] { 6, 8, 12, 15, 23, 25, 26 ,31 }; // tabela temporaria de numero de zombies por ronda
    
    public GameObject zombiePrefab;
    public int maxZombiesOnMap = 10;    
    public float timeBetweenWaves = 5f; 
    public float spawnDelay = 1.5f;     
    
    [Header("Spawn Locations & UI")]
    public Transform[] spawnPoints;     
    public TextMeshProUGUI waveText;  

    private int currentWave = 0;
    private int zombiesLeftToSpawnInWave = 0;
    private float spawnTimer;
    private bool isWaitingForNextWave = false;

    private List<GameObject> activeZombies = new List<GameObject>();

    void Start()
    {
        StartCoroutine(StartNextWave());
    }

    void Update()
    {
        activeZombies.RemoveAll(z => z == null);

        // ISTO E TEMPORARIO PARA TESTAR AS RONDAS (MATAR ZOMBIES SO A CLICAR NO K)
        //if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.kKey.wasPressedThisFrame)
        //{
        //    if (activeZombies.Count > 0)
        //    {
        //       GameObject zombieToKill = activeZombies.Find(z => z != null);
        //        if (zombieToKill != null) Destroy(zombieToKill);
        //    }
        //}
        // ------------------------------------------------

        // Start next wave if current one is completely clear
        if (zombiesLeftToSpawnInWave <= 0 && activeZombies.Count == 0 && !isWaitingForNextWave && currentWave > 0)
        {
            StartCoroutine(StartNextWave());
        }

        // Spawn zombies over time
        if (zombiesLeftToSpawnInWave > 0 && activeZombies.Count < maxZombiesOnMap && !isWaitingForNextWave)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnDelay)
            {
                SpawnZombie();
                zombiesLeftToSpawnInWave--; 
                spawnTimer = 0f;
            }
        }
    }

    IEnumerator StartNextWave()
    {
        isWaitingForNextWave = true;

        PlayerPoints[] allPlayers = FindObjectsByType<PlayerPoints>(FindObjectsInactive.Exclude);
        foreach (PlayerPoints player in allPlayers)
        {
            player.CommitPoints();
        }

        currentWave++;
        
        // UPDATE GUI TEXT
        if (waveText != null) 
        {
            waveText.text = currentWave.ToString();
        }

        yield return new WaitForSeconds(timeBetweenWaves);

        int waveIndex = currentWave - 1; // Wave 1 is Index 0
        if (waveIndex < zombiesPerWave.Length)
        {
            zombiesLeftToSpawnInWave = zombiesPerWave[waveIndex];
        }
        else
        {
            zombiesLeftToSpawnInWave = zombiesPerWave[zombiesPerWave.Length - 1];
        }
        
        isWaitingForNextWave = false;
    }

    void SpawnZombie()
    {
        if (zombiePrefab == null) return;

        Vector3 basePosition = transform.position;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform randomPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (randomPoint != null)
            {
                basePosition = randomPoint.position;
            }
        }

        Vector3 spawnPosition = basePosition + new Vector3(
            Random.Range(-5f, 5f), 
            0f, 
            Random.Range(-5f, 5f)
        );

        GameObject newZombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        activeZombies.Add(newZombie);
    }
}