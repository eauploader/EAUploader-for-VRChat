using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EAUploader.UI.Setup.EditTools
{
    // See Docs/EditTools.md#dress-up-compatible-outfits
    internal static class OutfitBoneMapper
    {
        private static readonly string[] HIPS_NAMES = { "hips", "hip", "pelvis", "jbipchips" };

        #region Lookup

        public static Transform FindHumanBone(GameObject root, HumanBodyBones bone)
        {
            if (root == null) return null;

            var animator = root.GetComponent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                var humanBone = animator.avatar.humanDescription.human
                    .FirstOrDefault(h => h.humanName == HumanName(bone));
                if (!string.IsNullOrEmpty(humanBone.boneName))
                {
                    var found = FindDescendant(root.transform, t => t.name == humanBone.boneName);
                    if (found != null) return found;
                }
            }

            if (bone == HumanBodyBones.Hips)
            {
                return FindDescendant(root.transform, t => HIPS_NAMES.Contains(Normalize(t.name)) && t.parent != root.transform);
            }

            return null;
        }

        public static Transform FindOutfitHips(GameObject avatarRoot, GameObject outfitRoot)
        {
            var hips = FindHumanBone(outfitRoot, HumanBodyBones.Hips);
            if (hips != null && hips.parent != outfitRoot.transform) return hips;

            var avatarHips = FindHumanBone(avatarRoot, HumanBodyBones.Hips);
            if (avatarHips == null) return null;

            var avatarHipsName = Normalize(avatarHips.name);
            return FindDescendant(outfitRoot.transform,
                t => Normalize(t.name) == avatarHipsName && t.parent != outfitRoot.transform);
        }

        public static bool HasArmature(GameObject avatarRoot, GameObject outfitRoot)
        {
            return FindOutfitHips(avatarRoot, outfitRoot) != null
                && outfitRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any();
        }

        private static string HumanName(HumanBodyBones bone)
        {
            var names = HumanTrait.BoneName;
            var index = (int)bone;
            return index >= 0 && index < names.Length ? names[index] : bone.ToString();
        }

        private static Transform FindDescendant(Transform root, Func<Transform, bool> predicate)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t != root && predicate(t));
        }

        public static string Normalize(string name)
        {
            return new string(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        #endregion

        #region Bone merge

        public static bool CanMerge(GameObject avatarRoot, GameObject outfitRoot, out string error)
        {
            error = null;
            if (FindHumanBone(avatarRoot, HumanBodyBones.Hips) != null && FindOutfitHips(avatarRoot, outfitRoot) != null) return true;

            error = T7e.Get("Could not find the hips bone of the avatar or the outfit.");
            return false;
        }

        public static bool Merge(GameObject avatarRoot, GameObject outfitRoot, out string error)
        {
            if (!CanMerge(avatarRoot, outfitRoot, out error)) return false;

            var avatarHips = FindHumanBone(avatarRoot, HumanBodyBones.Hips);
            var outfitHips = FindOutfitHips(avatarRoot, outfitRoot);
            var outfitArmature = outfitHips.parent;
            var map = BuildBoneMap(avatarRoot, avatarHips, outfitRoot, outfitHips);

            foreach (var renderer in outfitRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = renderer.bones;
                for (var i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null && map.TryGetValue(bones[i], out var target)) bones[i] = target;
                }
                renderer.bones = bones;

                if (renderer.rootBone != null && map.TryGetValue(renderer.rootBone, out var rootTarget))
                {
                    renderer.rootBone = rootTarget;
                }
            }

            var unmatched = outfitArmature.GetComponentsInChildren<Transform>(true)
                .Where(t => t != outfitArmature && !map.ContainsKey(t) && t.parent != null && map.ContainsKey(t.parent))
                .ToList();

            foreach (var bone in unmatched)
            {
                bone.SetParent(map[bone.parent], true);
            }

            var keepArmature = map.Keys.Any(t => t != null && t.GetComponents<Component>().Length > 1);
            if (keepArmature)
            {
                outfitArmature.name += " (Merged)";
            }
            else
            {
                Object.DestroyImmediate(outfitArmature.gameObject);
            }

            var outfitAnimator = outfitRoot.GetComponent<Animator>();
            if (outfitAnimator != null) Object.DestroyImmediate(outfitAnimator);

            return true;
        }

        private static Dictionary<Transform, Transform> BuildBoneMap(
            GameObject avatarRoot, Transform avatarHips, GameObject outfitRoot, Transform outfitHips)
        {
            var map = new Dictionary<Transform, Transform>
            {
                [outfitHips] = avatarHips,
                [outfitHips.parent] = avatarHips.parent,
            };

            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;

                var outfitBone = FindHumanBone(outfitRoot, bone);
                var avatarBone = FindHumanBone(avatarRoot, bone);
                if (outfitBone != null && avatarBone != null && outfitBone.IsChildOf(outfitHips.parent))
                {
                    map[outfitBone] = avatarBone;
                }
            }

            foreach (var outfitBone in outfitHips.GetComponentsInChildren<Transform>(true))
            {
                if (map.ContainsKey(outfitBone) || outfitBone.parent == null) continue;
                if (!map.TryGetValue(outfitBone.parent, out var avatarParent)) continue;

                var name = Normalize(outfitBone.name);
                var match = avatarParent.Cast<Transform>().FirstOrDefault(c => Normalize(c.name) == name);
                if (match != null) map[outfitBone] = match;
            }

            return map;
        }

        #endregion
    }
}
