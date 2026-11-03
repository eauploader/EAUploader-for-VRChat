using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using EAUploader;
using System.IO;
using UnityEngine.UIElements;
using UnityEngine;
using System;
using VRC.SDKBase.Editor.Api;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Ocsp;
using System.Threading.Tasks;

namespace EAUploader.UI.Components
{
    public class PrefabItemButton : Button
    {

        public PrefabItemButton(PrefabInfo prefab, Action clicked_action, bool disabled = false)
        {
            var hasDescriptor = Utility.CheckAvatarHasVRCAvatarDescriptor(PrefabManager.GetPrefab(prefab.Path));
            var hasShader = ShaderChecker.CheckAvatarHasShader(PrefabManager.GetPrefab(prefab.Path));
            var isVRM = Utility.CheckAvatarIsVRM(PrefabManager.GetPrefab(prefab.Path));
            var previewImage = new Image { image = prefab.Preview, scaleMode = ScaleMode.ScaleToFit, style = { width = 100, height = 100 } };
            Add(previewImage);

            var labelContainer = new VisualElement()
            {
                style =
                    {
                        flexDirection = FlexDirection.Column,
                        alignItems = Align.FlexStart,
                        justifyContent = Justify.Center,
                    }
            };

            var label = new Label(Path.GetFileNameWithoutExtension(prefab.Path))
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };
            labelContainer.Add(label);

            if (!hasDescriptor || !hasShader || isVRM)
            {
                var warning = new VisualElement()
                {
                    style = {
                            flexDirection = FlexDirection.Row,
                        }
                };
                warning.AddToClassList("warning");
                var warningIcon = new MaterialIcon { icon = "warning" };
                var warningLabel = new Label(T7e.Get("Can't be uploaded"));
                warning.Add(warningIcon);
                warning.Add(warningLabel);
                labelContainer.Add(warning);

                if (disabled)
                {
                    SetEnabled(false);
                }
            }


            UnityEditor.EditorApplication.delayCall += async () =>
            {
                try
                {
                    var avatar = await AvatarUploader.GetVRCAvatarAsync(prefab.Path);

                    if (avatar.HasValue)
                    {
                        var vrcName = avatar.Value.Name;
                        var name = new Label(vrcName);
                        labelContainer.Add(name);
                    }
                }
                catch (ApiErrorException e)
                {
                    Debug.LogError(e);
                }
            };

            Add(labelContainer);

            if (EAUploaderCore.selectedPrefabPath == prefab.Path)
            {
                EnableInClassList("selected", true);
            }

            if (PrefabManager.IsPinned(prefab.Path))
            {
                EnableInClassList("pinned", true);
            }

            clicked += () =>
            {
                EnableInClassList("selected", true);
                foreach (var child in parent.Children())
                {
                    if (child != this)
                    {
                        child.EnableInClassList("selected", false);
                    }
                }

                clicked_action();
            };
        }
    }
}