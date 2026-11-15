using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class ZombieSpawner : NetworkBehaviour
{
    [Header("Wave Settings")]
    [Tooltip("Index 0 is Wave 1. If wave exceeds list, it uses the last number.")]
    public int[] zombiesPerWave = new int[] { 6, 8, 12, 15, 23, 25, 26, 31 };

    public GameObject zombiePrefab;
    public int maxZombiesOnMap = 10;
    public float timeBetweenWaves = 5f;
    public float spawnDelay = 1.5f;

    [Header("Spawn Locations & UI")]
    public Transform[] spawnPoints;
    public TextMeshProUGUI waveText;

    // Ronda partilhada: só o servidor escreve, todos leem
    private NetworkVariable<int> currentWave = new NetworkVariable<int>(0);

    private int zombiesLeftToSpawnInWave = 0;
    private float spawnTimer;
    private bool isWaitingForNextWave = false;

    private List<GameObject> activeZombies = new List<GameObject>();

    public override void OnNetworkSpawn()
    {
        currentWave.OnValueChanged += OnWaveChanged;
        OnWaveChanged(0, currentWave.Value); // para quem entra a meio da partida

        if (IsServer) StartCoroutine(StartNextWave());
    }

    public override void OnNetworkDespawn()
    {
        currentWave.OnValueChanged -= OnWaveChanged;
    }

    private void OnWaveChanged(int oldValue, int newValue)
    {
        if (waveText != null) waveText.text = newValue.ToString();
    }

    void Update()
    {
        if (!IsServer) return; // clientes não decidem nada

        activeZombies.RemoveAll(z => z == null);

        // Começa a ronda seguinte quando a atual está toda limpa
        if (zombiesLeftToSpawnInWave <= 0 && activeZombies.Count == 0 && !isWaitingForNextWave && currentWave.Value > 0)
        {
            StartCoroutine(StartNextWave());
        }

        // Cria zombies ao longo do tempo
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

        currentWave.Value++; // todos os clientes recebem o novo valor

        yield return new WaitForSeconds(timeBetweenWaves);

        int waveIndex = currentWave.Value - 1; // Wave 1 is Index 0
        if (waveIndex < zombiesPerWave.Length)
            zombiesLeftToSpawnInWave = zombiesPerWave[waveIndex];
        else
            zombiesLeftToSpawnInWave = zombiesPerWave[zombiesPerWave.Length - 1];

        isWaitingForNextWave = false;
    }

    void SpawnZombie()
    {
        if (zombiePrefab == null) return;

        Vector3 basePosition = transform.position;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform randomPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (randomPoint != null) basePosition = randomPoint.position;
        }

        Vector3 spawnPosition = basePosition + new Vector3(
            Random.Range(-5f, 5f),
            0f,
            Random.Range(-5f, 5f)
        );

        GameObject newZombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        newZombie.GetComponent<NetworkObject>().Spawn(); // aparece em todos os jogadores
        activeZombies.Add(newZombie);
    }
}