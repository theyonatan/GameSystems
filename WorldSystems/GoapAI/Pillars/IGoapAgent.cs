using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(GoapAnimationMapper))]
public abstract class IGoapAgent : MonoBehaviour
{
    // goap
    protected Dictionary<string, AgentBelief> Beliefs;
    protected HashSet<AgentAction> Actions;
    protected HashSet<AgentGoal> Goals;
    
    private GoapRunner _gRunner;
    protected GoapAnimator GAnimator;
    
    // values
    [SerializeField] protected bool logAgent;
    protected float TimerInterval;
    
    // goap functions
    protected abstract void SetupBeliefs();
    protected abstract void SetupActions();
    protected abstract void SetupGoals();
    
    // optional functions
    protected virtual void UpdateStats() {}
    
    // references from the user
    protected SerializedDictionary<string, Transform> Locations;
    protected SerializedDictionary<string, Sensor> Sensors;
    
    // components
    private GoapAnimationMapper _animationMapper;
    private CountdownTimer _goapTimer;
    protected NavMeshAgent AgentNavmesh;
    protected Rigidbody Rb;
    
    // MonoBehaviour functions
    private void Awake()
    {
        var animator = GetComponent<Animator>();
        var animationMapper = GetComponent<GoapAnimationMapper>();
        AgentNavmesh = GetComponent<NavMeshAgent>();
        Rb = GetComponent<Rigidbody>();
        if (Rb != null)
            Rb.freezeRotation = true;
        
        _gRunner = new GoapRunner(Beliefs, Actions, Goals, PreActionReset, logAgent);
        GAnimator = new GoapAnimator(animator, animationMapper);
    }
    
    private void Start()
    {
        SetupTimers();
        SetupBeliefs();
        SetupActions();
        SetupGoals();
    }
    
    protected void Update()
    {
        // update timers
        _goapTimer.Tick(Time.deltaTime);
        GAnimator.UpdateAnimationsTimer(Time.deltaTime);
        
        // tell goap system to find what to do next or perform current action.
        _gRunner.Perform();
    }
    
    // helper functions
    protected void ResetActionAndGoal()
    {
        _gRunner.ResetActionAndGoal();
    }
    
    void SetupTimers()
    {
        _goapTimer = new CountdownTimer(2f);
        _goapTimer.OnTimerStop += () =>
        {
            UpdateStats();
            _goapTimer.Start();
        };
        _goapTimer.Start();
    }
    
    // this will happen before a new action occurs
    protected virtual void PreActionReset()
    {
        AgentNavmesh?.ResetPath();
    }
}
