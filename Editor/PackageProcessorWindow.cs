using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace DreadScripts.PackageProcessor
{
    public class PackageProcessorWindow : EditorWindow
    {
        private static SerializedObject settings;
        private static SerializedProperty includeDependencies;
        private static SerializedProperty active;

        private static SerializedProperty exportDefaultOffTypes;
        private static SerializedProperty exportDefaultOffFolders;
        private static SerializedProperty exportDefaultOffExtensions;
        private static SerializedProperty exportDefaultOffAssets;

        private static Object targetObject;
        private static Object targetFolder;
        private static Object targetAsset;

        [MenuItem("DreadTools/Scripts Settings/Package Processor")]
        public static void ShowWindow() => GetWindow<PackageProcessorWindow>(false, "Package Processor Settings", true);
        
        private void OnGUI()
        {
            settings.Update();

            using (new BGColorScope(active.boolValue, Color.green, Color.grey))
                active.boolValue = GUILayout.Toggle(active.boolValue, "Active", "toolbarbutton");

            using (new EditorGUI.DisabledScope(!active.boolValue))
            {
                using (new LabelWidthScope(200))
                    EditorGUILayout.PropertyField(includeDependencies, new GUIContent("Default Include Dependencies"));
                DrawListProperty(exportDefaultOffExtensions);
                DrawListProperty(exportDefaultOffFolders, FolderPropertyGUI);
                DrawListProperty(exportDefaultOffTypes, TypePropertyGUI);
                DrawAssetProperty(exportDefaultOffAssets);
            }
            
            PackageProcessorData.folderPath = AssetFolderPath(PackageProcessorData.folderPath, "Settings Path", "ExportPostProcessorDataPath");

            settings.ApplyModifiedProperties();


        }
        
        #region GUI Methods
        private void DrawListProperty(SerializedProperty p, Action guiCall = null)
        {
            using (new GUILayout.VerticalScope("box"))
            {
                p.isExpanded = EditorGUILayout.Foldout(p.isExpanded, p.displayName);
                if (!p.isExpanded) return;
                
                guiCall?.Invoke();

                EditorGUI.indentLevel++;
                if (GUILayout.Button("+"))
                    p.arraySize++;
                
                for (int i = p.arraySize-1; i >= 0; i--)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(p.GetArrayElementAtIndex(i));
                        if (GUILayout.Button("X", GUILayout.Width(18)))
                            p.DeleteArrayElementAtIndex(i);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }
        private void TypePropertyGUI()
        {

            using (new GUILayout.HorizontalScope())
            {
                targetObject = EditorGUILayout.ObjectField("Add Type Of", targetObject, typeof(Object), true);
                EditorGUI.BeginDisabledGroup(!targetObject);
                if (GUILayout.Button("Add Type"))
                {
                    exportDefaultOffTypes.arraySize++;
                    exportDefaultOffTypes.GetArrayElementAtIndex(exportDefaultOffTypes.arraySize - 1).stringValue = targetObject.GetType().AssemblyQualifiedName;
                }
                EditorGUI.EndDisabledGroup();
            }

        }
        private void FolderPropertyGUI()
        {

            using (new GUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                targetFolder = EditorGUILayout.ObjectField("Folder", targetFolder, typeof(Object), true);
                if (EditorGUI.EndChangeCheck() && targetFolder)
                {
                    string assetPath = AssetDatabase.GetAssetPath(targetFolder);
                    if (!AssetDatabase.IsValidFolder(assetPath))
                        targetFolder = AssetDatabase.LoadMainAssetAtPath(System.IO.Path.GetDirectoryName(assetPath));
                }
                EditorGUI.BeginDisabledGroup(!targetFolder);
                if (GUILayout.Button("Add Folder"))
                {
                    exportDefaultOffFolders.arraySize++;
                    exportDefaultOffFolders.GetArrayElementAtIndex(exportDefaultOffFolders.arraySize - 1).stringValue = AssetDatabase.GetAssetPath(targetFolder);
                }
                EditorGUI.EndDisabledGroup();
            }

        }

        private void DrawAssetProperty(SerializedProperty p)
        {
            using (new GUILayout.VerticalScope("box"))
            {
                p.isExpanded = EditorGUILayout.Foldout(p.isExpanded, p.displayName);
                if (p.isExpanded)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        targetAsset = EditorGUILayout.ObjectField("Add GUID Of", targetAsset, typeof(Object), false);
                        if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(targetAsset)))
                            targetAsset = null;
                        EditorGUI.BeginDisabledGroup(!targetAsset);
                        if (GUILayout.Button("Add GUID"))
                        {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(targetAsset, out string newGUID, out long _);
                            exportDefaultOffAssets.arraySize++;
                            exportDefaultOffAssets.GetArrayElementAtIndex(exportDefaultOffAssets.arraySize - 1).stringValue = newGUID;
                        }
                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUI.indentLevel++;
                    if (GUILayout.Button("+"))
                    {
                        p.arraySize++;
                    }
                    for (int i = p.arraySize - 1; i >= 0; i--)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(p.GetArrayElementAtIndex(i));
                            if (GUILayout.Button("X", GUILayout.Width(18)))
                                p.DeleteArrayElementAtIndex(i);
                            if (GUILayout.Button("?",GUILayout.Width(18)))
                            {
                                Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(p.GetArrayElementAtIndex(i).stringValue));
                                if (!asset)
                                {
                                    if (EditorUtility.DisplayDialog("Not Found", "Asset for this GUID was not found. Delete?", "Delete", "Ignore"))
                                        p.DeleteArrayElementAtIndex(i);
                                }
                                else
                                    EditorGUIUtility.PingObject(asset);

                            }
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        private static string AssetFolderPath(string variable, string title, string playerpref, bool isPlayerPrefs = true)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField(title, variable);

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var dummyPath = EditorUtility.OpenFolderPanel(title, AssetDatabase.IsValidFolder(variable) ? variable : "Assets", string.Empty);
                    if (string.IsNullOrEmpty(dummyPath))
                        return variable;
                    string newPath = FileUtil.GetProjectRelativePath(dummyPath);

                    if (!newPath.StartsWith("Assets"))
                    {
                        Debug.LogWarning("New Path must be a folder within Assets!");
                        return variable;
                    }

                    variable = newPath;
                    if (isPlayerPrefs) PlayerPrefs.SetString(playerpref, variable);
                    else EditorPrefs.SetString(playerpref, variable);
                }
            }

            return variable;
        }
        #endregion

        private void OnEnable()
        {
            if (settings == null)
            {
                settings = new SerializedObject(PackageProcessorData.instance);
                includeDependencies = settings.FindProperty("includeDependencies");
                active = settings.FindProperty("active");
                exportDefaultOffTypes = settings.FindProperty("exportDefaultOffTypes");
                exportDefaultOffFolders = settings.FindProperty("exportDefaultOffFolders");
                exportDefaultOffExtensions = settings.FindProperty("exportDefaultOffExtensions");
                exportDefaultOffAssets = settings.FindProperty("exportDefaultOffAssets");
            }

        }
        
        #region Scope Classes
        public class LabelWidthScope : System.IDisposable
        {
            private readonly float originalWidth;
            public LabelWidthScope(float newWidth)
            {
                originalWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = newWidth;
            }

            public void Dispose() => EditorGUIUtility.labelWidth = originalWidth;

        }

        public class BGColorScope : System.IDisposable
        {
            private readonly Color originalColor;

            public BGColorScope(bool active, Color activeColor, Color inactiveColor)
            {
                originalColor = GUI.backgroundColor;
                GUI.backgroundColor = active ? activeColor : inactiveColor;
            }

            public void Dispose() => GUI.backgroundColor = originalColor;

        }
        #endregion
    }



}