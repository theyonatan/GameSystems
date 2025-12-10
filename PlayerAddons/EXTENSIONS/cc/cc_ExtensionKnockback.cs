using UnityEngine;

// todo: copy from avatar
public class cc_ExtensionKnockback : MonoBehaviour, Knockbackable
{
    [SerializeField] private float knockbackBackwardsForce = 12f;
    [SerializeField] private float knockbackUpForce = 9f;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ApplyKnockback(Vector3 attackingPosition)
    {
        Debug.Log("Took Knockback!");
    }
}
