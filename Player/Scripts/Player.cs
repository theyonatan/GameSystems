using UnityEngine;

public class Player : MonoBehaviour
{
    private Camera _cam;
    private string _currentState;
    private PlayerStateData _playerStateData;
    private bool _ownsAuthority = true;
    public bool HasAuthority => _ownsAuthority;

    public Camera GetCamera()
    {
        if (!_cam)
            _cam = Camera.main;
        return _cam;
    }

    public void SetAuthority(bool value)
    {
        _ownsAuthority = value;
    }

    private void Start()
    {
        Load("WalkingPlayer");
    }

    public ref PlayerStateData GetData(string stateName)
    {
        switch (stateName)
        {
            case "Walking":
                Load("WalkingPlayer");
                return ref _playerStateData;
            case "WaterTurbo":
                Load("WaterTurboPlayer");
                return ref _playerStateData;
            default:
                Load("WalkingPlayer");
                return ref _playerStateData;
        }
    }

    private void Load(string stateName)
    {
        if (stateName != _currentState)
            _playerStateData = Resources.Load<PlayerStateData>($"playerStates/{stateName}");
        
        _currentState = stateName;
    }
}
