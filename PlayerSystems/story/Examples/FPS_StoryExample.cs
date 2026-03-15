using UnityEngine;

public class FPS_StoryExample : MonoBehaviour
{
    private void Start()
    {
        StoryExecuter executer = StoryExecuter.Instance;
        executer.SetChapter("FPS Example");
        var system = executer.GetSystem();
        
        system.SwapPlayerState<cc_fpState, FP_CameraState>();
        
        executer.startChapter();
    }
}
