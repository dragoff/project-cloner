using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectCloner
{
    /// <summary>
    /// Contains all required methods for creating a linked clone of the Unity project.
    /// </summary>
    public static class ClonesManager
    {
        /// <summary>
        /// Name used for an identifying file created in the clone project directory.
        /// </summary>
        /// <remarks>
        /// (!) Do not change this after the clone was created, because then connection will be lost.
        /// </remarks>
        public const string CLONE_FILE_NAME = ".clone";

        /// <summary>
        /// Suffix added to the end of the project clone name when it is created.
        /// </summary>
        /// <remarks>
        /// (!) Do not change this after the clone was created, because then connection will be lost.
        /// </remarks>
        public const string CLONE_NAME_SUFFIX = "_clone";

        /// <summary>
        /// The maximum number of clones
        /// </summary>
        public const int MAX_CLONE_PROJECT_COUNT = 16;

        /// <summary>
        /// Name of the file for storing clone's argument.
        /// </summary>
        public const string ARGUMENT_FILE_NAME = ".clonearg";

        /// <summary>
        /// Default argument of the new clone
        /// </summary>
        public const string DEFAULT_ARGUMENT = "client";

#region Managing clones
        /// <summary>
        /// Creates clone from the project currently open in Unity Editor.
        /// </summary>
        /// <returns></returns>
        public static void CreateCloneFromCurrent(string path, bool isLinkAssetFolder, bool isLinkProjectSettingsFolder)
        {
            if (Helper.IsClone())
            {
                Debug.LogError("This project is already a clone. Cannot clone it.");
                return;
            }

            CreateCloneFromPath(path, isLinkAssetFolder, isLinkProjectSettingsFolder);
        }

        /// <summary>
        /// Creates clone of the project located at the given path.
        /// </summary>
        /// <param name="cloneProjectPath"></param>
        /// <returns></returns>
        private static void CreateCloneFromPath(string cloneProjectPath, bool isLinkAssetFolder, bool isLinkProjectSettingsFolder)
        {
            if (!IsValidPath(cloneProjectPath))
            {
                Debug.LogError("Path is incorrect");
                return;
            }

            if (Directory.Exists(cloneProjectPath))
            {
                Debug.LogError("Directory has already existed");
                return;
            }

            Project sourceProject = new Project(Helper.GetCurrentProjectPath());
            Project cloneProject = new Project(cloneProjectPath, isLinkAssetFolder, isLinkProjectSettingsFolder);

            Debug.Log("Start cloning project, original project: " + sourceProject + ", clone project: " + cloneProject);

            CreateProjectFolder(cloneProject);

            CopyDirectoryWithProgressBar(sourceProject.LibraryPath, cloneProject.LibraryPath, "Cloning Project Library '" + sourceProject.Name + "'. ");
            CopyDirectoryWithProgressBar(sourceProject.PackagesPath, cloneProject.PackagesPath, "Cloning Project Packages '" + sourceProject.Name + "'. ");
            LinkFolders(sourceProject.AutoBuildPath, cloneProject.AutoBuildPath);
            LinkFolders(sourceProject.LocalPackages, cloneProject.LocalPackages);

            if (isLinkAssetFolder)
                LinkFolders(sourceProject.AssetPath, cloneProject.AssetPath);
            else
                CopyDirectoryWithProgressBar(sourceProject.AssetPath, cloneProject.AssetPath, "Cloning Assets '" + sourceProject.Name + "'. ");

            if (isLinkProjectSettingsFolder)
                LinkFolders(sourceProject.ProjectSettingsPath, cloneProject.ProjectSettingsPath);
            else
                CopyDirectoryWithProgressBar(sourceProject.ProjectSettingsPath, cloneProject.ProjectSettingsPath, "Cloning Project Settings '" + sourceProject.Name + "'. ");

            RegisterClone(cloneProject);
        }

        /// <summary>
        /// Registers a clone by placing an identifying ".clone" file in its root directory.
        /// </summary>
        /// <param name="cloneProject"></param>
        private static void RegisterClone(Project cloneProject)
        {
            // Add clone identifier file.
            string identifierFile = Path.Combine(cloneProject.ProjectPath, CLONE_FILE_NAME);
            File.WriteAllText(identifierFile, JsonUtility.ToJson(cloneProject));

            //Add argument file with default argument
            string argumentFilePath = Path.Combine(cloneProject.ProjectPath, ClonesManager.ARGUMENT_FILE_NAME);
            File.WriteAllText(argumentFilePath, DEFAULT_ARGUMENT, System.Text.Encoding.UTF8);

            // Add collabignore.txt to stop the clone from messing with Unity Collaborate if it's enabled.
            string collabIgnoreFile = Path.Combine(cloneProject.ProjectPath, "collabignore.txt");
            File.WriteAllText(collabIgnoreFile, "*");

            // Add .gitignore to stop the clone from messing with git if it's enabled.
            string gitIgnoreFile = Path.Combine(cloneProject.ProjectPath, ".gitignore");
            File.WriteAllText(gitIgnoreFile, "*");
        }

        /// <summary>
        /// Opens a project located at the given path (if one exists).
        /// </summary>
        /// <param name="projectPath"></param>
        public static void OpenProject(string projectPath)
        {
            if (!Directory.Exists(projectPath))
            {
                Debug.LogError("Cannot open the project - provided folder (" + projectPath + ") does not exist.");
                return;
            }

            if (projectPath == Helper.GetCurrentProjectPath())
            {
                Debug.LogError("Cannot open the project - it is already open.");
                return;
            }

            //Validate (and update if needed) the "Packages" folder before opening clone project to ensure the clone project will have the 
            //same "compiling environment" as the original project
            ValidateCopiedFoldersIntegrity.ValidateFolder(projectPath, Helper.GetOriginalProjectPath(), "Packages");

            string fileName = GetApplicationPath();
            string args = "-projectPath \"" + projectPath + "\"";
            Debug.Log("Opening project \"" + fileName + " " + args + "\"");
            Helper.StartHiddenConsoleProcess(fileName, args);
        }

        private static string GetApplicationPath()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor: return EditorApplication.applicationPath;
                case RuntimePlatform.OSXEditor: return EditorApplication.applicationPath + "/Contents/MacOS/Unity";
                case RuntimePlatform.LinuxEditor: return EditorApplication.applicationPath;
                default: throw new System.NotImplementedException("[ProjectCloner] Platform has not supported yet");
            }
        }

        /// <summary>
        /// Is this project being opened by an Unity editor?
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        public static bool IsCloneProjectRunning(string projectPath)
        {
            //Determine whether it is opened in another instance by checking the UnityLockFile
            string unityLockFilePath = new string[] { projectPath, "Temp", "UnityLockfile" }
                .Aggregate(Path.Combine);

            switch (Application.platform)
            {
                case (RuntimePlatform.WindowsEditor):
                    //Windows editor will lock "UnityLockfile" file when project is being opened.
                    //Sometime, for instance: windows editor crash, the "UnityLockfile" will not be deleted even the project
                    //isn't being opened, so a check to the "UnityLockfile" lock status may be necessary.
                    if (Preferences.ALSO_CHECK_UNITY_LOCK_FILE_STA_PREF.Value)
                        return File.Exists(unityLockFilePath) && IsFileLocked(unityLockFilePath);
                    else
                        return File.Exists(unityLockFilePath);
                case (RuntimePlatform.OSXEditor):
                    //Mac editor won't lock "UnityLockfile" file when project is being opened
                    return File.Exists(unityLockFilePath);
                case (RuntimePlatform.LinuxEditor):
                    return File.Exists(unityLockFilePath);
                default:
                    throw new System.NotImplementedException("[ProjectCloner] IsCloneProjectRunning: Unsupported platform: " + Application.platform);
            }
        }

        /// <summary>
        /// Deletes the clone of the currently open project, if such exists.
        /// </summary>
        public static void DeleteClone(string cloneProjectPath)
        {
            // Clone won't be able to delete itself.
            if (Helper.IsClone())
                return;

            //Extra precautions.
            if (cloneProjectPath == string.Empty) return;
            if (cloneProjectPath == Helper.GetOriginalProjectPath()) return;

            //Check what OS is
            string identifierFile;
            string args;
            switch (Application.platform)
            {
                case (RuntimePlatform.WindowsEditor):
                    Debug.Log("Attempting to delete folder \"" + cloneProjectPath + "\"");

                    //The argument file will be deleted first at the beginning of the project deletion process 
                    //to prevent any further reading and writing to it(There's a File.Exist() check at the (file)editor windows.)
                    //If there's any file in the directory being write/read during the deletion process, the directory can't be fully removed.
                    identifierFile = Path.Combine(cloneProjectPath, ClonesManager.ARGUMENT_FILE_NAME);
                    File.Delete(identifierFile);

                    args = "/c " + @"rmdir /s/q " + string.Format("\"{0}\"", cloneProjectPath);
                    Helper.StartHiddenConsoleProcess("cmd.exe", args);

                    break;
                case (RuntimePlatform.OSXEditor):
                    Debug.Log("Attempting to delete folder \"" + cloneProjectPath + "\"");

                    //The argument file will be deleted first at the beginning of the project deletion process 
                    //to prevent any further reading and writing to it(There's a File.Exist() check at the (file)editor windows.)
                    //If there's any file in the directory being write/read during the deletion process, the directory can't be fully removed.
                    identifierFile = Path.Combine(cloneProjectPath, ClonesManager.ARGUMENT_FILE_NAME);
                    File.Delete(identifierFile);

                    FileUtil.DeleteFileOrDirectory(cloneProjectPath);

                    break;
                case (RuntimePlatform.LinuxEditor):
                    Debug.Log("Attempting to delete folder \"" + cloneProjectPath + "\"");
                    identifierFile = Path.Combine(cloneProjectPath, ClonesManager.ARGUMENT_FILE_NAME);
                    File.Delete(identifierFile);

                    FileUtil.DeleteFileOrDirectory(cloneProjectPath);

                    break;
                default:
                    Debug.LogWarning("Not in a known editor. Where are you!?");
                    break;
            }
        }

        private static bool IsFileLocked(string path)
        {
            FileInfo file = new FileInfo(path);
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }
#endregion

