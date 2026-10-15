using EAUploader.Components;
using System.Linq;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md#dress-up-compatible-outfits
    internal class OutfitBuildHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -12000;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var outfits = avatarGameObject.GetComponentsInChildren<EAUploaderOutfit>(true)
                .Where(o => o.mode == EAUploaderOutfit.SetupMode.BoneMerge)
                .ToList();

            foreach (var outfit in outfits)
            {
                if (!OutfitBoneMapper.Merge(avatarGameObject, outfit.gameObject, out var error))
                {
                    Debug.LogWarning($"[EAUploader] Could not merge outfit {outfit.gameObject.name}: {error}");
                }
            }

            return true;
        }
    }
}
