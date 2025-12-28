using UnityEngine;

public class DefaultAnimation : MonoBehaviour
{
    [SerializeField] private string animationName;
    
    void Start()
    {
        int animationHash = Animator.StringToHash(animationName);
        GetComponent<Animator>().Play(animationHash);
    }
}
