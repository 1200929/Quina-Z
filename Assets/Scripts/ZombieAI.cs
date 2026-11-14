using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : NetworkBehaviour
{
    private NavMeshAgent agent;
    private Transform target;
    private float nextRetarget;

    public override void OnNetworkSpawn()
    {
        agent = GetComponent<NavMeshAgent>();

        if (!IsServer)
        {
            // nos clientes quem move o zombie é o NetworkTransform
            agent.enabled = false;
            enabled = false;
        }
    }

    void Update()
    {
        if (agent == null || !agent.enabled) return;

        // escolhe o jogador mais próximo, 2 vezes por segundo
        if (Time.time >= nextRetarget)
        {
            target = FindClosestPlayer();
            nextRetarget = Time.time + 0.5f;
        }

        if (target != null) agent.SetDestination(target.position);
    }

    private Transform FindClosestPlayer()
    {
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = client.PlayerObject;
            if (obj == null) continue;

            float d = (obj.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = obj.transform;
            }
        }
        return best;
    }
}