using System;
using UnityEngine;
using UnityEngine.AI;

public interface IActionStrategy
{
    bool CanPerform { get; }
    bool Complete { get; }

    void Start()
    {
        
    }

    void Update(float deltaTime)
    {

    }

    void Stop()
    {

    }
}

public class IdleStrategy : IActionStrategy
{
    public bool CanPerform => true;
    public bool Complete { get; private set; }

    private readonly CountdownTimer _timer;

    public IdleStrategy(float duration)
    {
        _timer = new CountdownTimer(duration);
        _timer.OnTimerStart += () => Complete = false;
        _timer.OnTimerStop += () => Complete = true;
    }

    public void Start() => _timer.Start();
    public void Update(float deltaTime) => _timer.Tick(deltaTime);
}

public class WanderStrategy : IActionStrategy
{
    private readonly NavMeshAgent _agent;
    private readonly float _wanderRadius;

    public bool CanPerform => !Complete;
    public bool Complete => _agent.remainingDistance <= 2f && !_agent.pathPending;

    public WanderStrategy(NavMeshAgent agent, float wanderRadius)
    {
        _agent = agent;
        _wanderRadius = wanderRadius;
    }

    public void Start()
    {
        for (int i = 0; i < 5; i++)
        {
            Vector3 randomDirection = (UnityEngine.Random.insideUnitSphere * _wanderRadius).With(y: 0);
            NavMeshHit hit;

            if (NavMesh.SamplePosition(_agent.transform.position + randomDirection, out hit, _wanderRadius, 1))
            {
                _agent.SetDestination(hit.position);
                return;
            }
        }
    }
}

public class MoveStrategy : IActionStrategy
{
    private readonly NavMeshAgent _agent;
    private readonly Func<Vector3> _destination;
    private readonly AnimationController _animations;

    public bool CanPerform => !Complete;
    public bool Complete => _agent.remainingDistance <= 1f && !_agent.pathPending;
    public Action OnComplete = null;

    public MoveStrategy(NavMeshAgent agent, Func<Vector3> destination, AnimationController animations, Action onComplete = null)
    {
        _agent = agent;
        _destination = destination;
        _animations = animations;
        OnComplete = onComplete;
    }

    public void Start()
    {
        _agent.SetDestination(_destination());
    }

    public void Stop()
    {
        if (OnComplete != null)
            OnComplete();
        _agent.ResetPath();
    }
}

public class ChaseStrategy : IActionStrategy
{
    private readonly NavMeshAgent _agent;
    private readonly Func<Vector3> _destination;

    public bool CanPerform => !Complete;
    public bool Complete => _agent.remainingDistance <= 1f && !_agent.pathPending;
    public Action OnComplete = null;

    public ChaseStrategy(NavMeshAgent agent, Func<Vector3> destination, Action onComplete = null)
    {
        _agent = agent;
        _destination = destination;
        OnComplete = onComplete;
    }

    public void Start()
    {
        _agent.SetDestination(_destination());
    }

    public void Stop()
    {
        if (OnComplete != null)
            OnComplete();
        _agent.ResetPath();
    }
}

public class AttackStrategy : IActionStrategy
{
    public bool CanPerform => true;
    public bool Complete { get; private set; }

    private readonly CountdownTimer _timer;
    private readonly AnimationController _animations;
    
    public AttackStrategy(AnimationController animations)
    {
        _animations = animations;
        _timer = new CountdownTimer(animations.GetAnimationLength(animations.animationClips["AttackClip"]));
        _timer.OnTimerStart += () => Complete = false;
        _timer.OnTimerStop += () => Complete = true;
    }

    public void Start()
    {
        _timer.Start();
        _animations.animator.SetTrigger("Attack");
    }

    public void Update(float deltaTime) => _timer.Tick(deltaTime);
}
public class DanceStrategy : IActionStrategy
{
    public bool CanPerform => true;
    public bool Complete { get; private set; }

    private readonly CountdownTimer _timer;
    private readonly AnimationController _animations;

    public DanceStrategy(AnimationController animations)
    {
        _animations = animations;
        _timer = new CountdownTimer(animations.GetAnimationLength(animations.animationClips["DanceClip"]));
        _timer.OnTimerStart += () => Complete = true;
        _timer.OnTimerStop += () => Complete = false;
    }

    public void Start()
    {
        _timer.Start();
        _animations.PlayAnimationUsingTimer(_animations.animationClips["DanceClip"]);
    }

    public void Update(float deltaTime) => _timer.Tick(deltaTime);
}

public class WaitUntilBeliefFalseStrategy : IActionStrategy
{
    private readonly AgentBelief _waitingBelief;
    public bool CanPerform => true;

    public bool Complete { get; private set; }

    public WaitUntilBeliefFalseStrategy(AgentBelief beleif)
    {
        _waitingBelief = beleif;
        Complete = false;
    }

    public void Start()
    {
        // Initialize any necessary state
        Complete = false;
    }

    public void Update(float deltaTime)
    {
        // Keep waiting as long as the player is in cashier range
        if (!_waitingBelief.Evaluate())
        {
            Complete = true;
        }
    }
}

public class LookAtStrategy : IActionStrategy
{
    private readonly NavMeshAgent _agent;
    private readonly Func<Vector3> _lookAtPosition;
    private readonly Transform _transform;
    private readonly float _rotationSpeed;

    public bool CanPerform => !Complete;
    public bool Complete { get; private set; }
    public Action OnComplete = null;

    public LookAtStrategy(NavMeshAgent agent, Func<Vector3> lookAtPosition, float rotationSpeed = 5f, Action onComplete = null)
    {
        _agent = agent;
        _transform = agent.transform;
        _lookAtPosition = lookAtPosition;
        _rotationSpeed = rotationSpeed;
        OnComplete = onComplete;
    }

    public void Start()
    {
        Complete = false;
        _agent.SetDestination(_lookAtPosition());
    }

    public void Update(float deltaTime)
    {
        if (_agent.pathPending) return;

        Vector3 destination = _lookAtPosition();
        Vector3 direction = (destination - _transform.position).With(y: 0f).normalized;

        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        _transform.rotation = Quaternion.RotateTowards(_transform.rotation, targetRotation, _rotationSpeed * deltaTime * 100f);

        // Mark complete when mostly facing the target
        float angle = Quaternion.Angle(_transform.rotation, targetRotation);
        if (angle < 5f && _agent.remainingDistance <= 1f)
        {
            Complete = true;
            OnComplete?.Invoke();
        }
    }

    public void Stop()
    {
        OnComplete?.Invoke();
        _agent.ResetPath();
    }
}
