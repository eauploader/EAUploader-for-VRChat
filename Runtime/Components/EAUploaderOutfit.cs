using UnityEngine;

namespace EAUploader.Components
{
    // See Docs/EditTools.md#dress-up-compatible-outfits
    [DisallowMultipleComponent]
    public class EAUploaderOutfit : AvatarTagComponent
    {
        public enum SetupMode { ModularAvatar, BoneMerge, AttachToBone }

        public string sourcePrefabGuid;
        public SetupMode mode;
    }
}
