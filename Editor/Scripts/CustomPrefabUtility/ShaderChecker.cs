using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EAUploader.CustomPrefabUtility
{
    public class ShaderChecker
    {
        public static readonly List<string> UNSUPPORTED_SHADERS = new List<string>
        {
            "_lil",
            "outlineoffset",
            "CloverUI",
            "Video",
            "VRM10",
            "FX",
        };

        public static void CheckShaders()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CheckShadersInPrefabs(path);
            }
        }

        public static List<string> GetExistingShaders()
        {
            List<string> shaderGroups = new List<string>();
            HashSet<string> uniqueGroups = new HashSet<string>(); // Create a HashSet to store unique shader groups

            foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
            {
                string shaderPath = AssetDatabase.GUIDToAssetPath(guid);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                string shaderGroupName = shader.name.Split('/')[0].Trim();

                if (!UNSUPPORTED_SHADERS.Contains(shaderGroupName) && uniqueGroups.Add(shaderGroupName))
                {
                    shaderGroups.Add(shaderGroupName);
                }
            }

            return shaderGroups;
        }

        private static bool ContainsMissingOrProblematicShaderMaterial(Renderer[] renderers, out string errorInfo)
        {
            errorInfo = string.Empty;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null)
                    {
                        string shaderName = material.shader.name;
                        if (shaderName == "Hidden/InternalErrorShader" || !ShaderExists(shaderName))
                        {
                            errorInfo += $"Game Object: {renderer.gameObject.name}, Material: {material.name}, Shader: {shaderName}\n";
                        }
                    }
                }
            }
            return !string.IsNullOrEmpty(errorInfo);
        }

        private static void DisplayShaderIssueMessageBox(string prefabName)
        {
            string msg1 = T7e.Get("Prefabs with missing or problematic shaders:");
            string msg2 = T7e.Get("Please confirm the required shaders from the avatar distributor.");
            string msg3 = T7e.Get("Why am I seeing this?");
            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.Append(msg1);
            messageBuilder.Append("\n");
            messageBuilder.Append(prefabName);
            messageBuilder.Append("\n\n");
            messageBuilder.Append(msg2);

            string message = messageBuilder.ToString();
            if (EditorUtility.DisplayDialogComplex(T7e.Get("Shader Issues Found"), message, msg3, "OK", "") == 0)
            {
                Application.OpenURL("https://eauploader-docs.uslog.tech/faq/missing_shader");
            }
        }

        public static void CheckShadersInPrefabs(string path = null)
        {
            if (path == null) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);

            DisplayShaderIssueMessageBox(prefab.name);
        }

        public static bool CheckAvatarHasShader(GameObject avatar)
        {
            Renderer[] renderers = avatar.GetComponentsInChildren<Renderer>(true);
            string errorInfo;
            return !ContainsMissingOrProblematicShaderMaterial(renderers, out errorInfo);
        }

        private static bool ShaderExists(string shaderName)
        {
            return Shader.Find(shaderName) != null;
        }
    }
}