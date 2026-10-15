using System.Collections.Generic;
using UnityEngine;

namespace EAUploader.Components
{
    [DisallowMultipleComponent]
    public class EAUploaderMeta : AvatarTagComponent
    {
        public enum PrefabStatus { Show , Hidden, Pinned }
        public enum PrefabType { VRChat, VRM, Other }
        public enum PrefabGenre { Avatar, Cloth, Accessory, Other }

        public PrefabStatus status;
        public PrefabType type;
        public PrefabGenre genre;

        /// <summary>
        /// EAUploaderがこのPrefabを取り込み/更新した日時(UTC)のTicks。
        /// 0の場合は未設定で、ファイルの更新日時にフォールバックする。
        /// ファイルのタイムスタンプはコピーやunitypackage展開で元の日時を引き継ぐため、
        /// 「いつEAUploaderに取り込んだか」を別途保持する。
        /// </summary>
        public long lastModifiedTicks;

        /// <summary>
        /// インポート時のジャンル選択が済んでいるかどうか。
        /// falseの場合、アバター以外のPrefabはインポート時にジャンル設定モーダルの対象になる。
        /// </summary>
        public bool genreConfirmed;

        /// <summary>
        /// ユーザーが自由に書き込めるメモ。編集(セットアップ)画面から編集する。
        /// </summary>
        [TextArea(3, 10)]
        public string memo;

        /// <summary>
        /// このPrefabに付けられたタグ。
        /// 選べるタグ名の一覧は TagManager がプロジェクト単位で管理する。
        /// </summary>
        public List<string> tags = new List<string>();
    }
}
