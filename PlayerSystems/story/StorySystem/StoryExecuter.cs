using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// a singleton which "runs" the story.
/// we use it to play / pause a running story.
/// </summary>
public class StoryExecuter : MonoBehaviour
{
    public StoryCommand CurrentAction;
    public Queue<StoryCommand> Story = new();
    public string CurrentChapter;
    public Action<string> OnChapterFinished;
    private bool _allowDebug;
    
    public bool IsStoryRunning { get; private set; }

    private static StoryExecuter _instance;

    public static StoryExecuter Instance
    {
        get
        {
            if (!_instance)
            {
                // find an existing one in the scene
                _instance = FindFirstObjectByType<StoryExecuter>();

                if (!_instance)
                {
                    // doesn't exist. let's create a new one.
                    GameObject go = new GameObject("StoryExecuter");
                    _instance = go.AddComponent<StoryExecuter>();
                    DontDestroyOnLoad(go);
                    Debug.Log("New StoryExecuter created.");
                }
            }
            
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogError("a story executer already exists!");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsStoryRunning) return;        // story isn’t playing
        
        // run the current Action; when it reports “done” (returns true) we move on
        if (CurrentAction != null && CurrentAction.Execute())
        {
            StoryLog("Finished Action.");
            
            if (Story.Count > 0)            // there’s another command waiting
            {
                CurrentAction = Story.Dequeue();
                StoryLog("Starting Action.");
            }
            else                            // nothing left → chapter finished
            {
                Debug.Log($"Chapter: '{CurrentChapter}': finished");
            
                string finishedChapter = CurrentChapter;
                IsStoryRunning  = false;
                CurrentChapter  = "";
                CurrentAction   = null;
                
                OnChapterFinished?.Invoke(finishedChapter);
            }
        }
    }

    /// <summary>
    /// adds a new "action" to the story.
    /// can be to speak,
    /// go somewhere, lookat something.
    /// </summary>
    /// <param name="action"></param>
    public void addAction(StoryCommand action)
    {
        Story.Enqueue(action);
    }

    public void LogCurrentChapter()
    {
        if (!string.IsNullOrEmpty(CurrentChapter))
            Debug.Log("No chapter selected");
        else
            Debug.Log(CurrentChapter);
    }
    
    /// <summary>
    /// visual only. will be used to differ between chapters.
    /// this makes sure that no "two" chapters run at the same time.
    /// we tell the story that I am now starting to enqueue commands for chapter X.
    /// </summary>
    /// <param name="chapter"></param>
    public void SetChapter(string chapter)
    {
        if (!string.IsNullOrEmpty(CurrentChapter))
            Debug.LogError(
                $"setting a new chapter: \"{chapter}\", but did not finish current chapter: \"{CurrentChapter}\"!");

        CurrentChapter = chapter;
    }

    /// <summary>
    /// after we enqueued commands for a chapter, in order to "Play" the story,
    /// we run this function.
    /// </summary>
    public void startChapter()
    {
        Debug.Log("Starting new chapter: " + CurrentChapter);

        if (!string.IsNullOrEmpty(CurrentChapter))
        {
            Debug.Log("No chapter selected, Playing empty chapter");
        }
        
        if (Story.Count is 0)
        {
            Debug.Log("Chapter is empty! story will not play anything.");
            return;
        }
        
        CurrentAction = Story.Dequeue();
        IsStoryRunning = true;
    }

    public void ResetChapter()
    {
        CurrentChapter = "";
        CurrentAction = null;
        Story.Clear();
        
        SpeechManager.Instance.ResetSpeech();
    }

    private void StoryLog(string log)
    {
        if (_allowDebug)
            Debug.Log($"(Story) Chapter: '{CurrentChapter}': '{CurrentAction}': {log}");
    }
    
    public void AllowDebug() => _allowDebug = true;
    public void DisableDebug() => _allowDebug = false;
}
