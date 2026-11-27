using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks the run (zombies killed, time, round) and ends the game when EVERY player is dead.
/// Add it to an empty object in the game scene together with a NetworkObject (in-scene placed, not a prefab).
///
/// Server only: counts kills, detects game over, restarts the game.
/// Everyone: gets the GameOver event with the final stats, which the GameOverScreen UI listens to.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Fires on every machine when the game ends: zombies killed, round reached, seconds survived.</summary>
    public static event Action<int, int, float> GameOver;

    [Header("References (optional)")]
    [Tooltip("Found automatically if left empty. Used to read the current round and to stop spawning at game over.")]
    public ZombieSpawner spawner;

    /// <summary>Total zombies killed by the team. Synced to everyone, so you can use it for a live HUD too.</summary>
    public NetworkVariable<int> zombiesKilled = new NetworkVariable<int>(0);

    public bool IsGameOver { get; private set; }

    private float startTime;
    private float nextCheckTime;
    private bool restarting;

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (!IsServer) return;
        startTime = Time.time;
        if (spawner == null) spawner = FindFirstObjectByType<ZombieSpawner>();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Called by ZombieHealth when a zombie dies. Only counts on the server.</summary>
    public void AddKill()
    {
        if (!IsServer || IsGameOver) return;
        zombiesKilled.Value++;
    }

    private void Update()
    {
        if (!IsServer || IsGameOver) return;

        // Check twice a second, it doesn't need to run every frame
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + 0.5f;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        if (players.Length == 0) return;

        foreach (PlayerHealth player in players)
            if (player.currentHealth.Value > 0f) return; // someone is still alive

        EndGame();
    }

    // ---------------------------------------------------------------- Game over
    private void EndGame()
    {
        IsGameOver = true;

        int round = spawner != null ? spawner.CurrentWave : 0;
        float secondsSurvived = Time.time - startTime;

        if (spawner != null) spawner.enabled = false; // no more zombies

        ShowGameOverClientRpc(zombiesKilled.Value, round, secondsSurvived);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowGameOverClientRpc(int kills, int round, float secondsSurvived)
    {
        IsGameOver = true;
        GameOver?.Invoke(kills, round, secondsSurvived);
    }

    // ---------------------------------------------------------------- Restart
    /// <summary>Any player can call this (the Restart button does). The server reloads the scene for everyone.</summary>
    public void RequestRestart()
    {
        if (IsServer) Restart();
        else RestartServerRpc();
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RestartServerRpc()
    {
        Restart();
    }

    private void Restart()
    {
        if (restarting) return;
        restarting = true;

        DespawnAllZombies();

        var manager = NetworkManager.Singleton;

        // Static handler on purpose: this object is destroyed by the scene reload, the handler must survive it
        manager.SceneManager.OnLoadEventCompleted += RespawnPlayersAfterLoad;
        manager.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    /// <summary>
    /// Removes every zombie still alive. Zombies are spawned at runtime and Netcode keeps runtime-spawned objects
    /// across a scene reload (unless they were spawned 'with scene'), so they have to be removed by hand.
    /// </summary>
    private static void DespawnAllZombies()
    {
        foreach (ZombieHealth zombie in FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None))
        {
            NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
                networkObject.Despawn(true);
        }
    }

    /// <summary>
    /// After the scene reloads, every player gets a brand new player object (full health, starting weapons,
    /// zero points). Player objects can survive a scene change, so the old one is removed first.
    /// </summary>
    private static void RespawnPlayersAfterLoad(string sceneName, LoadSceneMode mode,
                                                List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        var manager = NetworkManager.Singleton;
        manager.SceneManager.OnLoadEventCompleted -= RespawnPlayersAfterLoad;

        GameObject prefab = manager.NetworkConfig.PlayerPrefab;

        foreach (ulong clientId in new List<ulong>(manager.ConnectedClientsIds))
        {
            NetworkObject oldPlayer = manager.SpawnManager.GetPlayerNetworkObject(clientId);
            if (oldPlayer != null) oldPlayer.Despawn(true);

            // Same position the player prefab uses when someone first joins
            GameObject fresh = Instantiate(prefab, prefab.transform.position, prefab.transform.rotation);
            fresh.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }
    }
}