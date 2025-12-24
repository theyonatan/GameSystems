using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class GoapRunner
{
    // goap
    private Dictionary<string, AgentBelief> _beliefs;
    private HashSet<AgentAction> _actions;
    private HashSet<AgentGoal> _goals;
    
    // runner
    private ActionPlan _actionPlan;
    private AgentAction _currentAction;
    private AgentGoal _currentGoal;
    private AgentGoal _lastGoal;

    private readonly IGoapPlanner _gPlanner;
    private readonly UnityAction _preActionReset;

    private bool _logGoap;

    public GoapRunner(
        Dictionary<string, AgentBelief> beliefs,
        HashSet<AgentAction> actions,
        HashSet<AgentGoal> goals,
        UnityAction preActionReset,
        bool logAgent)
    {
        _preActionReset = preActionReset;
        _beliefs = beliefs;
        _actions = actions;
        _goals = goals;
        
        _logGoap = logAgent;
        _gPlanner = new GoapPlanner(logAgent);
    }

    public void UpdateRunner(
        Dictionary<string, AgentBelief> beliefs,
        HashSet<AgentAction> actions,
        HashSet<AgentGoal> goals)
    {
        _beliefs = beliefs;
        _actions = actions;
        _goals = goals;
    }
    
    public void Perform()
    {
        // Update the plan and current action if there is one
        if (_currentAction == null)
        {
            UpdatePlanAndAction();
        }

        // If we have a current action, execute it
        if (_actionPlan != null && _currentAction != null)
        {
            ExecuteAction();
        }
    }

    private void UpdatePlanAndAction()
    {
        LogGoap("GOAP: Calculating any potential new plan");
        CalculatePlan();

        if (_actionPlan != null && _actionPlan.Actions.Count > 0)
        {
            _preActionReset();

            _currentGoal = _actionPlan.AgentGoal;
            string planToPrint = string.Join("-> ", _actionPlan.Actions.Select(g => g.Name));
            LogGoap($"GOAP: Goal: {_currentGoal.Name} with {_actionPlan.Actions.Count} actions in plan: {planToPrint}");
            _currentAction = _actionPlan.Actions.Pop();
            LogGoap($"GOAP: Popped action: {_currentAction.Name}");
            if (_logGoap) LogPreconditions();
            // Verify all precondition effects are true
            if (_currentAction.Preconditions.All(b => b.Evaluate()))
            {
                _currentAction.Start();
            }
            else
            {
                LogGoap("Preconditions not met, clearing current action and goal");
                _currentAction = null;
                _currentGoal = null;
            }
        }
    }

    private void ExecuteAction()
    {
        _currentAction.Update(Time.deltaTime);

        if (_currentAction.Complete)
        {
            Debug.Log($"GOAP: {_currentAction.Name} complete");
            _currentAction.Stop();
            _currentAction = null;

            if (_actionPlan.Actions.Count == 0)
            {
                Debug.Log("GOAP: Plan complete");
                _lastGoal = _currentGoal;
                _currentGoal = null;
            }
        }
    }
    
    private void CalculatePlan()
    {
        var priorityLevel = _currentGoal?.Priority ?? 0;

        HashSet<AgentGoal> goalsToCheck = _goals;

        // If we have a current goal, we only want to check goals with higher priority
        if (_currentGoal != null)
        {
            Debug.Log("GOAP: Current goal exists, checking goals with higher priority");
            goalsToCheck = new HashSet<AgentGoal>(_goals.Where(g => g.Priority > priorityLevel));
        }

        var potentialPlan = _gPlanner.Plan(_actions, goalsToCheck, _lastGoal);
        if (potentialPlan != null)
        {
            _actionPlan = potentialPlan;
        }
    }

    public void ResetActionAndGoal()
    {
        // Forces the planner to re-evaluate the plan
        _currentAction = null;
        _currentGoal = null;
    }

    private void LogPreconditions()
    {
        foreach (var precondition in _currentAction.Preconditions)
        {
            Debug.Log($"GOAP: Precondition: {precondition.Name} - {precondition.Evaluate()}");
        }
    }

    private void LogGoap(string logMessage)
    {
        if (!_logGoap)
            return;

        Debug.Log(logMessage);
    }
}
