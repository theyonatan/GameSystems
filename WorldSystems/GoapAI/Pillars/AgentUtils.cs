using UnityEngine;
using UnityEngine.AI;

public class AgentUtils
{
    public static void DetectMovement(NavMeshAgent agent, Animator animator)
    {
        animator.SetBool("IsMoving", agent.velocity.sqrMagnitude >= 0.04f);
    }
}
