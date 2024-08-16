using EAUploader.Components;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EAUploader.CustomPrefabUtility
{
    [Serializable]
    public class PrefabInfo
    {
        public string Path;
        public string Name;
        public DateTime LastModified;
        public EAUploaderMeta.PrefabType Type;
        public EAUploaderMeta.PrefabStatus Status;
        public EAUploaderMeta.PrefabGenre Genre;
        public Texture2D Preview { get; internal set; }

        // プレハブのタイプを設定
        public void SetType(EAUploaderMeta.PrefabType newType)
        {
            Type = newType;
        }

        // プレハブのジャンルを設定
        public void SetGenre(EAUploaderMeta.PrefabGenre newGenre)
        {
            Genre = newGenre;
        }
    }

    [Serializable]
    public class PrefabInfoList
    {
        public List<PrefabInfo> Prefabs;
    }
}
