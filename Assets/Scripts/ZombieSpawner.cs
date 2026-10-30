using System.Collections.Generic;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Configuração de Spawn")]
    public GameObject zombiePrefab; //COLOCAR PREFAB ZOMBIE
    public int maxZombies = 4;      //
    public float spawnInterval = 3f; //TEMPO ENTRE SPAWNS

    private float spawnTimer;
    private List<GameObject> activeZombies = new List<GameObject>();

    void Update()
    {
        //LIMPAR ZOMBIES DESTRUIDOS/ELIMINADOS
        activeZombies.RemoveAll(z => z == null);

        //CONTADOR PARA SPAWN
        if (activeZombies.Count < maxZombies)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                SpawnZombie();
                spawnTimer = 0f;
            }
        }
    }

    void SpawnZombie()
    {
        if (zombiePrefab == null) return;

        //POSICAO ALEATORIA PARA SPAWN
        Vector3 spawnPosition = transform.position + new Vector3(
            Random.Range(-5f, 5f), 
            0f, 
            Random.Range(-5f, 5f)
        );

        //CRIAR INSTANCIA DE ZOMBIE
        GameObject newZombie = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        activeZombies.Add(newZombie);
    }
}