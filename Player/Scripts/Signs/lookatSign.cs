using UnityEngine;

public class LookatSign : MonoBehaviour
{
    [SerializeField] private lookatType lookatType;

    public lookatType GetLookatType()
    {
        return lookatType;
    }
}

public enum lookatType
{
    FirstPerson,
    ThirdPersonFocusOnPlayer,
    ThirdPersonFocusOnAim,
}