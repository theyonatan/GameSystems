using UnityEngine;

public class AnimationManager : MonoBehaviour
{
    public static AnimationManager Instance;
    [SerializeField] Animator playerAnimator;
    
    // Start is called before the first frame update
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Debug.LogError("Animations Manager already exist!");

        playerAnimator ??= GameObject.FindGameObjectWithTag("Character").GetComponent<Animator>();
    }

    public void LoadAnimator(RuntimeAnimatorController animatorController)
    {
        if (playerAnimator == null)
            Debug.LogError("no player animator was found!");

        playerAnimator.runtimeAnimatorController = animatorController;
    }

    public void SetAnimatorValue(string name, float value)
    {
        if (playerAnimator.runtimeAnimatorController is null)
            return;
        
        playerAnimator.SetFloat(name, value);
    }
    
    public void SetAnimatorValue(string name, bool value)
    {
        if (playerAnimator.runtimeAnimatorController is null)
            return;
        
        playerAnimator.SetBool(name, value);
    }
    
    public void SetAnimatorValue(string name)
    {
        if (playerAnimator.runtimeAnimatorController is null)
            return;
        
        playerAnimator.SetTrigger(name);
    }

    public void PlayAnimation(string animationName)
    {
        playerAnimator.SetTrigger(animationName);
    }
}
