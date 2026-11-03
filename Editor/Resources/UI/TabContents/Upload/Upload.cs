using EAUploader.UI.Components;

namespace EAUploader.UI.Upload
{
    /// <summary>
    /// アップロード関連の共通処理。
    /// アップロード画面は編集画面(Setup)のシートに統合したので、画面としてのUIは持たない。
    /// </summary>
    internal class Main
    {
        /// <summary>
        /// アップロード完了時など、選択中アバターの変更を一覧に反映する。
        /// </summary>
        internal static void RefreshSelectedAvatar()
        {
            AvatarList.Main.RequestRefresh();
        }
    }
}
