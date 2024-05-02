using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DreadScripts.PackageProcessor
{
    [System.Serializable]
    public class PackageProcessorData : ScriptableObject
    {
        private static PackageProcessorData _instance;

        public static PackageProcessorData instance
        {
            get
            {
                if (!_instance && Exists())
                    if (_instance = AssetDatabase.LoadAssetAtPath<PackageProcessorData>(SavePath)) 
                        return _instance;

                _instance = CreateInstance<PackageProcessorData>();
                if (!System.IO.Directory.Exists(folderPath))
                    System.IO.Directory.CreateDirectory(folderPath);

                AssetDatabase.CreateAsset(_instance, SavePath);
                return _instance;
            }
        }

        private static string _folderPath;

        public static string folderPath
        {
            get
            {
                if (string.IsNullOrEmpty(_folderPath))
                    _folderPath = PlayerPrefs.GetString("PackageProcessorDataPath", "Assets/DreadScripts/Saved Data/PackageProcessor");
                return _folderPath;
            }
            set => _folderPath = value;
        }

        private static string SavePath => folderPath + "/PackageProcessorData.asset";

        public bool active = true;
        public bool includeDependencies = false;
        public List<string> exportDefaultOffExtensions = new List<string>();
        public List<string> exportDefaultOffFolders = new List<string>();
        public List<string> exportDefaultOffTypes = new List<string>();
        public List<string> exportDefaultOffAssets = new List<string>();

        public static bool Exists() => System.IO.File.Exists(SavePath);
        
    }
}
