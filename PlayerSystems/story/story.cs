using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class story : MonoBehaviour
{
    private StoryExecuter _storyExecuter;
    public bool StartStoryOnStart = false;

    private void Start()
    {
        _storyExecuter = StoryExecuter.Instance;

        if (StartStoryOnStart)
            startStory();
    }

    public void startStory()
    {
        // setup chapter
        Dictionary<Characters, StoryCharacter> storyCharacters = FindObjectsByType<StoryCharacter>(FindObjectsSortMode.None).ToDictionary(sc => sc.CharacterStory.character);
        StoryCharacter bigFoot = storyCharacters[Characters.Bigfoot];
        _storyExecuter.SetChapter("start");

        // play chapter
        Debug.Log("starting");
        bigFoot.DebugSay("hey");
        bigFoot.GoTo(new Vector3(10, bigFoot.transform.position.y, 1));
        bigFoot.DebugSay("I think it worked!");
        bigFoot.GoTo(new Vector3(1, bigFoot.transform.position.y, 1));
        Debug.Log("ending");

        _storyExecuter.startChapter();
    }
}
