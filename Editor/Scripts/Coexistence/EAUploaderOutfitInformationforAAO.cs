#if HAS_AAO && UNITY_EDITOR
using Anatawa12.AvatarOptimizer.API;
using EAUploader.Components;

[ComponentInformation(typeof(EAUploaderOutfit))]
internal class EAUploaderOutfitInformation : ComponentInformation<EAUploaderOutfit>
{
    protected override void CollectDependency(EAUploaderOutfit component, ComponentDependencyCollector collector)
    {
    }
}
#endif
