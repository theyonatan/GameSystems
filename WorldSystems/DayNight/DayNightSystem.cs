using UnityEngine;

/// <summary>
/// Central System that holds reference to the core DayNightSystem components.
/// it will decide if they are active, or not.
/// </summary>

[DisallowMultipleComponent]
public class DayNightSystem : MonoBehaviour
{
    #region singleton
    // singleton this
    private static DayNightSystem _instance;

    public static DayNightSystem Singleton
    {
        get
        {
            if (_instance == null)
            {
                // find existing instance in scene
                _instance = FindFirstObjectByType<DayNightSystem>();

                // if none exist, something wrong happened.
                if (_instance == null)
                    Debug.LogError("I tried accessing the daynight system when it doesn't exist.");
            }

            return _instance;
        }
    }

    private void Awake()
    {
        // ensure this is destroyed if singleton already exists
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region DayTime_ActiveStatus
    public void Config(ActiveStatus status)
    {
        DayNightSystemStatus = status;
    }

    public class ActiveStatus
    {
        public bool DayClockActive = true;
        public bool LightingControllerActive = true;
        public bool SkyboxControllerActive = true;
        public bool StarfieldControllerActive = true;
        public bool LightProbeControllerActive = true;
        public bool BiomeManagerActive = true;
        public bool DayNightDebuggerActive = true;
    }
    #endregion

    public ActiveStatus DayNightSystemStatus = new ActiveStatus();
    
    private DayClock dayClock;
    private LightingController lightingController;
    private SkyboxController skyboxController;
    private StarfieldController starfieldController;
    private LightProbeController lightProbeController;
    private BiomeManager biomeManager;
    private DayNightDebugger debugger;
}
