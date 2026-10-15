using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using EAUploader.UI.Windows;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using VRC.Core;
using static EAUploader.UI.Windows.DialogPro;
#if HAS_AAO
using Anatawa12.AvatarOptimizer;
#endif

namespace EAUploader.UI.Upload
{
    internal class UploadForm
    {
        private static VisualElement root;
        private static bool isCloned = false;
        private static string thumbnailUrl = null;
        private static bool isLoggedin;
        private static bool isFormNull = true;
        private static bool isConfirmTerm = false;
        private static bool Has_AAO = EAUploaderCore.HasAAO;
        private static bool isUploaded = false;

        // 追加アップロード先。Windowsは常に対象なのでフラグを持たない。
        private static bool uploadForQuest = false;
        private static bool uploadForIOS = false;

        public static void ShowContent(VisualElement rootContainer)
        {
            isCloned = false;
            isLoggedin = APIUser.IsLoggedIn;
            root = rootContainer;

            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/TabContents/Upload/Contents/UploadForm"));

            EAUploaderCore.SelectedPrefabPathChanged -= OnSelectedPrefabPathChanged;
            EAUploaderCore.SelectedPrefabPathChanged += OnSelectedPrefabPathChanged;

            root.schedule.Execute(() =>
            {
                if (APIUser.IsLoggedIn != isLoggedin)
                {
                    isLoggedin = APIUser.IsLoggedIn;
                    UpdateStatus();
                }
            }).Every(500);


            if (EAUploaderCore.selectedPrefabPath == null)
            {
                var label = new Label(T7e.Get("Select a prefab to upload"))
                {
                    style =
                    {
                        flexGrow = 1,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 24,
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                };
                root.Add(label);
                return;
            }

            var hasDescriptor = Utility.CheckAvatarHasVRCAvatarDescriptor(PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath));
            var hasShader = ShaderChecker.CheckAvatarHasShader(PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath));
            var isVRM = Utility.CheckAvatarIsVRM(PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath));

            if (!hasDescriptor || !hasShader || isVRM)
            {
                var cantUpload = new VisualElement()
                {
                    style =
                    {
                        flexGrow = 1,
                        justifyContent = Justify.Center,
                        alignSelf = Align.Center
                    }
                };
                var label = new Label(T7e.Get("This avatar can't be uploaded for the following reasons:"))
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 24,
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                };
                cantUpload.Add(label);
                if (!hasDescriptor)
                {
                    var descriptorLabel = new Label(T7e.Get("No VRCAvatarDescriptor"));
                    cantUpload.Add(descriptorLabel);
                }
                if (!hasShader)
                {
                    var shaderLabel = new Label(T7e.Get("No Shader"));
                    cantUpload.Add(shaderLabel);
                }
                if (isVRM)
                {
                    var vrmLabel = new Label(T7e.Get("Avatar is a VRM model"));
                    cantUpload.Add(vrmLabel);
                }

                root.Add(cantUpload);

