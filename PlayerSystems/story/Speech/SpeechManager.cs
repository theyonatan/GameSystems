using System.Collections;
using UnityEngine;

/// <summary>
/// SpeechManager - Singleton
///
/// get: creates new monobehaviour gameobject (for the UI)
/// SpeechManager.Instance.AnimateText(text);
///
/// OnEnable: textAnimationSpeed = StoryGameSettings.GetSpeed();
/// </summary>
public class SpeechManager : MonoBehaviour
{
    [SerializeField] GameObject canvasObject;
    public bool Finished;
    
    private static SpeechManager _instance;

    public static SpeechManager Instance
    {
        get
        {
            if (!_instance)
            {
                // find an existing one in the scene
                _instance = FindFirstObjectByType<SpeechManager>();

                if (!_instance)
                {
                    // doesn't exist. let's create a new one.
                    GameObject go = new GameObject("SpeechManager");
                    _instance = go.AddComponent<SpeechManager>();
                    DontDestroyOnLoad(go);
                    Debug.Log("New SpeechManager created.");
                }
            }
            
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogError("a Speach Manager already exists!");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }


    void LoadActiveCanvas(out SpeechCanvas activeCanvas, Transform characterTransform)
    {
        activeCanvas = FindFirstObjectByType<SpeechCanvas>();
        if (!activeCanvas)
        {
            Debug.Log("No active canvas found!");
            canvasObject = Resources.Load<GameObject>("ActiveCanvas");
            if (!canvasObject)
                Debug.LogError("SpeechManager: Failed to load canvasObject from Resources.");
            activeCanvas = Instantiate(canvasObject).GetComponent<SpeechCanvas>();
        }
        
        // enable canvas
        activeCanvas.ChatBubble.SetActive(true);
        
        // enable speech arrow pointer
        if (characterTransform != null)
        {
            activeCanvas.BubblePointer.gameObject.SetActive(true);
            activeCanvas.StartPointing(characterTransform);
        }
    }

    /// <summary>
    /// starts a new dialogue in the text box
    /// </summary>
    /// <param name="speechMessage"></param>
    /// <param name="letterDelay"></param>
    /// <param name="characterTransform"></param>
    public void LoadDialogue(string speechMessage, Transform characterTransform, float letterDelay=0.2f)
    {
        Finished = false;
        
        LoadActiveCanvas(out SpeechCanvas activeCanvas, characterTransform);
        
        // StopAllCoroutines();
        StartCoroutine(AnimateText(activeCanvas, speechMessage, letterDelay));
    }


    /// <summary>
    /// animating the text letter by letter
    /// </summary>
    IEnumerator AnimateText(SpeechCanvas activeCanvas, string fullMessage, float letterDelay)
    {
        string curentText = "";
        for (int i = 0; i < fullMessage.Length; i++)
        {
            curentText += fullMessage[i];
            activeCanvas.SetText(curentText);
            yield return new WaitForSeconds(letterDelay);
        }
        
        // disable canvas
        while (true)
        {
            if (Input.GetMouseButtonDown(0))
            {
                activeCanvas.StopPointing();
                activeCanvas.ChatBubble.SetActive(false);
                activeCanvas.BubblePointer.gameObject.SetActive(false);
                Finished = true;
                break;
            }
            yield return null;
        }
    }

    public void ResetSpeech()
    {
        SpeechCanvas activeCanvas = FindFirstObjectByType<SpeechCanvas>();
        
        activeCanvas.StopPointing();
        activeCanvas.ChatBubble.SetActive(false);
        activeCanvas.BubblePointer.gameObject.SetActive(false);
        Finished = true;
    }
}
