using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[SelectionBase]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(GoapAnimator))]
public class GoapAgent : IGoapAgent
{
    [Header("Sensors")]
    private Sensor ChaseSensor => Sensors["ChaseSensor"];
    private Sensor AttackSensor => Sensors["AttackSensor"];
    
    [Header("Known Locations")]
    private Transform RestingPosition => Locations["RestingPosition"];
    private Transform FoodShack => Locations["FoodShack"];
    private Transform DoorOnePosition => Locations["DoorOnePosition"];
    private Transform DoorTwoPosition => Locations["DoorTwoPosition"];


    [Header("Stats")]
    private float _health = 100;
    private float _stamina = 100;

    #region Goap

    protected override void SetupBeliefs()
    {
        Beliefs = new Dictionary<string, AgentBelief>();
        BeliefFactory factory = new(this, Beliefs);

        factory.AddBelief("Nothing", () => false);

        factory.AddBelief("AgentIdle", () => !AgentNavmesh.hasPath);
        factory.AddBelief("AgentMoving", () => AgentNavmesh.hasPath);
        factory.AddBelief("AgentHealthLow", () => _health < 30);
        factory.AddBelief("AgentIsHealthy", () => _health >= 50);
        factory.AddBelief("AgentStaminaLow", () => _stamina < 10);
        factory.AddBelief("AgentIsRested", () => _stamina >= 50);

        factory.AddLocationBelief("AgentAtDoorOne", 3f, DoorOnePosition);
        factory.AddLocationBelief("AgentAtDoorTwo", 3f, DoorTwoPosition);
        factory.AddLocationBelief("AgentAtRestingPosition", 3f, RestingPosition);
        factory.AddLocationBelief("AgentAtFoodShack", 3f, FoodShack);

        factory.AddSensorBelief("PlayerInChaseRange", ChaseSensor);
        factory.AddSensorBelief("PlayerInAttackRange", AttackSensor);

        factory.AddBelief("AttackingPlayer", () => false); // Player can always be attacked, this will never become true
    }

    protected override void SetupActions()
    {
        Actions = new HashSet<AgentAction>
        {
            new AgentAction.Builder("Relax")
            .WithStrategy(new IdleStrategy(5))
            .AddEffect(Beliefs["Nothing"])
            .Build(),
            
            new AgentAction.Builder("Wander Around")
            .WithStrategy(new WanderStrategy(AgentNavmesh, 10))
            .AddEffect(Beliefs["AgentMoving"])
            .Build(),

            new AgentAction.Builder("MoveToEatingPosition")
            .WithStrategy(new MoveStrategy(AgentNavmesh, () => FoodShack.position, GAnimator))
            .AddEffect(Beliefs["AgentAtFoodShack"])
            .Build(),

            new AgentAction.Builder("Eat")
            .WithStrategy(new IdleStrategy(5)) // Later replace with a Command
            .AddPrecondition(Beliefs["AgentAtFoodShack"])
            .AddEffect(Beliefs["AgentIsHealthy"])
            .Build(),

            new AgentAction.Builder("MoveToDoorOne")
            .WithStrategy(new MoveStrategy(AgentNavmesh, () => DoorOnePosition.position, GAnimator))
            .AddEffect(Beliefs["AgentAtDoorOne"])
            .Build(),

            new AgentAction.Builder("MoveToDoorTwo")
            .WithStrategy(new MoveStrategy(AgentNavmesh, () => DoorTwoPosition.position, GAnimator))
            .AddEffect(Beliefs["AgentAtDoorTwo"])
            .Build(),

            new AgentAction.Builder("MoveFromDoorOneToRestArea")
            .WithStrategy(new MoveStrategy(AgentNavmesh, () => RestingPosition.position, GAnimator))
            .AddPrecondition(Beliefs["AgentAtDoorOne"])
            .AddEffect(Beliefs["AgentAtRestingPosition"])
            .Build(),

            new AgentAction.Builder("MoveFromDoorTwoToRestArea")
            .WithStrategy(new MoveStrategy(AgentNavmesh, () => RestingPosition.position, GAnimator))
            .WithCost(2)
            .AddPrecondition(Beliefs["AgentAtDoorTwo"])
            .AddEffect(Beliefs["AgentAtRestingPosition"])
            .Build(),

            new AgentAction.Builder("Rest")
            .WithStrategy(new IdleStrategy(5))
            .AddPrecondition(Beliefs["AgentAtRestingPosition"])
            .AddEffect(Beliefs["AgentIsRested"])
            .Build(),

            new AgentAction.Builder("ChasePlayer")
            .WithStrategy(new MoveStrategy(AgentNavmesh, () => Beliefs["PlayerInChaseRange"].Location, GAnimator))
            .AddPrecondition(Beliefs["PlayerInChaseRange"])
            .AddEffect(Beliefs["PlayerInAttackRange"])
            .Build(),
            
            new AgentAction.Builder("AttackPlayer")
            .WithStrategy(new AttackStrategy(GAnimator))
            .AddPrecondition(Beliefs["PlayerInAttackRange"])
            .AddEffect(Beliefs["AttackingPlayer"])
            .Build()
        };
    }

    protected override void SetupGoals()
    {
        Goals = new HashSet<AgentGoal>
        {
            new AgentGoal.Builder("Chill Out")
            .WithPriority(1)
            .WithDesiredEffect(Beliefs["Nothing"])
            .Build(),

            new AgentGoal.Builder("Wander")
            .WithPriority(1)
            .WithDesiredEffect(Beliefs["AgentMoving"])
            .Build(),

            new AgentGoal.Builder("KeepHealthUp")
            .WithPriority(2)
            .WithDesiredEffect(Beliefs["AgentIsHealthy"])
            .Build(),

            new AgentGoal.Builder("KeepStaminaUp")
            .WithPriority(2)
            .WithDesiredEffect(Beliefs["AgentIsRested"])
            .Build(),
            
            new AgentGoal.Builder("SeekAndDestroy")
            .WithPriority(3)
            .WithDesiredEffect(Beliefs["AttackingPlayer"])
            .Build()
        };
    }

    #endregion

    #region Updates

    protected override void UpdateStats()
    {
        _stamina += InRangeOf(RestingPosition.position, 3f) ? 20 : -10;
        _health += InRangeOf(FoodShack.position, 3f) ? 20 : -5;
        _stamina = Mathf.Clamp(_stamina, 0, 100);
        _health = Mathf.Clamp(_health, 0, 100);
    }

    #endregion


    #region HelperFunctions

    bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(transform.position, pos) < range;

    void OnEnable() => ChaseSensor.OnTargetChanged += HandleTargetChanged;
    void OnDisable() => ChaseSensor.OnTargetChanged -= HandleTargetChanged;

    void HandleTargetChanged()
    {
        Debug.Log("GOAP: Target changed, clearing current action and goal");
        
        ResetActionAndGoal();
    }

    #endregion
}
