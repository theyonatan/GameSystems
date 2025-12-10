using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StatsSingleton : MonoBehaviour
{
    static StatsSingleton mInstance;

    public static StatsSingleton Instance
    {
        get
        {
            if (mInstance == null)
            {
                GameObject go = new("StatsManager");
                mInstance = go.AddComponent<StatsSingleton>();

                mInstance.m_stats = new Dictionary<StatType, Stat>
                {
                    { StatType.Money, new Stat(0f) },
                    { StatType.Health, new Stat(100f) }
                };

                mInstance.m_modifiers = new HashSet<Modifier>();
            }
            return mInstance;
        }
    }

    private Dictionary<StatType, Stat> m_stats;
    private HashSet<Modifier> m_modifiers;

    private void Update()
    {
        foreach (Modifier modifier in m_modifiers)
        {
            modifier.TimeRemaining -= Time.deltaTime;
            if (modifier.TimeRemaining <= 0f)
                RemoveModifier(modifier);
        }
    }

    public Stat GetStat(StatType type) => m_stats[type];

    public void SetStat(StatType type, float value) => m_stats[type].Value = value;

    public void AddStat(StatType type, float baseValue = 0f)
    {
        if (!m_stats.ContainsKey(type))
        {
            m_stats.Add(type, new Stat(baseValue));
        }
    }

    public void IncreamentStat(StatType type, float amount)
    {
        if (m_stats.ContainsKey(type))
            m_stats[type].Value += amount;
        else
            Debug.LogError($"Could not find stat {type}!");
    }

    public void DecreamentStat(StatType type, float amount)
    {
        if (m_stats.ContainsKey(type))
            m_stats[type].Value -= amount;
        else
            Debug.LogError($"Could not find stat {type}!");
    }

    public void AddOrResetModifier(Modifier modifier)
    {
        IEnumerable<Modifier> existingModifiersOfSameType = m_modifiers.Where(_modifier => _modifier.StatType == modifier.StatType);

        if (existingModifiersOfSameType.Count() == 0)
        {
            m_modifiers.Add(modifier);
            m_stats[modifier.StatType].ActivateModifier(modifier);
            return;
        }

        Modifier highestPriority = existingModifiersOfSameType
            .OrderByDescending(_modifier => _modifier.Priority)
            .FirstOrDefault();

        Modifier toActivate = modifier.Priority > highestPriority.Priority ?
            modifier : highestPriority;

        m_stats[modifier.StatType].DeactivateModifier();
        m_stats[modifier.StatType].ActivateModifier(toActivate);
    }

    public void RemoveModifier(Modifier modifier)
    {
        m_modifiers.Remove(modifier);

        m_stats[modifier.StatType].DeactivateModifier();

        IEnumerable<Modifier> existingModifiersOfSameType = m_modifiers.Where(_modifier => _modifier.StatType == modifier.StatType);

        if (existingModifiersOfSameType.Count() == 0)
            return;

        Modifier highestPriority = existingModifiersOfSameType
            .OrderByDescending(_modifier => _modifier.Priority)
            .FirstOrDefault();

        m_stats[modifier.StatType].ActivateModifier(modifier);
    }
    
    private void OnDestroy()
    {
        if (mInstance == this)
            mInstance = null;
    }
}

public class Stat
{
    private StatType m_type;
    private float m_Value;
    public event Action<float> OnStatChanged;

    private bool canBeModified;
    private float ValuebeforeModifier;
    private bool modifierAcivated;

    // Property to get/set the stat value
    public float Value
    {
        get => m_Value;
        set
        {
            if (m_Value != value)
            {
                m_Value = value;
                OnStatChanged?.Invoke(m_Value);
            }
        }
    }

    public Stat(float initialValue, bool allowModification = false) => (m_Value, canBeModified) = (initialValue, allowModification);

    public void ActivateModifier(Modifier modifier)
    {
        if (!canBeModified)
        {
            Debug.LogError($"tried to activate a modifier that can't be modified! {modifier.StatType}");
            return;
        }
        if (modifierAcivated)
            Debug.LogError($"modifier is already activated for {modifier.StatType}!");
        else
        {
            modifierAcivated = true;
            ValuebeforeModifier = m_Value;
            m_Value += modifier.Value;
        }
    }

    public void DeactivateModifier()
    {
        if (!canBeModified)
        {
            Debug.LogError($"tried to deactivate a modifier that can't be modified! {m_type}");
            return;
        }
        if (!modifierAcivated)
            Debug.LogError($"No modifier is activated for {m_type}!");
        else
        {
            modifierAcivated = false;
            m_Value = ValuebeforeModifier;
        }
    }
}
