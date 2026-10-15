using UnityEngine;
using VRC.Core;
using VRC.SDKBase;
using EAUploader.Components;
#if HAS_VRM
using VRM;
#endif
#if HAS_AAO
using Anatawa12.AvatarOptimizer;
#endif

namespace EAUploader.CustomPrefabUtility
{
    public class Utility
    {
        public static float GetAvatarHeight(GameObject avatar)
        {
            var avatarDescriptor = avatar.GetComponent<VRC_AvatarDescriptor>();
            if (avatarDescriptor != null)
            {
                return avatarDescriptor.ViewPosition.y;
            }

            return 0f;
        }

        public static bool CheckAvatarHasVRCAvatarDescriptor(GameObject avatar)
        {
            if (avatar == null) return false;
            return avatar.GetComponent<VRC_AvatarDescriptor>() != null;
        }

        public static bool CheckAvatarIsVRM(GameObject avatar)
        {
#if HAS_VRM
            return avatar.GetComponent<VRMMeta>() != null;
#else
            return false;
#endif
        }

        public static bool CheckAvatarHasTandO(GameObject avatar)
        {
#if HAS_AAO
            return avatar.GetComponent<TraceAndOptimize>() != null;
#else
            return false;
#endif
        }


        public static VRC_AvatarDescriptor GetAvatarDescriptor(GameObject avatar)
        {
            return avatar.GetComponent<VRC_AvatarDescriptor>();
        }

        public static PipelineManager GetPipelineManager(GameObject avatar)
        {
            return avatar.GetComponent<PipelineManager>();
        }

        public static EAUploaderMeta GetEAUploaderMeta(GameObject avatar)
        {
            var eaUploaderMeta = avatar.GetComponent<EAUploaderMeta>();
            if (eaUploaderMeta == null)
            {
                eaUploaderMeta = avatar.AddComponent<EAUploaderMeta>();
            }
            return eaUploaderMeta;
        }
    }
}
