using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyGiantStudio.DevTools.DevTrails
{
    /// <summary>
    /// Handles adding and removing listeners required for UserStats class
    /// </summary>
    [InitializeOnLoad]
    public class UserStatsTracker
    {
        const string CompileKeyPrefix = "TGS_EditorCompileTimer";
        const string ReloadKey = "TGS_EditorDomainReloadStart";

        static UserStatsTracker()
        {
            //Load previous data first
            UserStats_Global.instance.LoadFromDisk();
            UserStats_Today.instance.LoadFromDisk();

            //DevTrails settings is just a wrapper for a bunch of EditorPref calls. That's why it is being created.
            DevTrailSettings settings = new();

            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            if (settings.TrackConsoleLogs)
            {
                //Application.logMessageReceived += OnLogMessageReceived; //This event only ever triggers on the main thread.
                Application.logMessageReceivedThreaded +=
                    OnLogMessageReceived; //This event will be triggered regardless of whether the message comes in on the main thread or not.
            }

            EditorSceneManager.sceneOpened -= SceneOpened;
            if (settings.TrackSceneOpen)
                EditorSceneManager.sceneOpened += SceneOpened;

            EditorSceneManager.sceneClosed -= SceneClosed;
            if (settings.TrackSceneClose)
                EditorSceneManager.sceneClosed += SceneClosed;

            EditorSceneManager.sceneSaved -= SceneSaved;
            if (settings.TrackSceneSave)
                EditorSceneManager.sceneSaved += SceneSaved;

            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
            //EditorApplicationUtility.onEnterPlayMode += EnterPlayMode; //Doesn't work for some reason

            EditorApplication.wantsToQuit -= WantsToQuit;
            if (settings.TrackTime)
            {
                EditorApplication.wantsToQuit += WantsToQuit;
                //EditorApplication.quitting += Quitting; //Session info resets by the time this is called.
            }

            if (settings.TrackEditorCrashes)
            {
                UserStats_Project projectStat = UserStats_Project.instance;

                //IDCache gets set to zero when it quits properly. So, if it's not zero, the editor probably didn't exit correctly.
                if (projectStat.currentSessionIDCache != EditorAnalyticsSessionInfo.id &&
                    projectStat.currentSessionIDCache != 0)
                {
                    //While using the test editor crash code, it only crashed when the scene was unsaved.
                    //Debug.Log("Editor crashed probably");
                    projectStat.probablyCrashes++;
                    UserStats_Global.instance.ProbableCrashes++;
                    UserStats_Today.instance.ProbableCrashes++;
                }

                projectStat.currentSessionIDCache = EditorAnalyticsSessionInfo.id;
                projectStat.Save();
            }

            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterReload;

            if (settings.TrackCompilation)
            {
                CompilationPipeline.compilationStarted += OnCompilationStarted;
                CompilationPipeline.compilationFinished += OnCompilationFinished;
                AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
                AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
            }

            Undo.undoRedoPerformed -= UndoRedoPerformed;
            if (settings.TrackUndoRedo) Undo.undoRedoPerformed += UndoRedoPerformed;
        }

        static ConcurrentQueue<LogType> _logTypes = new ConcurrentQueue<LogType>();

        static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            _logTypes ??= new();

            if (_logTypes.Count == 0)
                EditorApplication.update += OnEditorUpdate;

            _logTypes.Enqueue(type);
        }

        static void OnEditorUpdate()
        {
            if (_logTypes.Count == 0)
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            int i = 0;
            while (i < 10 && _logTypes.TryDequeue(out var item))
            {
                i++;
                ProcessLog(item);
            }

            if (_logTypes.Count == 0)
                EditorApplication.update -= OnEditorUpdate;
        }

        static void ProcessLog(LogType type)
        {
            if (Application.isPlaying)
                ProcessLog_PlayMode(type);
            else
                ProcessLog_EditorMode(type);
        }

        static void ProcessLog_PlayMode(LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    UserStats_Project.instance._logCounter_playMode++;
                    UserStats_Today.instance._logCounter_playMode++;
                    UserStats_Global.instance._logCounterPlayMode++;
                    break;

                case LogType.Warning:
                    UserStats_Project.instance._warningLogCounter_playMode++;
                    UserStats_Today.instance._warningLogCounter_playMode++;
                    UserStats_Global.instance._warningLogCounterPlayMode++;
                    break;

                case LogType.Exception:
                    UserStats_Project.instance._exceptionLogCounter_playMode++;
                    UserStats_Today.instance._exceptionLogCounter_playMode++;
                    UserStats_Global.instance._exceptionLogCounterPlayMode++;
                    break;
                case LogType.Error:
                case LogType.Assert:
                    UserStats_Project.instance._errorLogCounter_playMode++;
                    UserStats_Today.instance._errorLogCounter_playMode++;
                    UserStats_Global.instance._errorLogCounterPlayMode++;
                    break;
                default:
                    break;
            }
        }

        static void ProcessLog_EditorMode(LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    UserStats_Project.instance.LogCounter_editor++;
                    UserStats_Today.instance.LogCounter_editor++;
                    UserStats_Global.instance.LogCounterEditor++;

                    break;

                case LogType.Warning:

                    UserStats_Project.instance.WarningLogCounter_editor++;
                    UserStats_Today.instance.WarningLogCounter_editor++;
                    UserStats_Global.instance.WarningLogCounterEditor++;

                    break;

                case LogType.Exception:
                    UserStats_Project.instance.ExceptionLogCounter_editor++;
                    UserStats_Today.instance.ExceptionLogCounter_editor++;
                    UserStats_Global.instance.ExceptionLogCounterEditor++;

                    break;

                case LogType.Error:
                    UserStats_Project.instance.ErrorLogCounter_editor++;
                    UserStats_Today.instance.ErrorLogCounter_editor++;
                    UserStats_Global.instance.ErrorLogCounterEditor++;
                    break;
                case LogType.Assert:

                    UserStats_Project.instance.ErrorLogCounter_editor++;
                    UserStats_Today.instance.ErrorLogCounter_editor++;
                    UserStats_Global.instance.ErrorLogCounterEditor++;

                    break;
                default:
                    break;
            }
        }

        static void UndoRedoPerformed()
        {
            UserStats_Project.instance.UndoRedoCounter++;
            UserStats_Today.instance.UndoRedoCounter++;
            UserStats_Global.instance.UndoRedoCounter++;
        }

        static void OnCompilationStarted(object obj)
        {
            string key = GetCompileKey(obj);
            SessionState.SetFloat(key, (float)EditorApplication.timeSinceStartup);
        }

        static void OnCompilationFinished(object context)
        {
            string key = GetCompileKey(context);

            if (!TryGetSessionFloat(key, out float startTime))
                return;

            UserStats_Project.instance.CompileCounter++;
            UserStats_Today.instance.CompileCounter++;
            UserStats_Global.instance.CompileCounter++;

            double compileTime = EditorApplication.timeSinceStartup - startTime;
            SessionState.EraseFloat(key);

            UserStats_Project.instance.TimeSpentCompiling += (float)compileTime;
            UserStats_Today.instance.TimeSpentCompiling += (float)compileTime;
            UserStats_Global.instance.TimeSpentCompiling += (float)compileTime;
        }

        static void OnBeforeReload()
        {
            SessionState.SetFloat(ReloadKey, (float)EditorApplication.timeSinceStartup);
        }


        static void OnAfterReload()
        {
            if (TryGetSessionFloat(ReloadKey, out float startTime))
            {
                double elapsed = EditorApplication.timeSinceStartup - startTime;
                SessionState.EraseFloat(ReloadKey);

                UserStats_Project.instance.TimeSpentInDomainReload += (float)elapsed;
                UserStats_Today.instance.TimeSpentInDomainReload += (float)elapsed;
                UserStats_Global.instance.TimeSpentInDomainReload += (float)elapsed;
            }
        }

        static string GetCompileKey(object context)
        {
            return CompileKeyPrefix + (context?.GetHashCode().ToString() ?? "Default");
        }

        static bool TryGetSessionFloat(string key, out float value)
        {
            value = SessionState.GetFloat(key, float.NaN); // default NaN = “not set”
            return !float.IsNaN(value);
        }
        //private static void FocusChanged(bool obj)
        //{
        //    Debug.Log("Focus changed to " + obj);
        //}

        //private static void Update()
        //{
        //    //Debug.Log("Update");
        //}

        static bool WantsToQuit()
        {
            DevTrailSettings settings = new DevTrailSettings();

            settings.PauseTimeTracking = false;

            UserStats_Project project = UserStats_Project.instance;
            UserStats_Global global = UserStats_Global.instance;
            UserStats_Today today = UserStats_Today.instance;

            int timeSinceStartup = (int)EditorApplication.timeSinceStartup;
            int focusedElapsedTime =
                (int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.focusedElapsedTime).TotalSeconds;
            int activeElapsedTime =
                (int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.activeElapsedTime).TotalSeconds;

            if (project.PauseRecords.sessionID == EditorAnalyticsSessionInfo.id)
            {
                for (int i = 0; i < project.PauseRecords.sessions.Count; i++)
                {
                    timeSinceStartup -= project.PauseRecords.sessions[i].usageTime;
                    focusedElapsedTime -= project.PauseRecords.sessions[i].focusedTime;
                    activeElapsedTime -= project.PauseRecords.sessions[i].activeTime;
                }

                //Unnecessary precaution. But, better safe than sorry.
                timeSinceStartup = Math.Abs(timeSinceStartup);
                focusedElapsedTime = Math.Abs(focusedElapsedTime);
                activeElapsedTime = Math.Abs(activeElapsedTime);
            }

            project.activeUseTime += activeElapsedTime;
            global.activeUseTime += activeElapsedTime;

            project.focusedUseTime += focusedElapsedTime;
            global.focusedUseTime += focusedElapsedTime;

            project.totalUseTime += timeSinceStartup;
            global.totalUseTime += timeSinceStartup;

            global.EditorSessionEnded(EditorAnalyticsSessionInfo.id, timeSinceStartup, focusedElapsedTime,
                activeElapsedTime);

            //project.Save(); //ApplicationExited method saves project stats
            global.SaveToDisk();
            today.SaveToDisk();

            project.currentSessionIDCache = 0;
            if (project.PauseRecords != null)
            {
                project.PauseRecords.sessions?.Clear();
                project.PauseRecords.sessionID = 0;
            }

            project.Save();

            return true;
        }

        static double _timeOfEnteringPlaymode;

        static void PlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    _timeOfEnteringPlaymode = EditorApplication.timeSinceStartup;

                    if (new DevTrailSettings().TrackPlayMode)
                    {
                        UserStats_Project.instance.EnteredPlayMode++;
                        UserStats_Today.instance.EnteredPlayMode++;
                        UserStats_Global.instance.EnteredPlayMode++;
                    }

                    break;
                case PlayModeStateChange.ExitingPlayMode when _timeOfEnteringPlaymode == 0:
                    return;
                case PlayModeStateChange.ExitingPlayMode:
                {
                    if (new DevTrailSettings().TrackPlayMode)
                    {
                        int playModeUseTimeThisSession =
                            (int)(EditorApplication.timeSinceStartup - _timeOfEnteringPlaymode);

                        UserStats_Project.instance.PlayModeUseTime += playModeUseTimeThisSession;
                        UserStats_Today.instance.PlayModeUseTime += playModeUseTimeThisSession;
                        UserStats_Global.instance.PlayModeUseTime += playModeUseTimeThisSession;
                    }
                    else  
                    {
                        //Stats aren't saved automatically to avoid performance impact. So, using the play mode exit to save it.
                        //Changing PlayModeUseTime automatically saves the stats
                        UserStats_Project.instance.Save();
                        UserStats_Today.instance.SaveToDisk();
                        UserStats_Global.instance.SaveToDisk();
                    }
                    
                    _timeOfEnteringPlaymode = 0;
                    break;
                }
                case PlayModeStateChange.EnteredEditMode:
                    break;
                case PlayModeStateChange.ExitingEditMode:
                    break;
                default:
                    break;
            }
        }

        static void SceneSaved(Scene scene)
        {
            UserStats_Project.instance.SceneSaved++;
            UserStats_Today.instance.SceneSaved++;
            UserStats_Global.instance.SceneSaved++;
        }

        static void SceneOpened(Scene scene, OpenSceneMode mode)
        {
            UserStats_Project.instance.SceneOpened++;
            UserStats_Today.instance.SceneOpened++;
            UserStats_Global.instance.SceneOpened++;
        }

        static void SceneClosed(Scene scene)
        {
            UserStats_Project.instance.SceneClosed++;
            UserStats_Today.instance.SceneClosed++;
            UserStats_Global.instance.SceneClosed++;
        }

        [SerializeField] static Queue<SessionInfo> sessionInformations;

        [Serializable]
        class SessionInfo
        {
            public int sessionID;
            public int focusedTime;
            public int activeTime;
        }
    }
}