using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : NetworkBehaviour
{

    [Header("Stats")]
    public float attackDamage = 30f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    
    private NavMeshAgent agent;
    private Transform target;
    private float nextRetarget;
    private float nextAttackTime;

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

        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            
            // Attack logic (Stop moving when close)
            if (distance <= attackRange)
            {
                agent.isStopped = true;
                if (Time.time >= nextAttackTime)
                {
                    Attack();
                }
            }
            else
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
            }
        }
    }
    private void Attack()
    {
        nextAttackTime = Time.time + attackCooldown;

        // Damage the player using the shared IDamageable interface
        IDamageable playerHealth = target.GetComponentInParent<IDamageable>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(new DamageInfo { damage = attackDamage });
        }
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