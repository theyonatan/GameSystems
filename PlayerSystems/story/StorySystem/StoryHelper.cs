using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

public class StoryHelper
{
    #region StoryCharacters

    /// <summary>
    /// Gather all characters present in the scene, dictionary format
    /// </summary>
    /// <returns>dictionary of available characters in the scene:
    /// (character id, character object)</returns>
    public static Dictionary<Characters, StoryCharacter> GatherCharacters()
    {
        // Gather all characters in the scene
        StoryCharacter[] foundCharacters = Object.FindObjectsByType<StoryCharacter>(FindObjectsSortMode.None);

        // verify characters are built correctly
        if (foundCharacters.Length == 0)
        {
            Debug.LogError("No story characters found!");
            return new ();
        }
        foreach (StoryCharacter character in foundCharacters)
        {
            if (character.CharacterStory is null)
                Debug.LogError($"Character {character.name} has no StoryCharacter assigned!");
        }
        
        // save them to dictionary and setup them
        try
        {
            Dictionary<Characters, StoryCharacter> storyCharacters = foundCharacters
                .ToDictionary(sc => sc.CharacterStory.character);

            SetUpAll(storyCharacters);
        
            return storyCharacters;
        }
        catch (ArgumentException e)
        {
            Debug.LogError("made a new character? you forgot to change it's CharacterType.");
            Debug.LogError(e);
            throw;
        }
    }
    
    /// <summary>
    /// Gather all characters in the scene, string[] format
    /// </summary>
    /// <returns>string array of available characters in the scene</returns>
    public static string[] GatherCharactersIds(bool ignoreSystemCharacter = true)
    {
        // Gather all characters in the scene
        StoryCharacter[] foundCharacters = Object.FindObjectsByType<StoryCharacter>(FindObjectsSortMode.None);
        var resultCharacters = new List<string>();
        
        // verify characters are built correctly
        if (foundCharacters.Length == 0)
        {
            Debug.LogError("No story characters found!");
            return Array.Empty<string>();
        }
        foreach (StoryCharacter character in foundCharacters)
        {
            if (character.CharacterStory is null)
            {
                Debug.LogError($"Character {character.name} has no StoryCharacter assigned!");
                continue;
            }
            
            if (character.CharacterStory.character is Characters.System && ignoreSystemCharacter)
                continue;
            
            resultCharacters.Add(character.CharacterStory.character.ToString());
        }
        
        return resultCharacters.ToArray();
    }
    
    // sets their script variables
    public static void SetUpAll(Dictionary<Characters, StoryCharacter> characters)
    {
        foreach (StoryCharacter character in characters.Values)
            character.SetUp();
    }

    #endregion

    #region StoryObjects

    
    /// <summary>
    /// gathers all story objects present in the scene
    /// </summary>
    /// <returns>Dictionary: (StoryObject Id, StoryObject)</returns>
    public static Dictionary<string, StoryObject> GatherStoryObjects()
    {
        StoryObject[] foundObjects = Object.FindObjectsOfType<StoryObject>();
        
        if (foundObjects.Length == 0)
        {
            Debug.LogError("No story Objects found!");
            return new ();
        }
        
        return foundObjects.ToDictionary(so => so.Id, sc => sc);
    }
    
    /// <summary>
    /// gathers all story objects present in the scene
    /// </summary>
    /// <returns>array of all their names</returns>
    public static string[] GatherStoryObjectsIds(bool ignoreSystemCharacter = true)
    {
        StoryObject[] foundObjects = Object.FindObjectsOfType<StoryObject>();
        
        if (foundObjects.Length == 0)
        {
            Debug.LogError("No story Objects found!");
            return Array.Empty<string>();
        }

        var resultObjects = foundObjects.Select(sc => sc.Id).ToList();

        // remove system object
        if (ignoreSystemCharacter && resultObjects.Contains("SystemObject"))
            resultObjects.Remove("SystemObject");

        return resultObjects.ToArray();
    }
    
    /// <summary>
    /// returns a storyObject object from the scene by id
    /// </summary>
    public static bool FindStoryObjectInScene(string storyObjectId, out StoryObject storyObject)
    {
        var objectsInScene = GatherStoryObjects();
        if (objectsInScene.TryGetValue(storyObjectId, out var gotoObject))
        {
            storyObject = gotoObject;
            return true;
        }
        
        Debug.LogError($"Cannot find StoryObject {storyObjectId} in scene!");
        storyObject = null;
        return false;
    }

    #endregion
}
