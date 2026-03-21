using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.DevTools.DevTrails
{
    [FilePath("Tiny Giant Studio/UserStats_today Build Stats.asset", FilePathAttribute.Location.PreferencesFolder)]
    // ReSharper disable once InconsistentNaming
    public class UserStats_Today_BuildStats : ScriptableSingleton<UserStats_Today_BuildStats>
    {
        public List<BuildRecord> buildRecords;

        #region Methods

        public void Reset(bool saveChange)
        {
            buildRecords?.Clear();

            if (saveChange)
                SaveToDisk();
        }

        // Define your file path (My Documents)
        static string GetCustomSavePath()
        {
#if UNITY_EDITOR_WIN
            string myPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
#elif UNITY_EDITOR_OSX
            string myPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
#elif UNITY_EDITOR_LINUX
            string myPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
#else
            throw new System.NotSupportedException("Unsupported platform");
#endif
            return Path.Combine(myPath, "Tiny Giant Studio/DevTrails", "Today's Build Stats.json");
        }

        // Load from file
        public void LoadFromDisk()
        {
            try
            {
                string path = GetCustomSavePath();
                if (!File.Exists(path)) return;
                
                string json = File.ReadAllText(path);
                if (!json.StartsWith("{")) return;
                if (string.IsNullOrWhiteSpace(json)) return;

                JsonUtility.FromJsonOverwrite(json, this);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "Failed to load today's build stats. Please contact support(FerdowsurAsif@gmail.com) with details, if you have time. Exception: " +
                    ex);
            }
        }

        static readonly object FileLock = new object();

        /// <summary>
        /// Saves the stats to a JSON file
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void SaveToDisk()
        {
            string tempPath = null;
            
            try
            {
                string path = GetCustomSavePath();
                if (string.IsNullOrWhiteSpace(path))
                    throw new Exception("Invalid or empty save path for Today's build Stats. The path is: " + path);

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(this, true);
                if (string.IsNullOrWhiteSpace(json)) return;

                tempPath = path + ".tmp";
                
                // Attempt to write with retry mechanism
                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        lock (FileLock)
                        {
                            File.WriteAllText(tempPath, json);
                            if (File.Exists(path))
                                File.Replace(tempPath, path, null);
                            else
                                File.Move(tempPath, path);
                        }

                        // Success � break out of retry loop
                        break;
                    }
                    //catch (IOException e) when (attempt < maxAttempts)
                    catch (IOException) when (attempt < maxAttempts)
                    {
                        // If file is in use, wait and retry
                        System.Threading.Thread.Sleep(100); // Wait 100ms before retrying
                    }
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogError($"Couldn't save stats. Access denied: {e.Message}");
            }
            catch (IOException e)
            {
                Debug.LogError($"Couldn't save stats. IO error while saving: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't save stats. Unexpected error: {e.Message}");
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        void Save()
        {
            //Save(true);
            //SaveToDisk();
        }

        #endregion Methods
    }
}