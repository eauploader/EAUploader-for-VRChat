using UnityEngine;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
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
