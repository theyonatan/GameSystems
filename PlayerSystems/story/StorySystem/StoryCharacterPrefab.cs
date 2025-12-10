using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StoryCharacterPrefab", menuName = "Zero/StoryCharacter")]
public class StoryCharacterPrefab : ScriptableObject
{
    public Characters character;
    public GameObject prefab;
    public bool ShowTailWhenTalking = true;
    private Dictionary<Emotions, Sprite> _faces;

    public Sprite getFace(Emotions emotion)
    {
        if (!_faces.ContainsKey(emotion))
            emotion = Emotions.happy;

        return _faces[emotion];
    }
}
