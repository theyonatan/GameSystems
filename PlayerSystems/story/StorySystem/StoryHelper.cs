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
    [Obsolete("Use GatherCharacters() of Dictionary<String, StoryCharacter> instead")]
    public static Dictionary<Characters, StoryCharacter> GatherCharactersByEnum()
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
            Debug.LogError("Likely used old story gathering system of enums! use GatherCharacters instead.");
            Debug.LogError("OR made a new character? you forgot to change it's CharacterType.");
            Debug.LogError(e);
            return new Dictionary<Characters, StoryCharacter>();
        }
    }

    /// <summary>
    /// Gather all characters present in the scene
    /// </summary>
    public static StoryCharacter[] GatherAllCharacters()
    {
        // Gather all characters in the scene
        StoryCharacter[] foundCharacters =
            Object.FindObjectsByType<StoryCharacter>(FindObjectsSortMode.None);

        // verify characters are built correctly
        if (foundCharacters.Length == 0)
        {
            Debug.LogError("No story characters found!");
            return Array.Empty<StoryCharacter>();
        }

        return foundCharacters;
    }

    /// <summary>
    /// Gather all characters present in the scene for a specific cutscene
    /// </summary>
    /// <param name="cutsceneId">Cutscene identifier</param>
    public static Dictionary<string, StoryCharacter> GatherCharacters(string cutsceneId)
    {
        // Gather all characters in the scene
        StoryCharacter[] foundCharacters = GatherAllCharacters();

        if (foundCharacters.Length == 0)
            return new Dictionary<string, StoryCharacter>();

        // Filter by cutscene
        var filteredCharacters = foundCharacters
            .Where(sc => sc.CutsceneId == cutsceneId);

        var filteredCharactersList = filteredCharacters.ToList();
        if (!filteredCharactersList.Any())
        {
            Debug.LogWarning($"No characters found for cutscene '{cutsceneId}'.");
            return new Dictionary<string, StoryCharacter>();
        }

        // Validate
        foreach (StoryCharacter character in filteredCharactersList)
        {
            if (character.CharacterStory == null)
                Debug.LogError($"Character {character.name} has no CharacterStory assigned!");
            else if (string.IsNullOrWhiteSpace(character.CharacterStory.CharacterName))
                Debug.LogError($"Character {character.name} has an empty CharacterName!");
        }

        // save them to dictionary and setup them
        try
        {
            Dictionary<string, StoryCharacter> storyCharacters =
                filteredCharactersList.ToDictionary(
                    sc => sc.CharacterStory.CharacterName
                );

            SetUpAll(storyCharacters);
            return storyCharacters;
        }
        catch (ArgumentException e)
        {
            Debug.LogError(
                $"Duplicate CharacterName detected in cutscene '{cutsceneId}'."
            );
            Debug.LogError(e);
            throw;
        }
    }

    public static StoryCharacter GatherSpecific(string requestedName)
    {
        var allCharacters = GatherAllCharacters();
        foreach (var character in allCharacters)
        {
            if (character.CharacterStory.CharacterName == requestedName)
                return character;
        }
        
        throw new ArgumentException($"Character with name {requestedName} not found in scene!");
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
    private static void SetUpAll(Dictionary<Characters, StoryCharacter> characters)
    {
        foreach (StoryCharacter character in characters.Values)
            character.SetUp();
    }

    private static void SetUpAll(Dictionary<string, StoryCharacter> characters)
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