#region Creating project folders
        /// <summary>
        /// Creates an empty folder using data in the given Project object
        /// </summary>
        /// <param name="project"></param>
        private static void CreateProjectFolder(Project project)
        {
            string path = project.ProjectPath;
            Debug.Log("Creating new empty folder at: " + path);
            Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Copies the full contents of the unity library. We want to do this to avoid the lengthy re-serialization of the whole project when it opens up the clone.
        /// </summary>
        /// <param name="sourceProject"></param>
        /// <param name="destinationProject"></param>
        [System.Obsolete]
        public static void CopyLibraryFolder(Project sourceProject, Project destinationProject)
        {
            if (Directory.Exists(destinationProject.LibraryPath))
            {
                Debug.LogWarning("Library copy: destination path already exists! ");
                return;
            }

            Debug.Log("Library copy: " + destinationProject.LibraryPath);
            CopyDirectoryWithProgressBar(sourceProject.LibraryPath, destinationProject.LibraryPath,
                "Cloning project '" + sourceProject.Name + "'. ");
        }
#endregion

#region Creating symlinks
        /// <summary>
        /// Creates a symlink between destinationPath and sourcePath (Mac version).
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        private static void CreateLinkMac(string sourcePath, string destinationPath)
        {
            sourcePath = sourcePath.Replace(" ", "\\ ");
            destinationPath = destinationPath.Replace(" ", "\\ ");
            var command = string.Format("ln -s {0} {1}", sourcePath, destinationPath);

            Debug.Log("Mac hard link " + command);

            Helper.ExecuteBashCommand(command);
        }

        /// <summary>
        /// Creates a symlink between destinationPath and sourcePath (Linux version).
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        private static void CreateLinkLinux(string sourcePath, string destinationPath)
        {
            sourcePath = sourcePath.Replace(" ", "\\ ");
            destinationPath = destinationPath.Replace(" ", "\\ ");
            var command = string.Format("ln -s {0} {1}", sourcePath, destinationPath);

            Debug.Log("Linux Symlink " + command);

            Helper.ExecuteBashCommand(command);
        }

        /// <summary>
        /// Creates a symlink between destinationPath and sourcePath (Windows version).
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        private static void CreateLinkWin(string sourcePath, string destinationPath)
        {
            string cmd = "/C mklink /J " + string.Format("\"{0}\" \"{1}\"", destinationPath, sourcePath);
            Debug.Log("Windows junction: " + cmd);
            Helper.StartHiddenConsoleProcess("cmd.exe", cmd);
        }

        /// <summary>
        /// Create a link / junction from the original project to it's clone.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        private static void LinkFolders(string sourcePath, string destinationPath)
        {
            if ((Directory.Exists(destinationPath) == false) && (Directory.Exists(sourcePath) == true))
            {
                switch (Application.platform)
                {
                    case (RuntimePlatform.WindowsEditor):
                        CreateLinkWin(sourcePath, destinationPath);
                        break;
                    case (RuntimePlatform.OSXEditor):
                        CreateLinkMac(sourcePath, destinationPath);
                        break;
                    case (RuntimePlatform.LinuxEditor):
                        CreateLinkLinux(sourcePath, destinationPath);
                        break;
                    default:
                        Debug.LogWarning("Not in a known editor. Application.platform: " + Application.platform);
                        break;
                }
            }
            else
            {
                Debug.LogWarning("Skipping Asset link, it already exists: " + destinationPath);
            }
        }
#endregion
        /// <summary>
        /// Copies directory located at sourcePath to destinationPath. Displays a progress bar.
        /// </summary>
        /// <param name="sourcePath">Directory to be copied.</param>
        /// <param name="destinationPath">Destination directory (created automatically if needed).</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        private static void CopyDirectoryWithProgressBar(string sourcePath, string destinationPath,
            string progressBarPrefix = "")
        {
            var source = new DirectoryInfo(sourcePath);
            var destination = new DirectoryInfo(destinationPath);

            long totalBytes = 0;
            long copiedBytes = 0;

            CopyDirectoryWithProgressBarRecursive(source, destination, ref totalBytes, ref copiedBytes,
                progressBarPrefix);
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Copies directory located at sourcePath to destinationPath. Displays a progress bar.
        /// Same as the previous method, but uses recursion to copy all nested folders as well.
        /// </summary>
        /// <param name="source">Directory to be copied.</param>
        /// <param name="destination">Destination directory (created automatically if needed).</param>
        /// <param name="totalBytes">Total bytes to be copied. Calculated automatically, initialize at 0.</param>
        /// <param name="copiedBytes">To track already copied bytes. Calculated automatically, initialize at 0.</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        private static void CopyDirectoryWithProgressBarRecursive(DirectoryInfo source, DirectoryInfo destination,
            ref long totalBytes, ref long copiedBytes, string progressBarPrefix = "")
        {
            // Directory cannot be copied into itself.
            if (source.FullName.ToLower() == destination.FullName.ToLower())
            {
                Debug.LogError("Cannot copy directory into itself.");
                return;
            }

            // Calculate total bytes, if required.
            if (totalBytes == 0)
            {
                totalBytes = Helper.GetDirectorySize(source, true, progressBarPrefix);
            }

            // Create destination directory, if required.
            if (!Directory.Exists(destination.FullName))
            {
                Directory.CreateDirectory(destination.FullName);
            }

            // Copy all files from the source.
            foreach (FileInfo file in source.GetFiles())
            {
                try
                {
                    file.CopyTo(Path.Combine(destination.ToString(), file.Name), true);
                }
                catch (IOException)
                {
                    // Some files may throw IOException if they are currently open in Unity editor.
                    // Just ignore them in such case.
                }

                // Account the copied file size.
                copiedBytes += file.Length;

                // Display the progress bar.
                float progress = (float)copiedBytes / (float)totalBytes;
                bool cancelCopy = EditorUtility.DisplayCancelableProgressBar(
                    progressBarPrefix + "Copying '" + source.FullName + "' to '" + destination.FullName + "'...",
                    "(" + (progress * 100f).ToString("F2") + "%) Copying file '" + file.Name + "'...",
                    progress);
                if (cancelCopy) return;
            }

            // Copy all nested directories from the source.
            foreach (DirectoryInfo sourceNestedDir in source.GetDirectories())
            {
                DirectoryInfo nextDestinationNestedDir = destination.CreateSubdirectory(sourceNestedDir.Name);
                CopyDirectoryWithProgressBarRecursive(sourceNestedDir, nextDestinationNestedDir,
                    ref totalBytes, ref copiedBytes, progressBarPrefix);
            }
        }

        private static bool IsValidPath(string path)
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    return false;
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
