using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// In Game Clock
/// Opens up events
/// </summary>
public class DayClock : MonoBehaviour
{
#region Configurations
    // normalized time. 0 = midnight, 0.5 = noon.
    [Range(0f, 1f)] public float CurrentTime = 0f;

    [Tooltip("Seconds for 24-hour cycle")]
    public float CycleLength = 1200f; // 20 min by default

    [Tooltip("Global multiplier for speed-up/down")]
    public float TimeScale = 1f;

    // event for every time change
    [SerializableAttribute] public class FloatEvent : UnityEvent<float> { }
    public FloatEvent OnTimeChanged = new();
#endregion

#region Timed_Events
    // Time events
    public UnityEvent OnSunrise = new();
    public UnityEvent OnNoon = new();
    public UnityEvent OnSunset = new();
    public UnityEvent OnMidnight = new();

    // Generic time event - allowing calls for specific times
    private readonly SortedDictionary<float, UnityEvent> customTimeEvents = new();
    private float previousTime;

    /// <summary>
    /// Adds a time-based event at a normalized time (0 - 1)
    /// If repeatDaily is false, it will only trigger once.
    /// </summary>
    public UnityEvent AddEventNormlized(float normalizedTime, bool repeatDaily = true)
    {
        // Normalize the time
        normalizedTime = Mathf.Repeat(normalizedTime, 1f);
        
        // get or create the event for this time
        if (!customTimeEvents.TryGetValue(normalizedTime, out var timedEvent))
        {
            timedEvent = new UnityEvent();
            customTimeEvents.Add(normalizedTime, timedEvent);
        }

        if (!repeatDaily)
        {
            // create Wrapper for the event that triggers once
            UnityEvent singleUseEvent = new();
            singleUseEvent.AddListener(() =>
            {
                timedEvent.Invoke();
                customTimeEvents.Remove(normalizedTime);
            });
            return singleUseEvent;
        }

        return timedEvent;
    }

    public UnityEvent AddEventHHMM(int hhmm, bool repeatDaily = true)
    {
        int hour   = hhmm / 100;
        int minute = hhmm % 100;

        float normalized = ((hour * 60f) + minute); // 1440f
        
        return AddEventNormlized(normalized, repeatDaily);
    }
#endregion

#region HandleTime
    private void Update()
    {
        // calculate normalized "current time"
        previousTime = CurrentTime;
        CurrentTime = Mathf.Repeat(CurrentTime + (Time.deltaTime * TimeScale) / CycleLength, 1f);

        // Fire generic update
        OnTimeChanged?.Invoke(CurrentTime);

        // Built-in events
        CheckAndInvokeTimedEvent(0f, OnMidnight);
        CheckAndInvokeTimedEvent(0.25f, OnSunrise);
        CheckAndInvokeTimedEvent(0.5f, OnNoon);
        CheckAndInvokeTimedEvent(0.75f, OnSunset);

        // Custom events call
        foreach (var customEvent in customTimeEvents)
        {
            CheckAndInvokeTimedEvent(customEvent.Key, customEvent.Value);
        }
    }

    private void CheckAndInvokeTimedEvent(float triggerTime, UnityEvent timedEventToInvoke)
    {
        if (HasCrossed(triggerTime, previousTime, CurrentTime))
        {
            timedEventToInvoke?.Invoke();
        }
    }

    // checks if we crossed a specific time
    // example: if we moved from 0.24 to 0.26 and sunrise is 0.25 "sunrise() just happpened"
    // but: if we moved from 0.99 back to 0.01, special case for midnight() which is at 0.0.
    private bool HasCrossed(float triggerTime, float previousTime, float currentTime)
    {
        bool regularTimeCheck = previousTime <= currentTime;
        bool lastFrameWeWereBeforeTheTrigger = previousTime < triggerTime;
        bool nowWeAreAfterTheTrigger = currentTime >= triggerTime;

        if (regularTimeCheck)
            return lastFrameWeWereBeforeTheTrigger && nowWeAreAfterTheTrigger;
        else
        { // midnight event (0.99 -> 0.01)
            // midnight could be called twice at low times
            if (Mathf.Approximately(triggerTime, 0f))
                return previousTime > 0.9f && currentTime < 0.1f;

            return lastFrameWeWereBeforeTheTrigger || nowWeAreAfterTheTrigger;
        }
    }
#endregion
}
