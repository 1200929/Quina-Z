using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    private Transform player;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        //ENCONTRA PELA TAG "Player" NO NOME DO OBJETO
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player != null)
        {
            agent.SetDestination(player.position);
        }
    }
}