using UnityEngine;

public class PlayerStory : MonoBehaviour
{
    private void Start()
    {
        StoryExecuter executer = StoryExecuter.Instance;
        var characters = StoryHelper.GatherCharactersByEnum();
        
        executer.SetChapter("testing player story");
        
        var system = characters[Characters.System];
        system.SwapPlayerState<cc_tpState, TP_CameraState>();
        
        executer.startChapter();
    }
}
