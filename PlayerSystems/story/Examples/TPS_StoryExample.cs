using UnityEngine;

public class TPS_StoryExample : MonoBehaviour
{
    void Start()
    {
        StoryExecuter executer = StoryExecuter.Instance;
        executer.SetChapter("TPS Example");
        var system = executer.GetSystem();
        
        system.SwapPlayerState<cc_tpState, TP_CameraState>();
        
        executer.startChapter();
    }
}
