using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using HarmonyLib;
using System.Reflection;
using System.IO;
using System.Linq;
using Object = UnityEngine.Object;

//Made by Dreadrith#3238
//Server: https://discord.gg/ZsPfrGn
//Github: https://github.com/Dreadrith/DreadScripts
//Gumroad: https://gumroad.com/dreadrith

namespace DreadScripts.PackageProcessor
{
    [InitializeOnLoad]
    public class ExportPostProcessor
    {
        #region Patching
        private static readonly System.Type packageExportType;
        private static readonly System.Type packageTreeGUIType;
        private static readonly FieldInfo includeDependenciesField;
        private static readonly FieldInfo exportItemsField;
        private static readonly MethodInfo refreshListMethod;
        private static readonly MethodInfo getTreeItemMethod;
        #endregion

        #region Constants

        private const string PARAM_FOLDERS = "exportDefaultOffFolders";
        private const string PARAM_EXTENSIONS = "exportDefaultOffExtensions";
        private const string PARAM_TYPES = "exportDefaultOffTypes";
        private const string PARAM_ASSETS = "exportDefaultOffAssets";
        #endregion

        private static PackageProcessorData settings;
        private static EditorWindow exporterInstance;

        private static object rawItems;
        private static ReflectedExportPackageItem[] reflectedItems;
        private static bool firstStart = true;
        
        static ExportPostProcessor()
        {
            packageExportType = GetType("UnityEditor.PackageExport");
            packageTreeGUIType = GetType("UnityEditor.PackageExportTreeView+PackageExportTreeViewGUI");

            includeDependenciesField = packageExportType.GetField("m_IncludeDependencies", BindingFlags.Instance | BindingFlags.NonPublic);
            exportItemsField = packageExportType.GetField("m_ExportPackageItems", BindingFlags.Instance | BindingFlags.NonPublic);
            

            refreshListMethod = GetType("UnityEditor.PackageExportTreeView").GetMethod("ComputeEnabledStateForFolders", BindingFlags.Instance | BindingFlags.NonPublic);
            getTreeItemMethod = GetType("UnityEditor.PackageExportTreeView+PackageExportTreeViewItem").GetMethod("get_item", BindingFlags.Instance | BindingFlags.Public);

            var harmony = new Harmony("com.dreadscripts.exportpostprocessor.tool");

            void QuickPatch(System.Type targetType, string ogMethod, string preMethod = "", string poMethod = "")
            {
                MethodInfo originalMethod = AccessTools.GetDeclaredMethods(targetType).Find(m => m.Name == ogMethod);
                HarmonyMethod prefixMethod = string.IsNullOrEmpty(preMethod) ? null : new HarmonyMethod(typeof(ExportPostProcessor).GetMethod(preMethod, BindingFlags.NonPublic | BindingFlags.Static));
                HarmonyMethod postMethod = string.IsNullOrEmpty(poMethod) ? null : new HarmonyMethod(typeof(ExportPostProcessor).GetMethod(poMethod, BindingFlags.NonPublic | BindingFlags.Static));
                harmony.Patch(originalMethod, prefixMethod, postMethod);
            }

            QuickPatch(packageExportType,  "ShowExportPackage", null, nameof(ShowExportPost));
            QuickPatch(packageExportType,  "BuildAssetList", null, nameof(BuildAssetListPost));
            QuickPatch(packageExportType,  "OnGUI", null, nameof(packageGUIPost));
            QuickPatch(packageTreeGUIType,  "OnRowGUI", nameof(treeRowGUIPrefix));
        }

        static void ShowExportPost()
        {
            settings = PackageProcessorData.instance;
            exporterInstance = EditorWindow.GetWindow(packageExportType);
            firstStart = true;
            if (settings.active)
                includeDependenciesField.SetValue(exporterInstance, settings.includeDependencies);
            
        }

        static void BuildAssetListPost()
        {
            GetExportItems();
            if (!firstStart || !settings.active) return;
            firstStart = false;
            
            List<System.Type> offTypes = new List<System.Type>();
            settings.exportDefaultOffTypes.ForEach(t =>
            {
                if (string.IsNullOrWhiteSpace(t)) return;
                System.Type targetType = GetType(t);
                if (targetType != null)
                    offTypes.Add(targetType);
            });
            foreach (var i in reflectedItems)
            {
                if (offTypes.Any(t => t == i.type) ||
                    settings.exportDefaultOffFolders.Any(f => i.assetPath.Contains(f)) ||
                    settings.exportDefaultOffExtensions.Any(s => s == Path.GetExtension(i.assetPath)) ||
                    (!i.isFolder && settings.exportDefaultOffAssets.Any(g => g == i.guid)))
                        
                    i.enabledState = 0;
            }
        }

        static void packageGUIPost()
        {
            using (new GUILayout.HorizontalScope())
            {

                if (GUILayout.Button("Modified by Dreadrith#3238", "minilabel"))
                    Application.OpenURL("https://github.com/Dreadrith/DreadScripts");
                GUILayout.FlexibleSpace();
            }
        }

