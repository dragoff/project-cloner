using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectCloner
{
    public class ClonesManagerWindow : EditorWindow
    {
        /// <summary>
        /// Returns true if project clone exists.
        /// </summary>
        public bool IsCloneCreated => cloneProjects.Count >= 1;

        private static readonly string[] OPTIONS = new string[] { "Link", "Copy" };

        private int isLinkAssetFolder;
        private int isLinkProjectSettingsFolder;

        private Vector2 clonesScrollPos;
        
        private Project currentProject;
        private List<Project> cloneProjects;
        private string[] tempPersistPathData;

        private enum Tabs
        {
            Manager,
            Settings,
            AddNew,
        }

        private Tabs currentTab;
        private Tabs prevTab;

        [MenuItem("Tools/ProjectCloner Manager", priority = 0)]
        public static void InitWindow()
        {
            ClonesManagerWindow window = (ClonesManagerWindow)EditorWindow.GetWindow(typeof(ClonesManagerWindow));

            window.titleContent = new GUIContent("Clones Manager");
            window.minSize = new Vector2(600, 615);
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            currentProject = Helper.GetCurrentProject();
            cloneProjects = Helper.GetCloneProjects();
            tempPersistPathData = new string[cloneProjects.Count + 1];
            tempPersistPathData[^1] = $"{currentProject.CompanyName}/{currentProject.ProductName}";
            for (int i = 0; i < cloneProjects.Count; i++)
                tempPersistPathData[i] = $"{cloneProjects[i].CompanyName}/{cloneProjects[i].ProductName}";
        }

        private void OnGUI()
        {
            switch (currentTab)
            {
                case Tabs.Settings:
                    DrawSettings();
                    break;
                case Tabs.AddNew:
                    DrawNewClone();
                    break;
                default:
                    DrawManager();
                    break;
            }
        }

        private void DrawManager()
        {
            // If clone project...
            if (Helper.IsClone())
            {
                // If original project is present, display some usage info.
                string message = "This project is the clone.";
                message += currentProject.IsLinkAssetFolder ? "\nAssets folder is linked. Changing assets is NOT allowed!" : "\nAssets folder is copied. Changing assets is allowed!";
                message += currentProject.IsLinkProjectSettingsFolder ? "\nProject folder is linked. Changing persistent path is NOT allowed!" : "\nProject folder is copied. Changing persistent path is allowed!";
                EditorGUILayout.HelpBox(message, MessageType.Info);

                GUILayout.BeginVertical("GroupBox");
                EditorGUILayout.LabelField("Clone", EditorStyles.boldLabel);
                
                DrawProjectPaths(currentProject, tempPersistPathData.Length - 1);

                //Clone project custom argument.
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Arguments", GUILayout.Width(70));
                if (GUILayout.Button("?", GUILayout.Width(20)))
                    ArgumentHelpWindow.InitWindow();
                GUILayout.EndHorizontal();

                string argumentFilePath = Path.Combine(Helper.GetCurrentProjectPath(), ClonesManager.ARGUMENT_FILE_NAME);
                if (File.Exists(argumentFilePath))
                {
                    string argument = File.ReadAllText(argumentFilePath, System.Text.Encoding.UTF8);
                    string argumentTextAreaInput = EditorGUILayout.TextArea(argument,
                        GUILayout.Height(50),
                        GUILayout.MaxWidth(300)
                    );
                    File.WriteAllText(argumentFilePath, argumentTextAreaInput, System.Text.Encoding.UTF8);
                }
                else
                {
                    EditorGUILayout.LabelField("No argument file found.");
                }
                
                GUILayout.EndVertical();
            }
            else // If original project...
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(EditorGUIUtility.IconContent("_Popup"), GUILayout.Width(40), GUILayout.Height(38)))
                    UpdateTab(Tabs.Settings);

                EditorGUI.BeginDisabledGroup(cloneProjects.Count >= ClonesManager.MAX_CLONE_PROJECT_COUNT);
                if (GUILayout.Button("Create new Clone", GUILayout.Height(38)))
                    UpdateTab(Tabs.AddNew);
                EditorGUI.EndDisabledGroup();

                GUILayout.EndHorizontal();

                if (Preferences.SHOW_INFO_OF_ORIGIN.Value)
                {
                    GUILayout.BeginVertical("GroupBox");
                    EditorGUILayout.LabelField("Origin", EditorStyles.boldLabel);
                    DrawProjectPaths(currentProject, tempPersistPathData.Length - 1);
                    GUILayout.EndVertical();
                }

                if (IsCloneCreated)
                {
                    // List all clones
                    clonesScrollPos = EditorGUILayout.BeginScrollView(clonesScrollPos);
                    for (int i = 0; i < cloneProjects.Count; i++)
                    {
                        GUILayout.BeginVertical("GroupBox");
                        string cloneProjectPath = cloneProjects[i].ProjectPath;
                        bool isOpenInAnotherInstance = ClonesManager.IsCloneProjectRunning(cloneProjectPath);

                        if (isOpenInAnotherInstance)
                            EditorGUILayout.LabelField("Clone " + i + " (Running)", EditorStyles.boldLabel);
                        else
                            EditorGUILayout.LabelField("Clone " + i);

                        DrawProjectPaths(cloneProjects[i], i);

                        // Draw arguments
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Arguments", GUILayout.Width(70));
                        if (GUILayout.Button("?", GUILayout.Width(20)))
                            ArgumentHelpWindow.InitWindow();
                        GUILayout.EndHorizontal();

                        string argumentFilePath = Path.Combine(cloneProjectPath, ClonesManager.ARGUMENT_FILE_NAME);
                        //Need to be careful with file reading/writing since it will effect the deletion of
                        //the clone project(The directory won't be fully deleted if there's still file inside being read or write).
                        //The argument file will be deleted first at the beginning of the project deletion process 
                        //to prevent any further being read and write.
                        //Will need to take some extra cautious if want to change the design of how file editing is handled.
                        if (File.Exists(argumentFilePath))
                        {
                            string argument = File.ReadAllText(argumentFilePath, System.Text.Encoding.UTF8);
                            string argumentTextAreaInput = EditorGUILayout.TextArea(argument,
                                GUILayout.Height(50),
                                GUILayout.MaxWidth(300)
                            );
                            File.WriteAllText(argumentFilePath, argumentTextAreaInput, System.Text.Encoding.UTF8);
                        }
                        else
                            EditorGUILayout.LabelField("No argument file found.");

                        EditorGUILayout.Space();

                        // Draw buttons
                        EditorGUI.BeginDisabledGroup(isOpenInAnotherInstance);
                        if (GUILayout.Button("Open in New Editor", GUILayout.Height(40)))
                            ClonesManager.OpenProject(cloneProjectPath);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Delete"))
                        {
                            bool delete = EditorUtility.DisplayDialog(
                                "Delete the clone?",
                                "Are you sure you want to delete the clone project '" + Helper.GetCurrentProject().Name + "_clone'?",
                                "Delete",
                                "Cancel");
                            if (delete)
                            {
                                ClonesManager.DeleteClone(cloneProjectPath);
                                cloneProjects.Remove(cloneProjects[i]);
                            }
                        }

                        GUILayout.EndHorizontal();
                        EditorGUI.EndDisabledGroup();
                        GUILayout.EndVertical();
                    }

                    EditorGUILayout.EndScrollView();
                }
                else
                {
                    // If no clone created yet, we must create it.
                    EditorGUILayout.HelpBox("No project clones found. Create a new one!", MessageType.Info);
                }
            }
        }

        private void DrawNewClone()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(EditorGUIUtility.IconContent("d_back"), GUILayout.Width(40), GUILayout.Height(38)))
            {
                Refresh();
                BackTab();
            }

            if (GUILayout.Button(EditorGUIUtility.IconContent("_Popup"), GUILayout.Width(40), GUILayout.Height(38)))
                UpdateTab(Tabs.Settings);

            GUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Path can be changed in Settings.\nCoping Assets Folder allows to change assets in copy.\nCoping Project Setting allow to set different persistent path", MessageType.Info);

            GUILayout.Space(10);

            GUILayout.BeginVertical("HelpBox");
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Clone path", GUILayout.Width(150));
            EditorGUILayout.SelectableLabel(Helper.GetCloneProjectPath(), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal("Box");
            GUILayout.Label("Assets Folder", GUILayout.Width(150));
            isLinkAssetFolder = GUILayout.SelectionGrid(isLinkAssetFolder, OPTIONS, OPTIONS.Length, "Button");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal("Box");
            GUILayout.Label("Project Settings Folder", GUILayout.Width(150));
            isLinkProjectSettingsFolder = GUILayout.SelectionGrid(isLinkProjectSettingsFolder, OPTIONS, OPTIONS.Length, "Button");
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Add", GUILayout.Height(30)))
            {
                ClonesManager.CreateCloneFromCurrent(Helper.GetCloneProjectPath(), isLinkAssetFolder == 0, isLinkProjectSettingsFolder == 0);
                Refresh();
                BackTab();
            }

            GUILayout.EndVertical();
        }

        private void DrawSettings()
        {
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_back"), GUILayout.Width(40), GUILayout.Height(38)))
            {
                Refresh();
                BackTab();
            }

            GUILayout.BeginVertical("HelpBox");
            GUILayout.Label("Preferences");
            GUILayout.BeginVertical("GroupBox");

            GUILayout.BeginHorizontal();
            Preferences.CLONES_ROOT_PATH.Value = EditorGUILayout.TextField("Clones Path", Preferences.CLONES_ROOT_PATH.Value);
            if (GUILayout.Button("Reset"))
            {
                Preferences.CLONES_ROOT_PATH.ClearValue();
            }
            GUILayout.EndHorizontal();

            Preferences.ASSET_MOD_PREF.Value = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "(recommended) Disable asset saving in clone editors- require re-open clone editors",
                    "Disable asset saving in clone editors so all assets can only be modified from the original project editor"
                ),
                Preferences.ASSET_MOD_PREF.Value);

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                Preferences.ALSO_CHECK_UNITY_LOCK_FILE_STA_PREF.Value = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        "Also check UnityLockFile lock status while checking clone projects running status",
                        "Disable this can slightly increase Clones Manager window performance, but will lead to in-correct clone project running status" +
                        "(the Clones Manager window show the clone project is still running even it's not) if the clone editor crashed"
                    ),
                    Preferences.ALSO_CHECK_UNITY_LOCK_FILE_STA_PREF.Value);
            }
            
            Preferences.SHOW_INFO_OF_ORIGIN.Value = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Show project location and persistent path of original project",
                    "Show project location and persistent path of original project"
                ),
                Preferences.SHOW_INFO_OF_ORIGIN.Value);
            
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draw project path line and persistent path line
        /// </summary>
        /// <param name="project"></param>
        /// <param name="idx"></param>
        private void DrawProjectPaths(Project project, int idx)
        {
            // Draw project path
            GUILayout.BeginHorizontal();
            EditorGUILayout.TextField("Project path", project.ProjectPath, EditorStyles.textField);
            if (GUILayout.Button("View Folder", GUILayout.Width(80)))
                Helper.OpenProjectInFileExplorer(project.ProjectPath);
            GUILayout.EndHorizontal();

            // Draw project persistent path
            GUILayout.BeginHorizontal();

            tempPersistPathData[idx] = EditorGUILayout.TextField("Persistent path", tempPersistPathData[idx], EditorStyles.textField);
            if (!project.IsLinkProjectSettingsFolder)
            {
                if (GUILayout.Button("Set", GUILayout.Width(80)))
                {
                    var split = tempPersistPathData[idx].Split('/');
                    if (split.Length == 2)
                        project.SetProductCompanyNames(split[0], split[1]);
                }
            }
            if (GUILayout.Button("View Folder", GUILayout.Width(80)))
            {
                var persistantPath = Helper.CreatePersistantPath(project);
                if (Directory.Exists(persistantPath))
                    Helper.OpenProjectInFileExplorer(persistantPath);
                else
                    Debug.Log("Persistant path does not exist");
            }
            GUILayout.EndHorizontal();
        }
        
        private void UpdateTab(Tabs newTab) => (prevTab, currentTab) = (currentTab, newTab);
        private void BackTab() => (prevTab, currentTab) = (0, prevTab);
    }
}
