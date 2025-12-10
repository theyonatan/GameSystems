using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeliefFactory
{
    readonly IGoapAgent agent;
    readonly Dictionary<string, AgentBelief> beliefs;

    public BeliefFactory(IGoapAgent agent, Dictionary<string, AgentBelief> beliefs)
    {
        this.agent = agent;
        this.beliefs = beliefs;
    }

    public void AddBelief(string key, Func<bool> condition)
    {
        beliefs.Add(key, new AgentBelief.Builder(key)
            .WithCondition(condition)
            .Build());
    }
    
    public void AddFalseBelief(string key, Func<bool> condition)
    {
        beliefs.Add(key, new AgentBelief.Builder(key)
            .WithCondition(()  => !condition())
            .Build());
    }

    public void AddSensorBelief(string key, params Sensor[] sensors)
    {
        beliefs.Add(key, new AgentBelief.Builder(key)
            .WithCondition(() => // condition to activate the sensor
            {
                foreach (Sensor sensor in sensors)
                    if (sensor.IsTargetInRange)
                        return true;
                return false;
            })
            .WithLocation(() => // location for when the sensor detected something
            {
                foreach (Sensor sensor in sensors)
                    if (sensor.IsTargetInRange)
                        return sensor.TargetPosition;
                
                foreach (Sensor sensor in sensors)
                    Debug.Log(sensor.name + ": " + sensor.IsTargetInRange);
                Debug.LogError($"WTF? Sensor {key} detected something (condition returned true) but does not hold anything!");
                Debug.Break();
                return Vector3.zero;
            })
            .Build());
    }

    public void AddSensorFalseBelief(string key, Sensor sensor)
    {
        beliefs.Add(key, new AgentBelief.Builder(key)
            .WithCondition(() => !sensor.IsTargetInRange)
            .WithLocation(() => sensor.TargetPosition)
            .Build());
    }

    public void AddLocationBelief(string key, float distance, Transform locationCondition)
    {
        AddLocationBelief(key, distance, locationCondition.position);
    }

    public void AddLocationBelief(string key, float distance, Vector3 locationCondition)
    {
        beliefs.Add(key, new AgentBelief.Builder(key)
            .WithCondition(() => InRangeOf(locationCondition, distance))
            .WithLocation(() => locationCondition)
            .Build());
    }

    bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(agent.transform.position, pos) < range;
}

public class AgentBelief
{
    public string Name { get; }

    Func<bool> condition = () => false;
    Func<Vector3> observedLocation = () => Vector3.zero;

    public Vector3 Location => observedLocation();

    AgentBelief(string name)
    {
        Name = name;
    }

    public bool Evaluate() => condition();

    public class Builder
    {
        readonly AgentBelief belief;

        public Builder(string name)
        {
            belief = new AgentBelief(name);
        }

        public Builder WithCondition(Func<bool> condition)
        {
            belief.condition = condition;
            return this;
        }

        public Builder WithLocation(Func<Vector3> observedLocation)
        {
            belief.observedLocation = observedLocation;
            return this;
        }

        public AgentBelief Build()
        {
            return belief;
        }
    }
}