        static ReflectedExportPackageItem reflectedTargetItem;
        static void treeRowGUIPrefix(Rect rowRect,UnityEditor.IMGUI.Controls.TreeViewItem tvItem)
        {
            if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
            {
                object targetItem = getTreeItemMethod.Invoke(tvItem, null);
                if (targetItem != null)
                {
                    reflectedTargetItem = new ReflectedExportPackageItem(targetItem);
                    if (!reflectedTargetItem.isFolder)
                    {
                        GenericMenu myMenu = new GenericMenu();
                        myMenu.AddItem(new GUIContent("Toggle Type"), false, new GenericMenu.MenuFunction(ToggleSelectedType));
                        myMenu.AddSeparator("");

                        if (!HasSelectedAsset())
                            myMenu.AddItem(new GUIContent("Exclusions/Add Asset"), false, new GenericMenu.MenuFunction(ExcludeSelectedAsset));
                        else
                            myMenu.AddItem(new GUIContent("Exclusions/Remove Asset"), false, new GenericMenu.MenuFunction(IncludeSelectedAsset));

                        if (!HasSelectedType())
                            myMenu.AddItem(new GUIContent("Exclusions/Add Type"), false, new GenericMenu.MenuFunction(ExcludeSelectedType));
                        else
                            myMenu.AddItem(new GUIContent("Exclusions/Remove Type"), false, new GenericMenu.MenuFunction(IncludeSelectedType));

                        if (!HasSelectedExtension())
                            myMenu.AddItem(new GUIContent("Exclusions/Add Extension"), false, new GenericMenu.MenuFunction(ExcludeSelectedExtension));
                        else
                            myMenu.AddItem(new GUIContent("Exclusions/Remove Extension"), false, new GenericMenu.MenuFunction(IncludeSelectedExtension));

                        myMenu.ShowAsContext();
                        Event.current.Use();
                    }
                    else
                    {
                        GenericMenu myMenu = new GenericMenu();
                        if (!HasSelectedFolder())
                            myMenu.AddItem(new GUIContent("Exclusions/Add Folder"), false, new GenericMenu.MenuFunction(ExcludeSelectedFolder));
                        else
                            myMenu.AddItem(new GUIContent("Exclusions/Remove Folder"), false, new GenericMenu.MenuFunction(IncludeSelectedFolder));
                        myMenu.ShowAsContext();
                        Event.current.Use();
                    }
                }
                else
                {
                    GenericMenu myMenu = new GenericMenu();
                    myMenu.AddDisabledItem(new GUIContent("Folder not being exported"));
                    myMenu.ShowAsContext();
                    Event.current.Use();
                }
            }
        }

        static void ToggleSelectedType()
        {
            List<ReflectedExportPackageItem> targetItems = new List<ReflectedExportPackageItem>();
            bool allOn = true;
            Iterate(reflectedItems, i =>
            {
                if (i.type == reflectedTargetItem.type)
                {
                    targetItems.Add(i);
                    if (i.enabledState == 0)
                    {
                        allOn = false;
                    }
                }
            });
            targetItems.ForEach(i => i.enabledState = allOn ? 0 : 1);
            RefreshTreeView();
        }
        
        static bool HasExclusion(string property, string value)
        {
            SerializedObject data = new SerializedObject(PackageProcessorData.instance);
            SerializedProperty targetProperty = data.FindProperty(property);
            for (int i = 0; i < targetProperty.arraySize; i++)
            {
                if (targetProperty.GetArrayElementAtIndex(i).stringValue == value)
                {
                    return true;
                }
            }
            return false;
        }

