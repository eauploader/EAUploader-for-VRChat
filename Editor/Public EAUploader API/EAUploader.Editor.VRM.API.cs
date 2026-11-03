using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using EAUploader.CustomPrefabUtility;
using VRC.Core;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.Editor.Prefab.API
{
#if HAS_VRM
    public static class VRMAPI
    {
        /// <summary>
        /// VRM Comverter for VRChat パッケージが導入されている場合のみ
        /// VRMをインポートします。
        /// </summary>
        /// <param name="path">VRMファイルのパス</param>
        public static void ImportVRM(string path = null)
        {
            VRMImporter.ImportVRM(path);
        }
    }
#endif
}
