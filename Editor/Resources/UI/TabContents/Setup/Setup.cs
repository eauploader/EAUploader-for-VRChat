using EAUploader.Components;
using EAUploader.CustomPrefabUtility;
using EAUploader.UI.Components;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EAUploader.UI.Setup
{
    internal class Main
    {
        private static List<PrefabInfo> prefabsWithPreview = new List<PrefabInfo>();
        private static VisualElement root;
        private static ScrollView modelList;
        internal static Preview preview;
        private static SortOrder sortOrder = SortOrder.LastModifiedDescending;
        internal static Dictionary<string, bool> editorFoldouts = new Dictionary<string, bool>();
        private static bool isEditorInfoLoaded = false;

        public static void ShowContent(VisualElement rootElement)
        {
            root = rootElement;

            var visualTree = Resources.Load<VisualTreeAsset>("UI/TabContents/Setup/Setup");
            visualTree.CloneTree(root);

            modelList = root.Q<ScrollView>("model_list");

            var searchButton = root.Q<ShadowButton>("searchButton");
            searchButton.clicked += UpdateModelList;

            var sortDropdown = new DropdownField("", new List<string>
            {
                T7e.Get("Last Modified Descending"),
                T7e.Get("Last Modified Ascending"),
                T7e.Get("Name Descending"),
                T7e.Get("Name Ascending")
            }, 0);
            sortDropdown.RegisterValueChangedCallback(evt =>
            {
                sortOrder = (SortOrder)sortDropdown.index;
                UpdateModelList();
            });

            var sortbar = root.Q<VisualElement>("sortbar");
            sortbar.Add(sortDropdown);

            UpdateModelList();

            preview = new Preview(root.Q("avatar_preview"), EAUploaderCore.selectedPrefabPath);

            preview.ShowContent();

            if (EAUploaderCore.selectedPrefabPath != null)
            {
                UpdatePrefabInfo(EAUploaderCore.selectedPrefabPath);
                preview.UpdatePreview(EAUploaderCore.selectedPrefabPath);
            }


            ButtonClickHandler();

            root.Q<ShadowButton>("find_extentions").clicked += () =>
            {
                Application.OpenURL("https://www.uslog.tech/eauploader-plug-ins");
            };
        }

        private static async void AddPrefabsToModelListAsync()
        {
            foreach (var prefab in prefabsWithPreview)
            {
                var item = CreatePrefabItem(prefab);

                modelList.Add(item);
                await Task.Yield();
            }
        }

        private static VisualElement CreatePrefabItem(PrefabInfo prefab)
        {
            var item = new PrefabItemButton(prefab, () =>
            {
                EAUploaderCore.selectedPrefabPath = prefab.Path;
                UpdatePrefabInfo(prefab.Path);
                preview.UpdatePreview(prefab.Path);
            });
            return item;
        }

        private static async void UpdateModelList()
        {
            await Task.Yield();
            var searchQuery = root.Q<TextField>("searchQuery").value;
            GetModelList(searchQuery);
        }

        private static void GetModelList(string searchValue = "")
        {
            prefabsWithPreview = PrefabManager.GetAllPrefabsWithPreview();
            if (!string.IsNullOrEmpty(searchValue))
            {
                prefabsWithPreview = prefabsWithPreview.Where(prefab => prefab.Name.Contains(searchValue)).ToList();
            }

            var pinnedPrefabs = prefabsWithPreview.Where(p => p.Status == EAUploaderMeta.PrefabStatus.Pinned).ToList();
            var unpinnedPrefabs = prefabsWithPreview.Where(p => p.Status != EAUploaderMeta.PrefabStatus.Pinned).ToList();

            switch (sortOrder)
            {
                case SortOrder.LastModifiedDescending:
                    pinnedPrefabs = pinnedPrefabs.OrderByDescending(p => p.LastModified).ToList();
                    unpinnedPrefabs = unpinnedPrefabs.OrderByDescending(p => p.LastModified).ToList();
                    break;
                case SortOrder.LastModifiedAscending:
                    pinnedPrefabs = pinnedPrefabs.OrderBy(p => p.LastModified).ToList();
                    unpinnedPrefabs = unpinnedPrefabs.OrderBy(p => p.LastModified).ToList();
                    break;
                case SortOrder.NameDescending:
                    pinnedPrefabs = pinnedPrefabs.OrderByDescending(p => p.Name).ToList();
                    unpinnedPrefabs = unpinnedPrefabs.OrderByDescending(p => p.Name).ToList();
                    break;
                case SortOrder.NameAscending:
                    pinnedPrefabs = pinnedPrefabs.OrderBy(p => p.Name).ToList();
                    unpinnedPrefabs = unpinnedPrefabs.OrderBy(p => p.Name).ToList();
                    break;
            }

            prefabsWithPreview = pinnedPrefabs.Concat(unpinnedPrefabs).ToList();

            if (modelList != null)
            {
                modelList.Clear();
                AddPrefabsToModelListAsync();
            }
        }

        internal static void UpdatePrefabInfo(string prefabPath)
        {
            var prefabInfo = root.Q<VisualElement>("prefab_info");
            prefabInfo.Clear();

            var prefabName = new Label(T7e.Get("Now selecting Prefab: ") + Path.GetFileNameWithoutExtension(prefabPath))
            {
                style =
                {
                    fontSize = 20,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            prefabInfo.Add(prefabName);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var prefabHeight = new Label(T7e.Get("Height: ") + Utility.GetAvatarHeight(prefab) + "m")
            {
                style =
                {
                    fontSize = 15,
                    unityFontStyleAndWeight = FontStyle.Normal
                }
            };
            prefabInfo.Add(prefabHeight);

            BuildEditor();
        }

        private static void ButtonClickHandler()
        {
            var changeNameButton = root.Q<ShadowButton>("change_name");
            changeNameButton.clicked += ChangeNameButtonClicked;
            var pinButton = root.Q<ShadowButton>("pin_model");
            pinButton.clicked += PinButtonClicked;
            var deleteButton = root.Q<ShadowButton>("delete_model");
            deleteButton.clicked += DeleteButtonClicked;
            var importExtentionButton = root.Q<ShadowButton>("import_extentions");
            importExtentionButton.clicked += ImportExtentionButtonClicked;
        }

        private static void ChangeNameButtonClicked()
        {
            var renameWindow = ScriptableObject.CreateInstance<CustomPrefabUtility.RenamePrefabWindow>();
            if (renameWindow.ShowWindow(EAUploaderCore.selectedPrefabPath))
            {
                GetModelList();
            }
        }

        private static void PinButtonClicked()
        {
            PrefabManager.PinPrefab(EAUploaderCore.selectedPrefabPath);
            GetModelList();
        }

        private static void DeleteButtonClicked()
        {
            if (PrefabManager.ShowDeletePrefabDialog(EAUploaderCore.selectedPrefabPath))
            {
                GetModelList();
            }
        }

        private static void ImportExtentionButtonClicked()
        {
            ImportAsset(EditorUtility.OpenFilePanelWithFilters(T7e.Get("Import Extentions"), "", new[] { T7e.Get("Import a .unitypackage file."), "unitypackage", "All files", "*" }));
        }

        private static void ImportAsset(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var fileExtension = Path.GetExtension(filePath)?.ToLower();

            switch (fileExtension)
            {
                case ".unitypackage":
                    AssetDatabase.ImportPackage(filePath, false);
                    break;
            }
            AssetDatabase.Refresh();
        }

        internal static void BuildEditor()
        {
            var avatarStatus = root.Q<VisualElement>("avatar_status");

            if (EAUploaderCore.selectedPrefabPath != null)
            {
                avatarStatus.Clear();

                var prefab = PrefabManager.GetPrefab(EAUploaderCore.selectedPrefabPath);
                var isVRM = Utility.CheckAvatarIsVRM(prefab);

                if (isVRM)
                {
                    var warning = new VisualElement()
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 4,
                        }
                    };
                    warning.AddToClassList("warning");
                    var warningIcon = new MaterialIcon { icon = "warning", style = { paddingRight = 4 } };
                    var warningLabel = new Label(T7e.Get("VRM Avatar needs to convert to VRChat Avatar"));
                    warning.Add(warningIcon);
                    warning.Add(warningLabel);
                    avatarStatus.Add(warning);

                    var button = new Button(() =>
                    {
#if HAS_VRM
                        if (VRMImporterWindow.ShowWindow(EAUploaderCore.selectedPrefabPath))
                        {
                            GetModelList();
                        }
#endif
                    })
                    {
                        text = T7e.Get("Convert to VRChat Avatar")
                    };
                    avatarStatus.Add(button);
                }
                else
                {

                    var hasDescriptor = Utility.CheckAvatarHasVRCAvatarDescriptor(prefab);
                    var hasShader = ShaderChecker.CheckAvatarHasShader(prefab);

                    if (!hasDescriptor)
                    {
                        var warning = new VisualElement()
                        {
                            style = {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 4,
                        }
                        };
                        warning.AddToClassList("warning");
                        var warningIcon = new MaterialIcon { icon = "warning", style = { paddingRight = 4 } };
                        var warningLabel = new Label(T7e.Get("No VRCAvatarDescriptor"));
                        warning.Add(warningIcon);
                        warning.Add(warningLabel);
                        avatarStatus.Add(warning);
                    }

                    if (!hasShader)
                    {
                        var warning = new VisualElement()
                        {
                            style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 4,
                        }
                        };
                        warning.AddToClassList("warning");
                        var warningIcon = new MaterialIcon { icon = "warning", style = { paddingRight = 4 } };
                        var warningLabel = new Label(T7e.Get("No Shader"));
                        warning.Add(warningIcon);
                        warning.Add(warningLabel);
                        avatarStatus.Add(warning);
                    }

                    if (hasDescriptor && hasShader)
                    {
                        var success = new VisualElement()
                        {
                            style =
                            {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 4,
                        }
                        };
                        success.AddToClassList("success");
                        var successIcon = new MaterialIcon
                        {
                            icon = "check_circle",
                            style =
                            {
                                paddingRight = 4,
                            }
                        };
                        var successLabel = new Label(T7e.Get("Ready to upload"));
                        success.Add(successIcon);
                        success.Add(successLabel);
                        avatarStatus.Add(success);

                        var meta = prefab.GetComponent<EAUploaderMeta>();

                        if (meta == null)
                        {
                            meta = prefab.AddComponent<EAUploaderMeta>();
                            meta.type = EAUploaderMeta.PrefabType.VRChat;
                        }

                        if (meta.type == EAUploaderMeta.PrefabType.VRM)
                        {
                            var info = new VisualElement()
                            {
                                style =
                                {
                                    flexDirection = FlexDirection.Row,
                                    alignItems = Align.Center,
                                    marginBottom = 4,
                                }
                            };
                            var icon = new MaterialIcon
                            {
                                icon = "info",
                                style = {
                                    paddingRight = 4,
                                }
                            };
                            var label = new Label(T7e.Get("This avatar is a converted VRM, Viewpoint Position may need to be adjusted."));
                            info.Add(icon);
                            info.Add(label);
                            avatarStatus.Add(info);
                        }
                    }
                }

                var editorRegistrations = new List<EditorRegistration>(EAUploaderEditorManager.GetRegisteredEditors());
                if (!isEditorInfoLoaded)
                {
                    foreach (var editor in editorRegistrations)
                    {
                        editorFoldouts[editor.EditorName] = false;
                    }
                    isEditorInfoLoaded = true;
                }

                var editorList = root.Q<VisualElement>("avatar_editor_list");
                editorList.Clear();

                foreach (var editor in editorRegistrations)
                {
                    var editorItem = CreateEditorItem(editor);
                    editorList.Add(editorItem);
                }
            }
        }

        private static VisualElement CreateEditorItem(EditorRegistration editor)
        {
            var item = new EditorItem(editor)
            {
                style =
                {
                    marginBottom = 8,
                }
            };

            return item;
        }

        public enum SortOrder
        {
            LastModifiedDescending,
            LastModifiedAscending,
            NameDescending,
            NameAscending
        }
    }

    internal class EditorItem : VisualElement
    {
        public EditorItem(EditorRegistration editor)
        {
            var buttonGroup = new VisualElement()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                }
            };

            var item_button = new Button(() =>
            {
                EditorApplication.ExecuteMenuItem(editor.MenuName);
            })
            {
                text = editor.EditorName,
                style =
                {
                    flexGrow = 1,
                    borderBottomRightRadius = 0,
                    borderTopRightRadius = 0,
                    // borderRightColor = new StyleColor(new Color(0.0784313725f , 0.3921568627f, 0.7058823529f,1)),
                    borderRightWidth = 1,
                },
            };

            buttonGroup.Add(item_button);

            var foldoutButton = new Button(() =>
            {
                Main.editorFoldouts[editor.EditorName] = !Main.editorFoldouts[editor.EditorName];
                Main.BuildEditor();
            })
            {
                style =
                {
                    width = 50,
                    justifyContent = Justify.Center,
                    paddingLeft = 2,
                    paddingRight = 2,
                    borderBottomLeftRadius = 0,
                    borderTopLeftRadius = 0,
                }
            };

            buttonGroup.Add(foldoutButton);

            Add(buttonGroup);

            var expandMore = new MaterialIcon { icon = "expand_more" };
            expandMore.style.fontSize = 20;
            var expandLess = new MaterialIcon { icon = "expand_less" };
            expandLess.style.fontSize = 20;

            if (editor.Requirement != null && !editor.Requirement(EAUploaderCore.selectedPrefabPath))
            {
                item_button.SetEnabled(false);
                var warning = new VisualElement()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 4,
                    }
                };
                warning.AddToClassList("warning");
                var warningIcon = new MaterialIcon { icon = "warning", style = { paddingRight = 4 } };
                var warningLabel = new Label(editor.RequirementDescription);
                warning.Add(warningIcon);
                warning.Add(warningLabel);
                Add(warning);
            }

            if (Main.editorFoldouts[editor.EditorName])
            {
                foldoutButton.Add(expandLess);
                var editorContent = new VisualElement()
                {
                    style =
                    {
                        paddingBottom = 8,
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 8,
                    }
                };
                editorContent.Add(new Label(T7e.Get("Description: ") + editor.Description));
                editorContent.Add(new Label(T7e.Get("Version: ") + editor.Version));
                editorContent.Add(new Label(T7e.Get("Author: ") + editor.Author));

                if (!string.IsNullOrEmpty(editor.Url))
                {
                    var openButton = new Button(() =>
                    {
                        Application.OpenURL(editor.Url);
                    })
                    {
                        text = T7e.Get("Open the link")
                    };

                    openButton.AddToClassList("link");
                    editorContent.Add(openButton);
                }

                Add(editorContent);
            }
            else
            {
                foldoutButton.Add(expandMore);
            }
        }
    }
}