        static void AddExclusion(string property, string value)
        {
            SerializedObject data = new SerializedObject(PackageProcessorData.instance);
            SerializedProperty targetProperty = data.FindProperty(property);
            targetProperty.arraySize++;
            targetProperty.GetArrayElementAtIndex(targetProperty.arraySize - 1).stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RemoveExclusion(string property, string value)
        {
            SerializedObject data = new SerializedObject(PackageProcessorData.instance);
            SerializedProperty targetProperty = data.FindProperty(property);
            for (int i = 0; i < targetProperty.arraySize; i++)
            {
                if (targetProperty.GetArrayElementAtIndex(i).stringValue == value)
                {
                    targetProperty.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        #region Extension Exclusion
        static bool HasSelectedExtension() => HasExclusion(PARAM_EXTENSIONS, Ext(reflectedTargetItem.assetPath));
        static void ExcludeSelectedExtension() => AddExclusion(PARAM_EXTENSIONS, Ext(reflectedTargetItem.assetPath));
        static void IncludeSelectedExtension() => RemoveExclusion(PARAM_EXTENSIONS, Ext(reflectedTargetItem.assetPath));
        #endregion
        
        #region Folder Exclusion
        static bool HasSelectedFolder() => HasExclusion(PARAM_FOLDERS, reflectedTargetItem.assetPath);
        static void ExcludeSelectedFolder() => AddExclusion(PARAM_FOLDERS, reflectedTargetItem.assetPath);
        static void IncludeSelectedFolder() => RemoveExclusion(PARAM_FOLDERS, reflectedTargetItem.assetPath);
        #endregion

        #region Type Exclusion
        static bool HasSelectedType() => PackageProcessorData.instance.exportDefaultOffTypes.Any(t => GetType(t) == reflectedTargetItem.type);
        static void ExcludeSelectedType() => AddExclusion(PARAM_TYPES, reflectedTargetItem.type.AssemblyQualifiedName);
        static void IncludeSelectedType()
        {
            SerializedObject data = new SerializedObject(PackageProcessorData.instance);
            SerializedProperty targetProperty = data.FindProperty(PARAM_TYPES);
            for (int i = 0; i < targetProperty.arraySize; i++)
            {
                Type currentType = GetType(targetProperty.GetArrayElementAtIndex(i).stringValue);
                if (currentType == reflectedTargetItem.type)
                {
                    targetProperty.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
        
        #region Asset Exclusion
        static bool HasSelectedAsset() => HasExclusion(PARAM_ASSETS, reflectedTargetItem.guid);
        static void ExcludeSelectedAsset() => AddExclusion(PARAM_ASSETS, reflectedTargetItem.guid);
        static void IncludeSelectedAsset() => RemoveExclusion(PARAM_ASSETS, reflectedTargetItem.guid);
        #endregion

        private static string Ext(string path) => Path.GetExtension(path);
        
        private static void GetExportItems()
        {
            rawItems = exportItemsField.GetValue(exporterInstance);
            object[] exportItems = (object[])rawItems;
            exportItemsField.SetValue(exporterInstance, exportItems);

            reflectedItems = new ReflectedExportPackageItem[exportItems.Length];
            for (int i = 0; i < exportItems.Length; i++)
                reflectedItems[i] = new ReflectedExportPackageItem(exportItems[i]);
            
        }

        private static void RefreshTreeView()
        {
            refreshListMethod.Invoke(packageExportType.GetField("m_Tree", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(exporterInstance), null);
        }

        private class ReflectedExportPackageItem
        {
            private readonly object instance;

            public bool isFolder => AssetDatabase.IsValidFolder(assetPath);

            private string _assetPath;
            public string assetPath
            {
                get
                {
                    if (string.IsNullOrEmpty(_assetPath))
                        _assetPath = (string)assetPathField.GetValue(instance);
                    return _assetPath;
                }
            }

            private bool stateInit;
            private int _enabledState;
            public int enabledState
            {
                get
                {
                    if (!stateInit)
                        return 1;
                    if (_enabledState == -2)
                        _enabledState = (int)enabledStateField.GetValue(instance);
                    return _enabledState;
                }
                set
                {
                    stateInit = true;
                    _enabledState = value;
                    enabledStateField.SetValue(instance, value);
                }
            }

            private string _guid;
            public string guid
            {
                get
                {
                    if (string.IsNullOrEmpty(_guid))
                        _guid = (string)guidField.GetValue(instance);
                    return _guid;
                }
            }

            private System.Type _type;
            public System.Type type
            {
                get
                {
                    if (asset && _type == null)
                            _type = asset.GetType();
                    return _type;
                }
            }

            private Object _asset;
            private bool _assetLoaded;

            private Object asset
            {
                get
                {
                    if (_asset || _assetLoaded) return _asset;

                    _asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    _assetLoaded = true;
                    return _asset;
                }
            }


            static FieldInfo assetPathField;
            static FieldInfo enabledStateField;
            static FieldInfo guidField;
            public ReflectedExportPackageItem(object instance)
            {
                this.instance = instance;
            }

            [InitializeOnLoadMethod]
            static void InitializeFields()
            {
                System.Type targetType = AccessTools.TypeByName("ExportPackageItem");
                assetPathField = targetType.GetField("assetPath", BindingFlags.Public | BindingFlags.Instance);
                enabledStateField = targetType.GetField("enabledStatus", BindingFlags.Public | BindingFlags.Instance);
                guidField = targetType.GetField("guid", BindingFlags.Public | BindingFlags.Instance);
            }

        }

        private static System.Type GetType(string typeName)
        {
            System.Type myType = System.Type.GetType(typeName);
            if (myType != null)
                return myType;
            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                myType = assembly.GetType(typeName);
                if (myType != null)
                    return myType;
            }
            return null;
        }

        public static void Iterate<T>(IEnumerable<T> collection, System.Action<T> action)
        {
            using (IEnumerator<T> myNum = collection.GetEnumerator())
            {
                while (myNum.MoveNext())
                {
                    action(myNum.Current);
                }
            }
        }

    }

}