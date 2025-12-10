using UnityEngine;

public class ExampleLootRoll : MonoBehaviour
{
    private LootGenerator magicItemsGenerator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        magicItemsGenerator = new LootGenerator.Builder("Magic Items")
            .WithItem(50, "Magic Cape",    () => Debug.Log("You won the cape!"))
            .WithItem(25, "Magic Coat",    () => Debug.Log("Wait, isn't that the cape?"))
            .WithItem(25, "Never happens", () => Debug.Log("That's wild"))
            .Build();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
            magicItemsGenerator.Roll();
    }
}
