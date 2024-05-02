using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace DreadScripts.PackageProcessor
{
    [InitializeOnLoad]
    public class ImportPostProcessor
    {

        private static readonly Harmony harmony;
        private static readonly FieldInfo assetPathField;
        private static readonly FieldInfo enabledStatusField;
        private static readonly FieldInfo isFolderField;

        static ImportPostProcessor()
        {
            harmony = new Harmony("com.dreadscripts.importpostprocessor.tool");
            Type packageImportType = GetType("UnityEditor.PackageImport");
            Type packageImportItemType = GetType("UnityEditor.ImportPackageItem");
            assetPathField = packageImportItemType.GetField("destinationAssetPath", BindingFlags.Public | BindingFlags.Instance);
            enabledStatusField = packageImportItemType.GetField("enabledStatus", BindingFlags.Public | BindingFlags.Instance);
            isFolderField = packageImportItemType.GetField("isFolder", BindingFlags.Public | BindingFlags.Instance);
            QuickPatch(packageImportType, "ShowImportPackage", string.Empty, nameof(ShowImportPackagePost));
        }

        private static void ShowImportPackagePost(object[] items)
        {
            foreach (var i in items)
            {
                string path = (string)assetPathField.GetValue(i) ;
                bool isFolder = (bool)isFolderField.GetValue(i);
                if (!isFolder && !string.IsNullOrEmpty(path) && (path.EndsWith(".cs") || path.EndsWith(".dll")))
                    enabledStatusField.SetValue(i, 0);
                
            }
        }

        private static void QuickPatch(System.Type targetType, string ogMethod, string preMethod = "", string poMethod = "")
        {
            MethodInfo originalMethod = AccessTools.GetDeclaredMethods(targetType).Find(m => m.Name == ogMethod);
            HarmonyMethod prefixMethod = string.IsNullOrEmpty(preMethod) ? null : new HarmonyMethod(typeof(ImportPostProcessor).GetMethod(preMethod, BindingFlags.NonPublic | BindingFlags.Static));
            HarmonyMethod postMethod = string.IsNullOrEmpty(poMethod) ? null : new HarmonyMethod(typeof(ImportPostProcessor).GetMethod(poMethod, BindingFlags.NonPublic | BindingFlags.Static));
            harmony.Patch(originalMethod, prefixMethod, postMethod);
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
    }
}
