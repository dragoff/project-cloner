using UnityEditor;

namespace ProjectCloner
{
    /// <summary>
    /// To add value caching for <see cref="EditorPrefs"/> functions
    /// </summary>
    public class BoolPreference
    {
        public string Key { get; private set; }
        public bool DefaultValue { get; private set; }
        public BoolPreference(string key, bool defaultValue)
        {
            Key = key;
            DefaultValue = defaultValue;
        }

        private bool? valueCache = null;

        public bool Value
        {
            get
            {
                if (valueCache == null)
                    valueCache = EditorPrefs.GetBool(Key, DefaultValue);

                return (bool)valueCache;
            }
            set
            {
                if (valueCache == value)
                    return;

                EditorPrefs.SetBool(Key, value);
                valueCache = value;
            }
        }

        public void ClearValue()
        {
            EditorPrefs.DeleteKey(Key);
            valueCache = null;
        }
    }

    public class StringPreference
    {
        public string Key { get; private set; }
        public string DefaultValue { get; private set; }

        public StringPreference(string key, string defaultValue)
        {
            Key = key;
            DefaultValue = defaultValue;
        }

        private string valueCache = null;

        public string Value
        {
            get
            {
                if (string.IsNullOrEmpty(valueCache))
                    valueCache = EditorPrefs.GetString(Key, DefaultValue);

                return valueCache;
            }
            set
            {
                if (string.IsNullOrEmpty(valueCache))
                    return;

                EditorPrefs.SetString(Key, value);
                valueCache = value;
            }
        }

        public void ClearValue()
        {
            EditorPrefs.DeleteKey(Key);
            valueCache = null;
        }
    }

    public static class Preferences
    {
        /// <summary>
        /// Clones Path
        /// </summary>
        public static readonly StringPreference CLONES_ROOT_PATH;

        /// <summary>
        /// Disable asset saving in clone editors?
        /// </summary>
        public static readonly BoolPreference ASSET_MOD_PREF;

        /// <summary>
        /// In addition of checking the existence of UnityLockFile, 
        /// also check is the UnityLockFile being opened.
        /// </summary>
        public static readonly BoolPreference ALSO_CHECK_UNITY_LOCK_FILE_STA_PREF;

        /// <summary>
        /// Shows project location and persistent path of origin project.
        /// </summary>
        public static readonly BoolPreference SHOW_INFO_OF_ORIGIN;

        static Preferences()
        {
            CLONES_ROOT_PATH = new StringPreference("ProjectCloner_ProjectPath", Helper.GetCurrentProject().RootPath + "/");
            ASSET_MOD_PREF = new BoolPreference("ProjectCloner_DisableClonesAssetSaving", true);
            ALSO_CHECK_UNITY_LOCK_FILE_STA_PREF = new BoolPreference("ProjectCloner_CheckUnityLockFileOpenStatus", true);
            SHOW_INFO_OF_ORIGIN = new BoolPreference("ProjectCloner_ShowOriginInfo", false);
        }
    }
}
