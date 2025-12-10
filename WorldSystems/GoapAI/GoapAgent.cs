using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;


[SelectionBase]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AnimationController))]
public class GoapAgent : IGoapAgent
{
    [Header("Sensors")]
    [SerializeField] Sensor chaseSensor;
    [SerializeField] Sensor attackSensor;

    [Header("Known Locations")]
    [SerializeField] Transform restingPosition;
    [SerializeField] Transform foodShack;
    [SerializeField] Transform doorOnePosition;
    [SerializeField] Transform doorTwoPosition;

    NavMeshAgent navMeshAgent;
    AnimationController animations;
    Rigidbody rb;

    [Header("Stats")]
    public float health = 100;
    public float stamina = 100;

    CountdownTimer timer;
    CountdownTimer statsTimer;

    GameObject target;
    Vector3 destination;

    AgentGoal lastGoal;
    public AgentGoal currentGoal;
    public ActionPlan actionPlan;
    public AgentAction currentAction;

    public Dictionary<string, AgentBelief> beliefs;
    public HashSet<AgentGoal> goals;

    IGoapPlanner gPlanner;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animations = GetComponent<AnimationController>();
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        gPlanner = new GoapPlanner();
    }

    private void Start()
    {
        SetupTimers();
        SetupBeliefs();
        SetupActions();
        SetupGoals();
    }

    void SetupBeliefs()
    {
        beliefs = new Dictionary<string, AgentBelief>();
        BeliefFactory factory = new(this, beliefs);

        factory.AddBelief("Nothing", () => false);

        factory.AddBelief("AgentIdle", () => !navMeshAgent.hasPath);
        factory.AddBelief("AgentMoving", () => navMeshAgent.hasPath);
        factory.AddBelief("AgentHealthLow", () => health < 30);
        factory.AddBelief("AgentIsHealthy", () => health >= 50);
        factory.AddBelief("AgentStaminaLow", () => stamina < 10);
        factory.AddBelief("AgentIsRested", () => stamina >= 50);

        factory.AddLocationBelief("AgentAtDoorOne", 3f, doorOnePosition);
        factory.AddLocationBelief("AgentAtDoorTwo", 3f, doorTwoPosition);
        factory.AddLocationBelief("AgentAtRestingPosition", 3f, restingPosition);
        factory.AddLocationBelief("AgentAtFoodShack", 3f, foodShack);

        factory.AddSensorBelief("PlayerInChaseRange", chaseSensor);
        factory.AddSensorBelief("PlayerInAttackRange", attackSensor);

        factory.AddBelief("AttackingPlayer", () => false); // Player can always be attacked, this will never become true
    }

    void SetupActions()
    {
        actions = new HashSet<AgentAction>
        {
            new AgentAction.Builder("Relax")
            .WithStrategy(new IdleStrategy(5))
            .AddEffect(beliefs["Nothing"])
            .Build(),
            
            new AgentAction.Builder("Wander Around")
            .WithStrategy(new WanderStrategy(navMeshAgent, 10))
            .AddEffect(beliefs["AgentMoving"])
            .Build(),

            new AgentAction.Builder("MoveToEatingPosition")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => foodShack.position, animations))
            .AddEffect(beliefs["AgentAtFoodShack"])
            .Build(),

            new AgentAction.Builder("Eat")
            .WithStrategy(new IdleStrategy(5)) // Later replace with a Command
            .AddPrecondition(beliefs["AgentAtFoodShack"])
            .AddEffect(beliefs["AgentIsHealthy"])
            .Build(),

            new AgentAction.Builder("MoveToDoorOne")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => doorOnePosition.position, animations))
            .AddEffect(beliefs["AgentAtDoorOne"])
            .Build(),

            new AgentAction.Builder("MoveToDoorTwo")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => doorOnePosition.position, animations))
            .AddEffect(beliefs["AgentAtDoorTwo"])
            .Build(),

            new AgentAction.Builder("MoveFromDoorOneToRestArea")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => restingPosition.position, animations))
            .AddPrecondition(beliefs["AgentAtDoorOne"])
            .AddEffect(beliefs["AgentAtRestingPosition"])
            .Build(),

            new AgentAction.Builder("MoveFromDoorTwoToRestArea")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => restingPosition.position, animations))
            .WithCost(2)
            .AddPrecondition(beliefs["AgentAtDoorTwo"])
            .AddEffect(beliefs["AgentAtRestingPosition"])
            .Build(),

            new AgentAction.Builder("Rest")
            .WithStrategy(new IdleStrategy(5))
            .AddPrecondition(beliefs["AgentAtRestingPosition"])
            .AddEffect(beliefs["AgentIsRested"])
            .Build(),

            new AgentAction.Builder("ChasePlayer")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => beliefs["PlayerInChaseRange"].Location, animations))
            .AddPrecondition(beliefs["PlayerInChaseRange"])
            .AddEffect(beliefs["PlayerInAttackRange"])
            .Build(),
            
            new AgentAction.Builder("AttackPlayer")
            .WithStrategy(new AttackStrategy(animations))
            .AddPrecondition(beliefs["PlayerInAttackRange"])
            .AddEffect(beliefs["AttackingPlayer"])
            .Build()
        };
    }

    void SetupGoals()
    {
        goals = new HashSet<AgentGoal>
        {
            new AgentGoal.Builder("Chill Out")
            .WithPriority(1)
            .WithDesiredEffect(beliefs["Nothing"])
            .Build(),

            new AgentGoal.Builder("Wander")
            .WithPriority(1)
            .WithDesiredEffect(beliefs["AgentMoving"])
            .Build(),

            new AgentGoal.Builder("KeepHealthUp")
            .WithPriority(2)
            .WithDesiredEffect(beliefs["AgentIsHealthy"])
            .Build(),

            new AgentGoal.Builder("KeepStaminaUp")
            .WithPriority(2)
            .WithDesiredEffect(beliefs["AgentIsRested"])
            .Build(),
            
            new AgentGoal.Builder("SeekAndDestroy")
            .WithPriority(3)
            .WithDesiredEffect(beliefs["AttackingPlayer"])
            .Build()
        };
    }

    void SetupTimers()
    {
        statsTimer = new CountdownTimer(2f);
        statsTimer.OnTimerStop += () =>
        {
            UpdateStats();
            statsTimer.Start();
        };
        statsTimer.Start();
    }

    // TODO Move to stats system
    void UpdateStats()
    {
        stamina += InRangeOf(restingPosition.position, 3f) ? 20 : -10;
        health += InRangeOf(foodShack.position, 3f) ? 20 : -5;
        stamina = Mathf.Clamp(stamina, 0, 100);
        health = Mathf.Clamp(health, 0, 100);
    }

    bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(transform.position, pos) < range;

    void OnEnable() => chaseSensor.OnTargetChanged += HandleTargetChanged;
    void OnDisable() => chaseSensor.OnTargetChanged -= HandleTargetChanged;

    void HandleTargetChanged()
    {
        Debug.Log("GOAP: Target changed, clearing current action and goal");
        // Force the planner to re-evaluate the plan
        currentAction = null;
        currentGoal = null;
    }

    private void Update()
    {
        statsTimer.Tick(Time.deltaTime);

        // update animations
        AgentUtils.DetectMovement(navMeshAgent, animations.animator);
        animations.animator.SetFloat("Speed", navMeshAgent.velocity.magnitude);

        // Update the plan and current action if there is one
        if (currentAction == null)
        {
            Debug.Log("GOAP: Calculating any potential new plan");
            CalculatePlan();

            if (actionPlan != null && actionPlan.Actions.Count > 0)
            {
                navMeshAgent.ResetPath();

                currentGoal = actionPlan.AgentGoal;
                string planToPrint = string.Join("-> ", actionPlan.Actions.Select(g => g.Name));
                Debug.Log($"GOAP: Goal: {currentGoal.Name} with {actionPlan.Actions.Count} actions in plan: {planToPrint}");
                currentAction = actionPlan.Actions.Pop();
                Debug.Log($"GOAP: Popped action: {currentAction.Name}");
                // Verify all precondition effects are true
                if (currentAction.Preconditions.All(b => b.Evaluate()))
                {
                    currentAction.Start();
                }
                else
                {
                    Debug.Log("Preconditions not met, clearing current action and goal");
                    currentAction = null;
                    currentGoal = null;
                }
            }
        }

        // If we have a current action, execute it
        if (actionPlan != null && currentAction != null)
        {
            currentAction.Update(Time.deltaTime);

            if (currentAction.Complete)
            {
                Debug.Log($"GOAP: {currentAction.Name} complete");
                currentAction.Stop();
                currentAction = null;

                if (actionPlan.Actions.Count == 0)
                {
                    Debug.Log("GOAP: Plan complete");
                    lastGoal = currentGoal;
                    currentGoal = null;
                }
            }
        }
    }

    void CalculatePlan()
    {
        var priorityLevel = currentGoal?.Priority ?? 0;

        HashSet<AgentGoal> goalsToCheck = goals;

        // If we have a current goal, we only want to check goals with higher priority
        if (currentGoal != null)
        {
            Debug.Log("GOAP: Current goal exists, checking goals with higher priority");
            goalsToCheck = new HashSet<AgentGoal>(goals.Where(g => g.Priority > priorityLevel));
        }

        var potentialPlan = gPlanner.Plan(this, goalsToCheck, lastGoal);
        if (potentialPlan != null)
        {
            actionPlan = potentialPlan;
        }
    }
}
