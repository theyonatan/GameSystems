using UnityEngine;
using NaughtyAttributes;

public class Player : MonoBehaviour
{
    private Camera _cam;
    private string _currentState;
    [Expandable] [SerializeField] private PlayerStateData playerStateData;

    public Camera GetCamera()
    {
        if (!_cam)
            _cam = Camera.main;
        return _cam;
    }

    public PlayerStateData GetData(string stateName)
    {
        return stateName switch
        {
            "Walking" => Load("WalkingPlayer"),
            "WaterTurbo" => Load("WaterTurboPlayer"),
            _ => Load("WalkingPlayer")
        };
    }

    private PlayerStateData Load(string stateName)
    {
        if (stateName != _currentState)
            playerStateData = Resources.Load<PlayerStateData>($"playerStates/{stateName}");
        
        _currentState = stateName;
        return playerStateData;
    }
}
