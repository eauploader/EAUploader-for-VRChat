#if HAS_MA
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using System;
using UnityEditor;
using UnityEngine;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md#dress-up-compatible-outfits
    internal static class OutfitModularAvatar
    {
        public static bool Setup(GameObject avatarRoot, GameObject outfitRoot, out string error)
        {
            error = null;

            var avatarAnimator = avatarRoot.GetComponent<Animator>();
            if (avatarAnimator != null) avatarAnimator.Rebind();

            var outfitAnimator = outfitRoot.GetComponent<Animator>();
            if (outfitAnimator != null) outfitAnimator.Rebind();

            var canRunSetupOutfit = avatarAnimator != null
                && avatarAnimator.isHuman
                && avatarAnimator.GetBoneTransform(HumanBodyBones.Hips) != null;

            if (canRunSetupOutfit)
            {
                try
                {
                    SetupOutfit.SetupOutfitUI(outfitRoot);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Modular Avatar Setup Outfit failed: {e.Message}");
                }
            }

            if (outfitRoot.GetComponentInChildren<ModularAvatarMergeArmature>(true) != null) return true;

            return SetupMergeArmature(avatarRoot, outfitRoot, out error);
        }

        private static bool SetupMergeArmature(GameObject avatarRoot, GameObject outfitRoot, out string error)
        {
            error = null;

            var avatarHips = OutfitBoneMapper.FindHumanBone(avatarRoot, HumanBodyBones.Hips);
            var outfitHips = OutfitBoneMapper.FindOutfitHips(avatarRoot, outfitRoot);
            if (avatarHips == null || outfitHips == null)
            {
                error = T7e.Get("Could not find the hips bone of the avatar or the outfit.");
                return false;
            }

            var merge = outfitHips.parent.gameObject.AddComponent<ModularAvatarMergeArmature>();
            merge.mergeTarget = new AvatarObjectReference
            {
                referencePath = AnimationUtility.CalculateTransformPath(avatarHips.parent, avatarRoot.transform)
            };
            merge.LockMode = ArmatureLockMode.BaseToMerge;
            merge.InferPrefixSuffix();

            return true;
        }
    }
}
#endif
