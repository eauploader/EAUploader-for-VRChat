using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDKBase.Validation.Performance;
using VRC.SDKBase.Validation.Performance.Stats;

public class PerformanceInfoComputer
{
    public static PerformanceInfo[] ComputePerformanceInfos(GameObject gameObject, bool isMobile)
    {
        var stats = new AvatarPerformanceStats(isMobile);
        AvatarPerformance.CalculatePerformanceStats(gameObject.name, gameObject, stats, isMobile);

        var infos = new List<PerformanceInfo>();

        var overallRating = stats.GetPerformanceRatingForCategory(AvatarPerformanceCategory.Overall);
        var overallInfo = new PerformanceInfo
        {
            category = AvatarPerformanceCategory.Overall,
            categoryName = "Overall",
            data = "",
            rating = overallRating
        };

        infos.Add(overallInfo);

        foreach (var category in Enum.GetValues(typeof(AvatarPerformanceCategory))
                     .Cast<AvatarPerformanceCategory>())
        {
            var categoryName = TryGetCategoryName(category);
            if (categoryName == null) continue;
            infos.Add(new PerformanceInfo
            {
                category = category,
                categoryName = categoryName,
                data = PerformanceData(stats, category),
                rating = stats.GetPerformanceRatingForCategory(category),
            });
        }

        return infos.OrderByDescending(x => x.rating).ToArray();
    }

    private static string PerformanceData(AvatarPerformanceStats stats, AvatarPerformanceCategory category)
    {
        switch (category)
        {
            case AvatarPerformanceCategory.None: return "(none)";
            case AvatarPerformanceCategory.Overall: return "(none)";
            case AvatarPerformanceCategory.DownloadSize: return $"{stats.downloadSizeBytes}";
            case AvatarPerformanceCategory.PolyCount: return $"{stats.polyCount}";
            case AvatarPerformanceCategory.AABB: return $"{stats.aabb}";
            case AvatarPerformanceCategory.SkinnedMeshCount: return $"{stats.skinnedMeshCount}";
            case AvatarPerformanceCategory.MeshCount: return $"{stats.meshCount}";
            case AvatarPerformanceCategory.MaterialCount: return $"{stats.materialCount}";
            /* Good-bye DynamicBone!!
            case AvatarPerformanceCategory.DynamicBoneComponentCount: return $"{stats.dynamicBone?.componentCount}";
            case AvatarPerformanceCategory.DynamicBoneSimulatedBoneCount: return $"{stats.dynamicBone?.transformCount}";
            case AvatarPerformanceCategory.DynamicBoneColliderCount: return $"{stats.dynamicBone?.colliderCount}";
            case AvatarPerformanceCategory.DynamicBoneCollisionCheckCount: return $"{stats.dynamicBone?.collisionCheckCount}";
            */
            case AvatarPerformanceCategory.PhysBoneComponentCount: return $"{stats.physBone?.componentCount}";
            case AvatarPerformanceCategory.PhysBoneTransformCount: return $"{stats.physBone?.transformCount}";
            case AvatarPerformanceCategory.PhysBoneColliderCount: return $"{stats.physBone?.colliderCount}";
            case AvatarPerformanceCategory.PhysBoneCollisionCheckCount: return $"{stats.physBone?.collisionCheckCount}";
            case AvatarPerformanceCategory.ContactCount: return $"{stats.contactCount}";
            case AvatarPerformanceCategory.AnimatorCount: return $"{stats.animatorCount}";
            case AvatarPerformanceCategory.BoneCount: return $"{stats.boneCount}";
            case AvatarPerformanceCategory.LightCount: return $"{stats.lightCount}";
            case AvatarPerformanceCategory.ParticleSystemCount: return $"{stats.particleSystemCount}";
            case AvatarPerformanceCategory.ParticleTotalCount: return $"{stats.particleTotalCount}";
            case AvatarPerformanceCategory.ParticleMaxMeshPolyCount: return $"{stats.particleMaxMeshPolyCount}";
            case AvatarPerformanceCategory.ParticleTrailsEnabled: return $"{stats.particleTrailsEnabled}";
            case AvatarPerformanceCategory.ParticleCollisionEnabled: return $"{stats.particleCollisionEnabled}";
            case AvatarPerformanceCategory.TrailRendererCount: return $"{stats.trailRendererCount}";
            case AvatarPerformanceCategory.LineRendererCount: return $"{stats.lineRendererCount}";
            case AvatarPerformanceCategory.ClothCount: return $"{stats.clothCount}";
            case AvatarPerformanceCategory.ClothMaxVertices: return $"{stats.clothMaxVertices}";
            case AvatarPerformanceCategory.PhysicsColliderCount: return $"{stats.physicsColliderCount}";
            case AvatarPerformanceCategory.PhysicsRigidbodyCount: return $"{stats.physicsRigidbodyCount}";
            case AvatarPerformanceCategory.AudioSourceCount: return $"{stats.audioSourceCount}";
            case AvatarPerformanceCategory.TextureMegabytes: return $"{stats.textureMegabytes}";
            case AvatarPerformanceCategory.AvatarPerformanceCategoryCount: return "(none)";
            default: return "(unknown)";
        }
    }

    private static string TryGetCategoryName(AvatarPerformanceCategory category)
    {
        try
        {
            return AvatarPerformanceStats.GetPerformanceCategoryDisplayName(category);
        }
        catch
        {
            // GetPerformanceCategoryDisplayName may throw KeyNotFoundException
            return null;
        }
    }

    public struct PerformanceInfo
    {
        public AvatarPerformanceCategory category;
        public string categoryName;
        public string data;
        public PerformanceRating rating;
    }
}

static class PerformanceIcons
{
    private static Texture2D _excellent;
    private static Texture2D _good;
    private static Texture2D _medium;
    private static Texture2D _poor;
    private static Texture2D _veryPoor;

    public static Texture2D Excellent => _excellent ? _excellent : _excellent = LoadIcon("Great");
    public static Texture2D Good => _good ? _good : _good = LoadIcon("Good");
    public static Texture2D Medium => _medium ? _medium : _medium = LoadIcon("Medium");
    public static Texture2D Poor => _poor ? _poor : _poor = LoadIcon("Poor");
    public static Texture2D VeryPoor => _veryPoor ? _veryPoor : _veryPoor = LoadIcon("Horrible");

    public static Texture2D GetIconForPerformance(PerformanceRating rating)
    {
        switch (rating)
        {
            case PerformanceRating.Excellent:
                return Excellent;
            case PerformanceRating.Good:
                return Good;
            case PerformanceRating.Medium:
                return Medium;
            case PerformanceRating.Poor:
                return Poor;
            case PerformanceRating.VeryPoor:
                return VeryPoor;
            case PerformanceRating.None:
                return null;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static Texture2D LoadIcon(string texName)
    {
        return Resources.Load<Texture2D>($"PerformanceIcons/Perf_{texName}_32");
    }
}