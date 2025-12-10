using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LootGenerator
{
    private class LootEntry
    {
        public string Name;
        public int ChanceWeight;
        public Action WinResult;
    }

    public class Builder
    {
        // name for the loot generator
        private readonly string builderName;

        // all available loot
        private readonly List<LootEntry> entries = new();

        // the function result of a loot roll
        private readonly Action<String> defaultLootResult = name => Debug.Log($"You won {name}!");

        public Builder(string builderName)
        {
            this.builderName = builderName;
        }

        public Builder WithItem(int chanceWeight, string name, Action result = null)
        {
            entries.Add(new LootEntry
            {
                Name = name,
                ChanceWeight = chanceWeight,
                WinResult = result ?? (() => defaultLootResult(name))
            });

            return this;
        }

        public LootGenerator Build()
        {
            // check if total Loot Weight is 100
            int totalWeight = entries.Sum(e => e.ChanceWeight);
            if (totalWeight != 100)
                throw new InvalidOperationException($"Total Chance Weight for your items must be exactly 100. got {totalWeight} for {builderName}");
            
            return new LootGenerator(entries);
        }
    }

    private readonly List<LootEntry> entries;
    private readonly int totalWeight;

    private LootGenerator(List<LootEntry> entries)
    {
        this.entries = entries;
        totalWeight = entries.Sum(e => e.ChanceWeight);
    }

    public void Roll()
    {
        // get a random roll up to the total weight
        int rolledValue = UnityEngine.Random.Range(0, totalWeight);
        int accumulatedRollWeight = 0;

        // find the item that has that roll
        foreach (var entry in entries)
        {
            accumulatedRollWeight += entry.ChanceWeight;

            if (rolledValue < accumulatedRollWeight)
            {
                entry.WinResult?.Invoke();
                break;
            }
        }
    }
}