                return;
            }

            CloneVisualTree();
            CheckCanUpload();

            CheckIsUploaded();
            Validate();
            UpdateStatus();

            OnSelectedPrefabPathChanged(EAUploaderCore.selectedPrefabPath);
        }

        private static void CloneVisualTree()
        {
            if (!isCloned)
            {
                root.Clear();
                var visualTree = Resources.Load<VisualTreeAsset>("UI/TabContents/Upload/Contents/UploadForm");
                visualTree.CloneTree(root);
                isCloned = true;

                BindEvents();
            }
        }

        private static void BindEvents()
        {
            root.Q<ShadowButton>("buildandtest").clicked += async () => await BuildAndTestAsync();
            root.Q<ShadowButton>("upload").clicked += async () => await UploadAsync();
            root.Q<ShadowButton>("cancel_upload").clicked += () => AvatarUploader.CancelUpload();

            SetupPlatformTargets();

            // アップロード中はボタンを操作させず、キャンセルだけ出す
            AvatarUploader.UploadingStateChanged -= OnUploadingStateChanged;
            AvatarUploader.UploadingStateChanged += OnUploadingStateChanged;
            OnUploadingStateChanged(null, AvatarUploader.IsUploading);

            root.Q<ShadowButton>("add_thumbnail").clicked += AddThumbnail;
            root.Q<ShadowButton>("remove_thumbnail").clicked += RemoveThumbnail;
            root.Q<TextFieldPro>("content-name").RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrEmpty(root.Q<TextFieldPro>("content-name").GetValue()))
                {
                    isFormNull = true;
                }
                else
                {
                    isFormNull = false;
                }
                CheckCanUpload();
            });
            root.Q<Toggle>("confirm_term").RegisterValueChangedCallback(evt =>
            {
                isConfirmTerm = evt.newValue;
                CheckCanUpload();
            });

            root.Q<SlideToggle>("avatar_optimize").RegisterValueChangedCallback(evt =>
            {
                var selectedPrefabPath = EAUploaderCore.selectedPrefabPath;
                var prefab = PrefabManager.GetPrefab(selectedPrefabPath);
                var avatarRoot = prefab.transform.root.gameObject;

#if HAS_AAO
                if (evt.newValue) 
                {
                    // Add TraceAndOptimize Component
                    var traceAndOptimize = avatarRoot.GetComponent<TraceAndOptimize>();
                    if (traceAndOptimize == null)
                    {
                        traceAndOptimize = avatarRoot.AddComponent<TraceAndOptimize>();
                    }
                }
                else
                {
                    // Remove Component
                    var traceAndOptimize = avatarRoot.GetComponent<TraceAndOptimize>();
                    if (traceAndOptimize != null)
                    {
                        UnityEngine.Object.DestroyImmediate(traceAndOptimize, true);
                    }
                }
#else
                if (evt.newValue)
                {
                    EAUploaderMessageWindow.ShowMsg(203);
                    root.Q<SlideToggle>("avatar_optimize").value = false;
                }
#endif
            });
        }

        private static void CheckCanUpload()
        {
            var uploadButton = root?.Q<ShadowButton>("upload");
            if (uploadButton == null) return;

            // アップロード中は入力状態に関わらず押させない
            uploadButton.SetEnabled(!AvatarUploader.IsUploading && !isFormNull && isConfirmTerm);
        }

        private static async Task CheckIsUploaded()
        {
            var contentName = root.Q<TextFieldPro>("content-name");
            var contentDescription = root.Q<TextFieldPro>("content-description");
            var releaseStatus = root.Q<DropdownField>("release-status");
            var tags = root.Q<ContentWarningsField>("content-warnings");
            var thumbnail = root.Q<Image>("thumbnail-image");
            var updateButton = root.Q<VisualElement>("info-buttons");
            var discardButton = root.Q<VisualElement>("discard-info");
            var saveButton = root.Q<VisualElement>("save-info");
            var vrcInfo = root.Q<VisualElement>("vrc-info");

            var avatar = await AvatarUploader.GetVRCAvatar(EAUploaderCore.selectedPrefabPath);
            if (avatar == null)
            {
                contentName.ClearValue();
                contentDescription.ClearValue();
                releaseStatus.value = "Private";
                tags.SetTags(new List<string>());
                thumbnail.image = PrefabPreview.GetPrefabPreview(EAUploaderCore.selectedPrefabPath);
                thumbnailUrl = null;
                vrcInfo.EnableInClassList("hidden", true);

                isUploaded = false;
                updateButton.EnableInClassList("hidden", true);
                return;
            }

            vrcInfo.EnableInClassList("hidden", false);
            var vrcInfoContainer = vrcInfo.Q<VisualElement>("vrc-info-container");

            var vrcInfoList = new List<VisualElement>
            {
                new Label(T7e.Get("Author: ") + avatar.Value.AuthorName),
                new Label(T7e.Get("Created At: ") + avatar.Value.CreatedAt),
                new Label(T7e.Get("Updated At: ") + avatar.Value.UpdatedAt),
                new Label(T7e.Get("Upload Count: ") + avatar.Value.Version)
            };

            vrcInfoContainer.Clear();
            foreach (var info in vrcInfoList)
            {
                vrcInfoContainer.Add(info);
            }

            // Get blueprint id
            var blueprintContainer = root.Q<VisualElement>("blueprint-container");
            var prefab = PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath);
            var pipelineManager = Utility.GetPipelineManager(prefab);
            var blueprintId = pipelineManager.blueprintId;


            if (blueprintId != null)
            {
                blueprintContainer.EnableInClassList("hidden", false);

                var copyBlueprintId = blueprintContainer.Q<Button>("copy-blueprint-id-button");
                copyBlueprintId.UnregisterCallback<ClickEvent>(CopyBlueprintId);
                copyBlueprintId.RegisterCallback<ClickEvent>(CopyBlueprintId);
                void CopyBlueprintId(ClickEvent evt)
                {
                    EditorGUIUtility.systemCopyBuffer = blueprintId;
                    DialogPro.Show(DialogType.Info, T7e.Get("Blueprint ID Copied"), T7e.Get("Blueprint ID has been copied to the clipboard."), T7e.Get("OK"), null, true);
                }


                var UnlinkVRCButton = blueprintContainer.Q<Button>("unlink-vrc-button");
                UnlinkVRCButton.UnregisterCallback<ClickEvent>(UnlinkVRC);
                UnlinkVRCButton.RegisterCallback<ClickEvent>(UnlinkVRC);
                void UnlinkVRC(ClickEvent evt)
                {
                    if (EditorUtility.DisplayDialog(T7e.Get("Unlink VRChat from this avatar"), T7e.Get("Are you sure you want to unlink VRChat from this avatar?"), T7e.Get("Yes"), T7e.Get("No")))
                    {
                        pipelineManager.AssignId();
                        Main.RefreshSelectedAvatar();
                    }
                }

            }
            else
            {
                blueprintContainer.EnableInClassList("hidden", true);
            }

            isUploaded = true;

            contentName.SetValueWithoutNotify(avatar.Value.Name);
            contentName.Reset();
            contentDescription.SetValueWithoutNotify(avatar.Value.Description);
            contentDescription.Reset();
            isFormNull = false;
            releaseStatus.value = char.ToUpper(avatar.Value.ReleaseStatus[0]) + avatar.Value.ReleaseStatus.Substring(1);
            tags.SetTags(avatar.Value.Tags);
            Texture2D cachedImage = await DownloadTexture(avatar.Value.ThumbnailImageUrl);
            thumbnail.image = cachedImage;
            thumbnailUrl = avatar.Value.ThumbnailImageUrl;

            contentName.RegisterValueChangedCallback(evt =>
            {
                if (isUploaded) updateButton.EnableInClassList("hidden", false);
            });

            contentDescription.RegisterValueChangedCallback(evt =>
            {
                if (isUploaded) updateButton.EnableInClassList("hidden", false);
            });

            releaseStatus.RegisterValueChangedCallback(evt =>
            {
                if (isUploaded) updateButton.EnableInClassList("hidden", false);
            });

            tags.OnToggleTag += (sender, e) =>
            {
                if (isUploaded) updateButton.EnableInClassList("hidden", false);
            };

            discardButton.UnregisterCallback<ClickEvent>(DiscardChanges);
            discardButton.Q<Button>().RegisterCallback<ClickEvent>(DiscardChanges);

            void DiscardChanges(ClickEvent evt)
            {
                if (!isUploaded) return;

                contentName.SetValueWithoutNotify(avatar.Value.Name);
                contentDescription.SetValueWithoutNotify(avatar.Value.Description);
                releaseStatus.value = char.ToUpper(avatar.Value.ReleaseStatus[0]) + avatar.Value.ReleaseStatus.Substring(1);
                tags.SetTags(avatar.Value.Tags);
                thumbnail.image = cachedImage;
                thumbnailUrl = avatar.Value.ThumbnailImageUrl;
                updateButton.EnableInClassList("hidden", true);
            }

            saveButton.UnregisterCallback<ClickEvent>(SaveChanges);
            saveButton.Q<Button>().RegisterCallback<ClickEvent>(SaveChanges);

            async void SaveChanges(ClickEvent evt)
            {
                if (!isUploaded) return;
                await AvatarUploader.UpdateVRCAvatar(EAUploaderCore.selectedPrefabPath, contentName.GetValue(), contentDescription.GetValue(), releaseStatus.value.ToLower(), tags.Tags, thumbnailUrl);
                Main.RefreshSelectedAvatar();
                updateButton.EnableInClassList("hidden", true);
            };
        }

        private static Task<Texture2D> DownloadTexture(string url)
        {
            var tcs = new TaskCompletionSource<Texture2D>();
            var request = UnityWebRequestTexture.GetTexture(url);
            var asyncOp = request.SendWebRequest();
            asyncOp.completed += (op) =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    tcs.SetResult(((DownloadHandlerTexture)request.downloadHandler).texture);
                }
                else
                {
                    tcs.SetException(new Exception(request.error));
                }
            };
            return tcs.Task;
        }

        private static void AddThumbnail()
        {
            var path = EditorUtility.OpenFilePanel("Select Thumbnail", "", "png,jpg,jpeg");
            if (path.Length != 0)
            {
                var avatarThumbnail = root.Q<Image>("thumbnail-image");
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(path));
                avatarThumbnail.image = texture;
                thumbnailUrl = path;

                if (EditorUtility.DisplayDialog(T7e.Get("Change the thumbnail of list"), T7e.Get("Do you also change the thumbnails that appear in the EAUploader avatar list?"), T7e.Get("Yes"), T7e.Get("No")))
                {
                    PrefabPreview.SavePrefabPreview(EAUploaderCore.selectedPrefabPath, texture);
                }

                if (isUploaded)
                {
                    root.Q<VisualElement>("info-buttons").EnableInClassList("hidden", false);
                }
            }
        }

        private static void RemoveThumbnail()
        {
            var avatarThumbnail = root.Q<Image>("thumbnail-image");
            avatarThumbnail.image = PrefabPreview.GetPrefabPreview(EAUploaderCore.selectedPrefabPath);
            thumbnailUrl = null;
        }

        private static void OnSelectedPrefabPathChanged(string path)
        {
            if (path != null)
            {
                CloneVisualTree();

                var avatarThumbnail = root.Q<Image>("thumbnail-image");
                var previewImage = PrefabPreview.GetPrefabPreview(path);
                avatarThumbnail.image = previewImage;

                CheckIsUploaded();
                Validate();
                UpdateStatus();

#if HAS_AAO
                var prefab = PrefabManager.GetPrefab(path);
                var avatarRoot = prefab.transform.root.gameObject;
                var hasTraceAndOptimize = Utility.CheckAvatarHasTandO(avatarRoot);
                root.Q<SlideToggle>("avatar_optimize").value = hasTraceAndOptimize;
#endif
            }
        }

        /// <summary>
        /// アップロード先プラットフォームの選択UIを組み立てる。
        ///
        /// Quest等のモバイル向けアバターもWindowsビルドが前提のため、Windowsは常に対象。
        /// ここでは「追加でどのプラットフォームにもアップロードするか」だけを選ばせる。
        /// 実際のプラットフォーム切り替えとWindowsへの復帰はVRCSDKのマルチプラットフォームビルドが行う。
        /// </summary>
        private static void SetupPlatformTargets()
        {
            var questToggle = root.Q<SlideToggle>("upload_for_quest");
            if (questToggle != null)
            {
                questToggle.SetValueWithoutNotify(uploadForQuest);
                questToggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue &&
                        !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                    {
                        if (EditorUtility.DisplayDialog(
                                T7e.Get("Android Build Support Required"),
                                T7e.Get("Android Build Support is required to upload for Quest. Would you like to download it now?"),
                                T7e.Get("Download"), T7e.Get("Cancel")))
                        {
                            Application.OpenURL(BuildPlayerWindow.GetPlaybackEngineDownloadURL("Android"));
                        }

                        questToggle.SetValueWithoutNotify(false);
                        uploadForQuest = false;
                        return;
                    }

                    uploadForQuest = evt.newValue;
                    UpdatePlatformNote();
                });
            }

            // iOSはアカウントが対応している場合のみ選択できる
            var iosOption = root.Q<VisualElement>("ios-option");
            var iosToggle = root.Q<SlideToggle>("upload_for_ios");
            var iosSupported = ApiUserPlatforms.CurrentUserPlatforms?.SupportsiOS == true;

            if (iosOption != null)
            {
                iosOption.EnableInClassList("hidden", !iosSupported);
            }

            if (iosToggle != null)
            {
                if (!iosSupported)
                {
                    uploadForIOS = false;
                }

                iosToggle.SetValueWithoutNotify(uploadForIOS);
                iosToggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue &&
                        !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                    {
                        if (EditorUtility.DisplayDialog(
                                T7e.Get("iOS Build Support Required"),
                                T7e.Get("iOS Build Support is required to upload for iOS. Would you like to download it now?"),
                                T7e.Get("Download"), T7e.Get("Cancel")))
                        {
                            Application.OpenURL(BuildPlayerWindow.GetPlaybackEngineDownloadURL("iOS"));
                        }

                        iosToggle.SetValueWithoutNotify(false);
                        uploadForIOS = false;
                        return;
                    }

                    uploadForIOS = evt.newValue;
                    UpdatePlatformNote();
                });
            }

            UpdatePlatformNote();
        }

        private static void UpdatePlatformNote()
        {
            var note = root?.Q<Label>("platform-note");
            if (note == null) return;

            if (uploadForQuest || uploadForIOS)
            {
                note.text = T7e.Get("The build target will be switched during the upload and restored to Windows afterwards. This can take a while.");
                note.style.display = DisplayStyle.Flex;
            }
            else
            {
                note.text = string.Empty;
                note.style.display = DisplayStyle.None;
            }
        }

        private static UploadPlatforms GetSelectedPlatforms()
        {
            // Windowsは常に含める
            var platforms = UploadPlatforms.Windows;
            if (uploadForQuest) platforms |= UploadPlatforms.Android;
            if (uploadForIOS) platforms |= UploadPlatforms.IOS;
            return platforms;
        }

        private static void OnUploadingStateChanged(object sender, bool uploading)
        {
            if (root == null) return;

            var cancelContainer = root.Q<VisualElement>("upload_cancel_container");
            cancelContainer?.EnableInClassList("hidden", !uploading);

            var uploadButton = root.Q<ShadowButton>("upload");
            var buildTestButton = root.Q<ShadowButton>("buildandtest");

            if (uploading)
            {
                uploadButton?.SetEnabled(false);
                buildTestButton?.SetEnabled(false);
            }
            else
            {
                buildTestButton?.SetEnabled(true);
                CheckCanUpload();
            }
        }

        private static string GetCurrentBuildTarget()
        {
            string currentTarget = "Unsupported Target Platform";
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    currentTarget = "Windows";
                    break;
                case BuildTarget.Android:
                    currentTarget = "Android";
                    break;
                case BuildTarget.iOS:
                    currentTarget = "iOS";
                    break;
                default:
                    currentTarget = "Unsupported Target Platform";
                    break;
            }

            return currentTarget;
        }

        private static void Validate()
        {
            var prefab = PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath);
            var performanceInfos = PerformanceInfoComputer.ComputePerformanceInfos(prefab, false);

            var performanceInfoList = root.Q<VisualElement>("performance_info_list");
            performanceInfoList.Clear();

            foreach (var info in performanceInfos)
            {
                var item = new VisualElement()
                {
                    name = "performance_info_item"
                };
                item.AddToClassList("flex-row");
                item.AddToClassList("flex-1");
                item.AddToClassList("items-center");

                var icon_texture = PerformanceIcons.GetIconForPerformance(info.rating);
                var icon = new Image()
                {
                    image = icon_texture
                };
                item.Add(icon);

                string categoryName = T7e.Get(info.categoryName);
                string rating = T7e.Get(info.rating.ToString());
                string data = info.data;

                var label = new Label($"{categoryName}: {rating} ({data})");
                item.Add(label);

                performanceInfoList.Add(item);
            }
        }

        private static void UpdateStatus()
        {
            var loginStatus = root.Q<VisualElement>("login_status");
            var permissionStatusContainer = root.Q<VisualElement>("permission_status_container");
            var permissionStatus = root.Q<VisualElement>("permission_status");
            var uploadMain = root.Q<VisualElement>("upload_main");
            var uploadAction = root.Q<VisualElement>("upload_action");

            if (loginStatus != null)
            {
                loginStatus.Clear();
            }
            permissionStatus.Clear();

            if (VRC.Core.APIUser.IsLoggedIn)
            {
                var label = new Label(T7e.Get("Logged in as ") + VRC.Core.APIUser.CurrentUser.displayName);
                loginStatus.Add(label);
                permissionStatusContainer.style.display = DisplayStyle.Flex;
            }
            else
            {
                var label = new Label(T7e.Get("You need to login to upload avatar"));
                label.AddToClassList("pb-2");
                loginStatus.Add(label);

                var loginButton = new ShadowButton()
                {
                    text = T7e.Get("Login")
                };

                loginButton.clicked += () => VRCSdkControlPanel.GetWindow<VRCSdkControlPanel>().Show();
                loginStatus.Add(loginButton);

                uploadMain.style.display = DisplayStyle.None;
                permissionStatusContainer.style.display = DisplayStyle.None;
            }

            if (APIUser.CurrentUser != null && APIUser.CurrentUser.canPublishAvatars)
            {
                permissionStatus.Add(new Label(T7e.Get("You have permission to upload avatar")));
                uploadMain.style.display = DisplayStyle.Flex;
                uploadAction.style.display = DisplayStyle.Flex;
            }
            else
            {
                permissionStatus.Add(new Label(T7e.Get("You cannot upload an avatar with your current trust rank. You can immediately increase your rank by spending time on VRChat and adding friends. Or you can join VRChat+ (for a fee) to raise your rank immediately.")));
                uploadMain.style.display = DisplayStyle.None;
                uploadAction.style.display = DisplayStyle.None;
            }
        }

        private static async Task BuildAndTestAsync()
        {
            var selectedPrefabPath = EAUploaderCore.selectedPrefabPath;

            // If platform is not supported, return
            if (GetCurrentBuildTarget() == "Windows")
            {
                if (selectedPrefabPath != null)
                {
                    await AvatarUploader.BuildAndTestAsync(selectedPrefabPath);
                }
            }
            else // If platform is not "Windows"
            {
                EditorUtility.DisplayDialog(T7e.Get("Unsupported Platform"), T7e.Get("Avatar testing is only supported on Windows."), "OK");
                return;
            }
        }

        private static async Task UploadAsync()
        {
            var selectedPrefabPath = EAUploaderCore.selectedPrefabPath;
            if (string.IsNullOrEmpty(selectedPrefabPath) || !root.Q<Toggle>("confirm_term").value)
            {
                Debug.LogError("Required fields are missing or terms not confirmed.");
                return;
            }

            var contentName = root.Q<TextFieldPro>("content-name").GetValue();
            var contentDescription = root.Q<TextFieldPro>("content-description").GetValue();
            var releaseStatus = root.Q<DropdownField>("release-status").value.ToLower(CultureInfo.InvariantCulture);
            var tags = root.Q<ContentWarningsField>("content-warnings").Tags;

            string thumbnailPath = thumbnailUrl ?? PrefabPreview.GetPreviewImagePath(selectedPrefabPath);


            if (string.IsNullOrEmpty(contentName))
            {
                return;
            }

            if (string.IsNullOrEmpty(contentDescription))
            {
                contentDescription = string.Empty;
            }

            var uploadStatus = root.Q<VisualElement>("upload_status");
            uploadStatus.Clear();
            var progress = new ProgressBar()
            {
                lowValue = 0,
                highValue = 1,
            };
            uploadStatus.Add(progress);

            // ハンドラは毎回のアップロードで購読しなおすため、必ず解除できるよう変数に保持する
            void OnProgressChanged(object sender, AvatarUploader.ProgressEventArgs e)
            {
                EditorApplication.delayCall += () =>
                {
                    if (progress.panel == null) return;
                    progress.value = e.Percentage;
                    progress.title = e.Status;
                };
            }

            void OnUploadComplete(object sender, EventArgs e)
            {
                uploadStatus.Clear();
                Main.RefreshSelectedAvatar();
            }

            AvatarUploader.ProgressChanged += OnProgressChanged;
            AvatarUploader.OnComplete += OnUploadComplete;

            try
            {
                await AvatarUploader.UploadAvatarAsync(selectedPrefabPath, contentName, contentDescription,
                    releaseStatus, tags, thumbnailPath, GetSelectedPlatforms());
            }
            finally
            {
                AvatarUploader.ProgressChanged -= OnProgressChanged;
                AvatarUploader.OnComplete -= OnUploadComplete;
            }
        }
    }
}