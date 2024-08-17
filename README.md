# EAUploader　for　VRChat
VRChatへのアバターアップロード・調整を容易にするUnityの拡張エディタ

[VCCへの追加はこちら](https://eauploader.uslog.tech/install)

`VRChat Creator Companion`又は`ALCOM`が必要です.

Unity Extension Editor for easy uploading of avatars to VRChat.

[Click here to add to VCC](https://eauploader.uslog.tech/install)

`VRChat Creator Companion` or `ALCOM` is required.


# v1.4.4 was released!
* 修正
* VRCSDK v3.7.0 対応

## ⭐ Join us on Discord!
Discordに参加し、フィードバックやご意見を是非お聞かせください。
開発への参加も大歓迎です。

Please join us on Discord and let us know your feedback and suggestions.

https://discord.gg/8CnkDeZvY7

## 🌐 Translators wanted!
We are looking for translators to help us translate and modify VRChat into other languages to make it an easy step forward for first-time users of all custom avatars. If you have experience or knowledge of the VRChat SDK or avatar modification, please contact us on Discord or email.

## 📄 Developer Documentation 【開発者向け資料】
EAUploaderのプラグインを開発するためのドキュメントを制作中です。
より使いやすいツールへと進化させていくため、是非プラグインのご提供・ご協力をお願い致します。

We are currently working on documentation to develop plug-ins for EAUploader.
We would appreciate your cooperation in providing plug-ins for EAUploader to make it a more user-friendly tool.

--- Under maintenance ---

## How to build a development environment 開発環境構築の方法
以下の方法で開発環境を構築できます。

1. VCC（VRChat Creator Companion）において空のAvaterプロジェクトを作成する
   - 注意！：既に一般ユーザー向けの方法でEAUploaderをVCCに導入している場合、Manage PackagesにおいてEasy Avater Uploader for VRChatが**Not Installed**になっていることを確認してください。
2. 作成したUnityプロジェクト内に存在する、``Packages``フォルダ配下に``tech.uslog.eauploader``という名前のフォルダを作成してください。
3. 前手順で作成したフォルダにおいて、次のコマンドを入力してください。
   - ``git clone https://github.com/Project-EAUploader/EAUploader-for-VRChat.git .``
      - 注意！：ドットを忘れないでください。ドットを忘れると、``tech.uslog.eauploader``フォルダの配下にさらにフォルダが作成され、EAUploaderが正常に動作しなくなります。
4. Unityプロジェクトを再起動し、自動的にEAUploaderのウィンドウが表示されれば、開発環境は構築できています。

Unity拡張開発において、特別なビルド手順は必要ありません。ソースコードを変更すると、Unity側で自動的にビルドが行われます。

You can build a development environment in the following ways:

1. Create an empty Avater project in VCC (VRChat Creator Companion)
    - NOTE: If you have already installed EAUploader in VCC using the general user method, please make sure that Easy Avater Uploader for VRChat is **Not Installed** in the Manage Packages. 
2. Create a folder named ``tech.uslog.eauploader`` under ``Packages`` folder in the Unity project you created.
3. In the folder created in the previous step, type the following command:
    - ``git clone https://github.com/Project-EAUploader/EAUploader-for-VRChat.git .``
      - NOTE:DO NOT FORGET ``.``. If you forget the dot, it will create more folders under ``tech.uslog.eauploader`` folder and EAUploader will not work properly.
4. Restart the Unity project, and if the EAUploader window appears automatically, the development environment has been created.

No special build procedure is required for Unity extension development. When you change the source code, Unity will automatically build it.

## Contributions コントリビュート
私たちは、アバターをより自由なものにしたいという思いから、プロジェクトに参加してくださる方を随時募集しています。皆様のご参加を心よりお待ちしております。

プロジェクトに参加するには2つの方法があります。
* [Discord](https://discord.gg/yYFru7brra)に参加し、Github組織(Project-EAUploader)のメンバーとして相談しながらタスクを担当する

* プロジェクトをフォークし、修正や機能追加のプルリクエストを行う（宣言は不要）

どちらでも構いません。ただし、作業を開始する前に、プロジェクトのスケジュールと進行中のタスク [こちら](https://github.com/orgs/Project-EAUploader/projects/1) を確認することを忘れないでください。

We are always looking for people to participate in our projects with the idea of making avatars more free for everyone. We are very pleased to have your participation in our development.

There are two ways to participate in the project
* [Join the Discord](https://discord.gg/yYFru7brra) and take charge of tasks as a member of the Github organization (Project-EAUploader) after consulting with us.

* Fork the project and make a pull request for modifications or feature additions (no declaration required).

Either is acceptable. However, remember to check the project schedule and ongoing tasks [here](https://github.com/orgs/Project-EAUploader/projects/1) before starting work.
