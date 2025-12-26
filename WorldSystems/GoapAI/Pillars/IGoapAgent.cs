using System.Collections.Generic;
using System.Linq;
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

    [SerializeField] protected bool goapAgentEnabled = true;
    
    // values
    [SerializeField] protected bool logAgent;
    
    // goap functions
    protected abstract void SetupBeliefs();
    protected abstract void SetupActions();
    protected abstract void SetupGoals();
    
    // optional functions
    protected virtual void UpdateStats() {}
    protected virtual void OnStart() {}
    
    // references from the user
    [SerializedDictionary] public SerializedDictionary<string, Sensor> Sensors;
    [SerializedDictionary] public SerializedDictionary<string, Transform> Locations;
    
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
        OnStart();
        SetupTimers();
        SetupBeliefs();
        SetupActions();
        SetupGoals();
        
        // update runner after settingup goap stuff
        _gRunner.UpdateRunner(Beliefs, Actions, Goals);

        // subscribe to sensor detection event
        foreach (var sensor in Sensors.Values.Where(sensor => sensor.ResetGoapOnTargetChange))
            sensor.OnTargetChanged += ResetActionAndGoal;
    }
    
    protected void Update()
    {
        // update timers
        _goapTimer.Tick(Time.deltaTime);
        GAnimator.UpdateAnimationsTimer(Time.deltaTime);
        
        // don't control the agent if disabled.
        if (!goapAgentEnabled)
            return;
        
        // tell goap system to find what to do next or perform current action.
        _gRunner.Perform();
    }

    private void OnDestroy()
    {
        // Unsubscribe from sensor detection event
        foreach (var sensor in Sensors.Values.Where(sensor => sensor.ResetGoapOnTargetChange))
            sensor.OnTargetChanged -= ResetActionAndGoal;
    }

    // helper functions
    public void ResetActionAndGoal()
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

    public void EnableGoap()
    {
        goapAgentEnabled = true;
    }

    public void DisableGoap()
    {
        ResetActionAndGoal();
        AgentNavmesh?.ResetPath();
        
        goapAgentEnabled = false;
    }

    public void StopGoap()
    {
        goapAgentEnabled = false;
    }

    public bool IsEnabled()
    {
        return goapAgentEnabled;
    }
}
