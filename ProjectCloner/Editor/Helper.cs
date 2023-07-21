using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectCloner
{
    public static class Helper
    {
        private static readonly string LOCALLOW_PATH = "LocalLow/";
        private static bool? isCloneFileExistCache = null;


        /// <summary>
        /// Get the path to the current unityEditor project folder's info
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentProjectPath()
        {
            return Application.dataPath.Replace("/Assets", "");
        }

        /// <summary>
        /// Return a project object that describes all the paths we need to clone it.
        /// </summary>
        /// <returns></returns>
        public static Project GetCurrentProject()
        {
            string pathString = GetCurrentProjectPath();
            if (IsClone())
            {
                string cloneFilePath = Path.Combine(pathString, ClonesManager.CLONE_FILE_NAME);
                return JsonUtility.FromJson<Project>(File.ReadAllText(cloneFilePath));
            }
            return new Project(pathString);
        }

        /// <summary>
        /// Get the argument of this clone project.
        /// If this is the original project, will return an empty string.
        /// </summary>
        /// <returns></returns>
        public static string GetArgument()
        {
            string argument = "";
            if (IsClone())
            {
                string argumentFilePath = Path.Combine(GetCurrentProjectPath(), ClonesManager.ARGUMENT_FILE_NAME);
                if (File.Exists(argumentFilePath))
                {
                    argument = File.ReadAllText(argumentFilePath, System.Text.Encoding.UTF8);
                }
            }

            return argument;
        }

        /// <summary>
        /// Returns the path to the original project.
        /// If currently open project is the original, returns its own path.
        /// If the original project folder cannot be found, retuns an empty string.
        /// </summary>
        /// <returns></returns>
        public static string GetOriginalProjectPath()
        {
            if (IsClone())
            {
                // If this is a clone...
                // Original project path can be deduced by removing the suffix from the clone's path.
                string cloneProjectPath = GetCurrentProject().ProjectPath;

                int index = cloneProjectPath.LastIndexOf(ClonesManager.CLONE_NAME_SUFFIX);
                if (index > 0)
                {
                    string originalProjectPath = cloneProjectPath.Substring(0, index);
                    if (Directory.Exists(originalProjectPath)) return originalProjectPath;
                }

                return string.Empty;
            }
            else
            {
                // If this is the original, we return its own path.
                return GetCurrentProjectPath();
            }
        }

        /// <summary>
        /// Returns all clone projects path.
        /// </summary>
        /// <returns></returns>
        public static List<Project> GetCloneProjects()
        {
            List<string> projectsPath = new List<string>(ClonesManager.MAX_CLONE_PROJECT_COUNT);
            for (int i = 0; i < ClonesManager.MAX_CLONE_PROJECT_COUNT; i++)
            {
                string cloneProjectPath = (Preferences.CLONES_ROOT_PATH.Value + "/" + GetCurrentProject().Name + ClonesManager.CLONE_NAME_SUFFIX + "_" + i + "/" + ClonesManager.CLONE_FILE_NAME).Replace("//", "/");
                if (File.Exists(cloneProjectPath))
                    projectsPath.Add(cloneProjectPath);
            }

            List<Project> list = new List<Project>(projectsPath.Count);
            foreach (Project project in projectsPath.Select(File.ReadAllText).Select(JsonUtility.FromJson<Project>))
            {
                project.UpdateProductCompanyNames();
                list.Add(project);
            }
            return list;
        }

        /// <summary>
        /// Returns true if the project currently open in Unity Editor is a clone.
        /// </summary>
        /// <returns></returns>
        public static bool IsClone()
        {
            if (isCloneFileExistCache == null)
            {
                // The project is a clone if its root directory contains an empty file named ".clone".
                string cloneFilePath = Path.Combine(GetCurrentProjectPath(), ClonesManager.CLONE_FILE_NAME);
                isCloneFileExistCache = File.Exists(cloneFilePath);
            }

            return (bool)isCloneFileExistCache;
        }

        /// <summary>
        /// Calculates the size of the given directory. Displays a progress bar.
        /// </summary>
        /// <param name="directory">Directory, which size has to be calculated.</param>
        /// <param name="includeNested">If true, size will include all nested directories.</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        /// <returns>Size of the directory in bytes.</returns>
        public static long GetDirectorySize(DirectoryInfo directory, bool includeNested = false,
            string progressBarPrefix = "")
        {
            EditorUtility.DisplayProgressBar(progressBarPrefix + "Calculating size of directories...",
                "Scanning '" + directory.FullName + "'...", 0f);

            // Calculate size of all files in directory.
            long filesSize = directory.GetFiles().Sum((FileInfo file) => file.Length);

            // Calculate size of all nested directories.
            long directoriesSize = 0;
            if (includeNested)
            {
                IEnumerable<DirectoryInfo> nestedDirectories = directory.GetDirectories();
                foreach (DirectoryInfo nestedDir in nestedDirectories)
                {
                    directoriesSize += GetDirectorySize(nestedDir, true, progressBarPrefix);
                }
            }

            return filesSize + directoriesSize;
        }

        /// <summary>
        /// Starts process in the system console, taking the given fileName and args.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="args"></param>
        public static void StartHiddenConsoleProcess(string fileName, string args)
        {
            Process.Start(fileName, args);
        }
        
        public static void ExecuteBashCommand(string command)
        {
            command = command.Replace("\"", "\"\"");

            Process proc = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"" + command + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            using (proc)
            {
                proc.Start();
                proc.WaitForExit();

                if (!proc.StandardError.EndOfStream)
                {
                    UnityEngine.Debug.LogError(proc.StandardError.ReadToEnd());
                }
            }
        }

        public static void OpenProjectInFileExplorer(string path)
        {
            Process.Start(@path);
        }

        public static string GetCloneProjectPath()
        {
            //Find available clone suffix id
            for (int i = 0; i < ClonesManager.MAX_CLONE_PROJECT_COUNT; i++)
            {
                string possibleCloneProjectPath = Preferences.CLONES_ROOT_PATH.Value + GetCurrentProject().Name + ClonesManager.CLONE_NAME_SUFFIX + "_" + i;

                if (!Directory.Exists(possibleCloneProjectPath))
                    return possibleCloneProjectPath;
            }
            return null;
        }

        public static string CreatePersistantPath(Project project)
        {
            var original = Application.persistentDataPath;
            var index = original.IndexOf(LOCALLOW_PATH) + LOCALLOW_PATH.Length;
            original = original.Remove(index, original.Length - index);
            original = original.Insert(index, $"{project.CompanyName}/{project.ProductName}");
            return original;
        }
    }
}
