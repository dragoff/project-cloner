using UnityEditor;
using UnityEngine;

namespace ProjectCloner
{
    public class ArgumentHelpWindow  : EditorWindow
    {
        public static void InitWindow()
        {
            ArgumentHelpWindow window = (ArgumentHelpWindow)EditorWindow.GetWindow(typeof(ArgumentHelpWindow));
            window.titleContent = new GUIContent("Arguments Help");
            window.minSize = new Vector2(610, 200);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical("HelpBox");
            EditorGUILayout.HelpBox("Argument are a custom string that can be set up for each individual clone. It can be a simple string like client/server, user name, or a complex json representing the loadout of a character.", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Argument can be set from the clones manager windows.");
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(400));
            EditorGUILayout.LabelField("To get the custom argument, invoke ");
            EditorGUILayout.TextField("ClonesManager.GetArgument()");
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Example:");
            EditorGUILayout.Space();
            GUILayout.TextArea("// Must be editor only\n#if UNITY_EDITOR\nusing UnityEngine;\nusing ProjectCloner;\n\npublic class CustomArgumentExample : MonoBehaviour\n{\n\tvoid Start()\n\t{\n\t\t//Is this unity editor instance opening a clone project?\n\t\tif (ClonesManager.IsClone())\n\t\t{\n\t\t\tDebug.Log(\"This is a clone project.\");\n\t\t\t// Get the custom argument for this clone project.\n\t\t\tstring customArgument = ClonesManager.GetArgument();\n\t\t\t// Do what ever you need with the argument string.\n\t\t\tDebug.Log(\"The custom argument of this clone project is: \" + customArgument);\n\t\t}\n\t\telse\n\t\t{\n\t\t\tDebug.Log(\"This is the original project.\");\n\t\t}\n\t}\n}\n#endif"); 
            GUILayout.EndVertical();
        }
    }
}



