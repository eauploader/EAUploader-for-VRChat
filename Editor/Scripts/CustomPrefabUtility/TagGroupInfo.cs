using EAUploader.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EAUploader.CustomPrefabUtility
{
    /// <summary>
    /// タググループ1つ分の情報。<see cref="TagManager.GetGroupedTags"/> が返す。
    /// </summary>
    public class TagGroupInfo
    {
        /// <summary>グループ名。どのグループにも属さないタグの場合はnull。</summary>
        public string Name;

        public List<string> Tags = new List<string>();

        public bool IsUngrouped => Name == null;
    }
}
