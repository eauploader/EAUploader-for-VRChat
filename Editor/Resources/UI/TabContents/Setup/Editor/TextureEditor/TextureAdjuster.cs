using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EAUploader.UI.Setup.EditTools
{
    [Serializable]
    internal class TextureAdjustment
    {
        public float hue;
        public float saturation = 1f;
        public float brightness = 1f;
        public float contrast = 1f;
        public Color tint = Color.white;

        public bool IsIdentity =>
            Mathf.Approximately(hue, 0f)
            && Mathf.Approximately(saturation, 1f)
            && Mathf.Approximately(brightness, 1f)
            && Mathf.Approximately(contrast, 1f)
            && tint == Color.white;

        public TextureAdjustment Clone()
        {
            return (TextureAdjustment)MemberwiseClone();
        }
    }

    [Serializable]
    internal class TextureEditRecord
    {
        public string originalMaterial;
        public string sourceTexture;
        public string generatedTexture;
        public string property;
        public TextureAdjustment adjustment = new TextureAdjustment();

        public static TextureEditRecord Read(UnityEngine.Object asset)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset));
            if (importer == null || string.IsNullOrEmpty(importer.userData)) return null;

            try
            {
                var record = JsonUtility.FromJson<TextureEditRecord>(importer.userData);
                return record != null && !string.IsNullOrEmpty(record.originalMaterial) ? record : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public void Write(UnityEngine.Object asset)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset));
            if (importer == null) return;

            importer.userData = JsonUtility.ToJson(this);
            importer.SaveAndReimport();
        }
    }

    internal static class TextureAdjuster
    {
        public const int PREVIEW_MAX_SIZE = 1024;

        private static readonly int HUE = Shader.PropertyToID("_Hue");
        private static readonly int SATURATION = Shader.PropertyToID("_Saturation");
        private static readonly int BRIGHTNESS = Shader.PropertyToID("_Brightness");
        private static readonly int CONTRAST = Shader.PropertyToID("_Contrast");
        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int IS_SRGB = Shader.PropertyToID("_IsSRGB");

        private static Material adjustMaterial;

        private static Material AdjustMaterial
        {
            get
            {
                if (adjustMaterial == null)
                {
                    adjustMaterial = new Material(Resources.Load<Shader>("Shaders/TextureAdjust"))
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                return adjustMaterial;
            }
        }

        #region Processing

        public static bool IsSRGB(Texture texture)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            return importer == null || importer.sRGBTexture;
        }

        public static RenderTexture CreatePreviewTexture(Texture source)
        {
            var scale = Mathf.Min(1f, (float)PREVIEW_MAX_SIZE / Mathf.Max(source.width, source.height));
            var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32,
                IsSRGB(source) ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear)
            {
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = true,
                autoGenerateMips = true,
                wrapMode = source.wrapMode,
                filterMode = source.filterMode,
            };
            texture.Create();
            return texture;
        }

        public static void Apply(Texture source, RenderTexture destination, TextureAdjustment adjustment)
        {
            var material = AdjustMaterial;
            material.SetFloat(HUE, adjustment.hue / 360f);
            material.SetFloat(SATURATION, adjustment.saturation);
            material.SetFloat(BRIGHTNESS, adjustment.brightness);
            material.SetFloat(CONTRAST, adjustment.contrast);
            material.SetVector(TINT, new Vector4(adjustment.tint.r, adjustment.tint.g, adjustment.tint.b, 1f));
            material.SetFloat(IS_SRGB, IsSRGB(source) ? 1f : 0f);

            Graphics.Blit(source, destination, material);
        }

        public static byte[] BakeToPng(Texture source, TextureAdjustment adjustment)
        {
            var srgb = IsSRGB(source);
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;

            try
            {
                Apply(source, target, adjustment);

                RenderTexture.active = target;
                var readback = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, !srgb);
                readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readback.Apply();

                var png = readback.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(readback);
                return png;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        #endregion

        #region Assets

        public static Texture2D WriteTexture(string assetPath, byte[] png, Texture settingsSource)
        {
            File.WriteAllBytes(assetPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            CopyImportSettings(settingsSource, assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        public static Texture2D ImportImageFile(string filePath, string folder, Texture settingsSource)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var name = EditToolUtility.SanitizeFileName(Path.GetFileNameWithoutExtension(filePath));
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}{extension}");

            File.Copy(filePath, assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            CopyImportSettings(settingsSource, assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void CopyImportSettings(Texture settingsSource, string assetPath)
        {
            var destination = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (destination == null) return;

            var source = settingsSource != null
                ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(settingsSource)) as TextureImporter
                : null;

            if (source != null)
            {
                var settings = new TextureImporterSettings();
                source.ReadTextureSettings(settings);
                destination.SetTextureSettings(settings);

                destination.maxTextureSize = source.maxTextureSize;
                destination.textureCompression = source.textureCompression;
                destination.crunchedCompression = source.crunchedCompression;
                destination.compressionQuality = source.compressionQuality;

                foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                {
                    var platformSettings = source.GetPlatformTextureSettings(platform);
                    if (platformSettings.overridden)
                    {
                        destination.SetPlatformTextureSettings(platformSettings);
                    }
                }
            }
            else
            {
                destination.textureType = TextureImporterType.Default;
                destination.sRGBTexture = true;
                destination.alphaIsTransparency = true;
                destination.mipmapEnabled = true;
            }

            destination.isReadable = false;
            destination.streamingMipmaps = true;
            destination.SaveAndReimport();
        }

        #endregion
    }
}
