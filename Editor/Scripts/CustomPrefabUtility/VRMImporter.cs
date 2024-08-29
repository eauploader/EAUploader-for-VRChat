#if HAS_VRM
using EAUploader.Components;
using EAUploader.UI.Components;
using Esperecyan.Unity.VRMConverterForVRChat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniGLTF;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.Core;
using VRM;
using static Esperecyan.Unity.VRMConverterForVRChat.Converter;

namespace EAUploader.CustomPrefabUtility
{
    public class VRMImporter
    {
        public static void ImportVRM(string path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = EditorUtility.OpenFilePanel("Open .vrm", "", "vrm");
            }

            if (path.StartsWithUnityAssetPath())
            {
                Debug.LogWarningFormat("disallow import from folder under the Assets");
                return;
            }
            string vrmFolder = "Assets/VRM";
            if (!AssetDatabase.IsValidFolder(vrmFolder))
            {
                AssetDatabase.CreateFolder("Assets", "VRM");
            }

            var prefabPath = Path.Combine(vrmFolder, Path.GetFileNameWithoutExtension(path) + ".prefab");
            vrmAssetPostprocessor.ImportVrmAndCreatePrefab(path, UnityPath.FromUnityPath(prefabPath));

            WaitForPrefabGeneration(prefabPath, () =>
            {
                VRMImporterWindow.ShowWindow(prefabPath);
            });
        }

        private static void WaitForPrefabGeneration(string prefabPath, Action callback)
        {
            EditorApplication.update += WaitForPrefabGenerationInternal;

            void WaitForPrefabGenerationInternal()
            {
                if (File.Exists(prefabPath))
                {
                    EditorApplication.update -= WaitForPrefabGenerationInternal;
                    callback();
                }
            }
        }
    }

    public class VRMImporterWindow : EditorWindow
    {
        private string prefabPath = "";
        private SwayingObjectsConverterSetting swayingObjectsConverterSetting;
        private OSCComponents oscComponents;

        [Serializable]
        public struct GameSwitch
        {
            public string name;
        }

        public static bool ShowWindow(string prefabPath)
        {
            VRMImporterWindow wnd = GetWindow<VRMImporterWindow>();
            wnd.titleContent = new GUIContent("VRM Importer");
            wnd.minSize = new Vector2(480, 640);
            wnd.prefabPath = prefabPath;
            wnd.ShowModal();

            return true;
        }

        public void CreateGUI()
        {
            rootVisualElement.styleSheets.Add(UI.EAUploader.styles);
            rootVisualElement.styleSheets.Add(UI.EAUploader.tailwind);

            rootVisualElement.schedule.Execute(() =>
            {
                LanguageUtility.Localization(rootVisualElement);
            }).Every(100);

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Windows/VRMImporter");
            visualTree.CloneTree(rootVisualElement);

            rootVisualElement.Q<EnumField>("springBone").Init(SwayingObjectsConverterSetting.ConvertVrmSpringBonesAndVrmSpringBoneColliderGroups);
            rootVisualElement.Q<EnumField>("springBone").RegisterCallback<ChangeEvent<Enum>>((evt) =>
            {
                swayingObjectsConverterSetting = (SwayingObjectsConverterSetting)evt.newValue;
            });

            rootVisualElement.Q<EnumField>("oscComponents").Init(OSCComponents.None);
            rootVisualElement.Q<EnumField>("oscComponents").RegisterCallback<ChangeEvent<Enum>>((evt) =>
            {
                oscComponents = (OSCComponents)evt.newValue;
            });

            rootVisualElement.Q<ShadowButton>("importButton").clicked += ImportVRMButtonClicked;

            addedShouldersPositionYSlider = rootVisualElement.Q<Slider>("addedShouldersPositionY");

            addedShouldersPositionYFiller = new VisualElement() { name = "addedShouldersPositionYFiller" };
            addedShouldersPositionYFiller.AddToClassList("unity-slider__filler");
            addedShouldersPositionYSlider.Q("unity-drag-container").Add(addedShouldersPositionYFiller);

            addedShouldersPositionYFiller.SendToBack();
            addedShouldersPositionYSlider.Q("unity-tracker").SendToBack();

            addedShouldersPositionYDragger = addedShouldersPositionYSlider.Q("unity-dragger");

            addedShouldersPositionYSlider.RegisterValueChangedCallback((evt) =>
            {
                rootVisualElement.Q<Label>("addedShouldersPositionYLabel").text = evt.newValue.ToString("F4");
            });

            addedArmaturePositionYSlider = rootVisualElement.Q<Slider>("addedArmaturePositionY");

            addedArmaturePositionYFiller = new VisualElement() { name = "addedArmaturePositionYFiller" };
            addedArmaturePositionYFiller.AddToClassList("unity-slider__filler");
            addedArmaturePositionYSlider.Q("unity-drag-container").Add(addedArmaturePositionYFiller);

            addedArmaturePositionYFiller.SendToBack();
            addedArmaturePositionYSlider.Q("unity-tracker").SendToBack();

            addedArmaturePositionYDragger = addedArmaturePositionYSlider.Q("unity-dragger");

            addedArmaturePositionYSlider.RegisterValueChangedCallback((evt) =>
            {
                rootVisualElement.Q<Label>("addedArmaturePositionYLabel").text = evt.newValue.ToString("F4");
            });
        }

        Slider addedShouldersPositionYSlider;
        VisualElement addedShouldersPositionYFiller;
        VisualElement addedShouldersPositionYDragger;

        Slider addedArmaturePositionYSlider;
        VisualElement addedArmaturePositionYFiller;
        VisualElement addedArmaturePositionYDragger;

        public void Update()
        {
            if (addedShouldersPositionYSlider != null)
            {
                addedShouldersPositionYFiller.style.width = new StyleLength(new Length((addedShouldersPositionYDragger.transform.position.x + 10) / addedShouldersPositionYSlider.worldBound.width * 100, LengthUnit.Percent));
            }

            if (addedArmaturePositionYSlider != null)
            {
                addedArmaturePositionYFiller.style.width = new StyleLength(new Length((addedArmaturePositionYDragger.transform.position.x + 10) / addedArmaturePositionYSlider.worldBound.width * 100, LengthUnit.Percent));
            }
        }

        private void ImportVRMButtonClicked()
        {
            var prefabBlueprintId = "";
            var blueprintIds = new Dictionary<int, string>();
            var previousPrefab = PrefabManager.GetPrefab(prefabPath);
            if (previousPrefab != null)
            {
                var pipelineManager = previousPrefab.GetComponent<PipelineManager>();
                prefabBlueprintId = pipelineManager ? pipelineManager.blueprintId : "";

                GameObject[] previousRootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                blueprintIds = previousRootGameObjects
                    .Where(root => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == prefabPath)
                    .Select(root =>
                    {
                        var manager = root.GetComponent<PipelineManager>();
                        var blueprintId = manager ? manager.blueprintId : "";
                        return new
                        {
                            index = Array.IndexOf(previousRootGameObjects, root),
                            blueprintId = blueprintId != prefabBlueprintId ? blueprintId : "",
                        };
                    }).ToDictionary(
                        keySelector: indexAndBlueprintId => indexAndBlueprintId.index,
                        elementSelector: indexAndBlueprintId => indexAndBlueprintId.blueprintId
                    );
            }

            GameObject prefabInstance = Duplicator.Duplicate(previousPrefab, prefabPath, new List<string> { "" }, true);

            var clips = VRMUtility.GetAllVRMBlendShapeClips(prefabInstance);
            bool forQuest = rootVisualElement.Q<SlideToggle>("forQuest").value;
            bool takingOverSwayingParameters = rootVisualElement.Q<SlideToggle>("takingOverSwayingParameters").value;
            float addedShouldersPositionY = rootVisualElement.Q<Slider>("addedShouldersPositionY").value;
            float addedArmaturePositionY = rootVisualElement.Q<Slider>("addedArmaturePositionY").value;
            bool useShapeKeyNormalsAndTangents = rootVisualElement.Q<SlideToggle>("useShapeKeyNormalsAndTangents").value;
            Convert(
                prefabInstance: prefabInstance,
                clips: clips,
                forQuest: forQuest,
                swayingObjectsConverterSetting: swayingObjectsConverterSetting,
                takingOverSwayingParameters: takingOverSwayingParameters,
                addedShouldersPositionY: addedShouldersPositionY,
                addedArmaturePositionY: addedArmaturePositionY,
                useShapeKeyNormalsAndTangents: useShapeKeyNormalsAndTangents,
                oscComponents: oscComponents
            );

            if (!string.IsNullOrEmpty(prefabBlueprintId))
            {
                prefabInstance.GetComponent<PipelineManager>().blueprintId = prefabBlueprintId;
            }

            var eauploaderMeta = prefabInstance.GetComponent<EAUploaderMeta>();
            if (eauploaderMeta == null)
            {
                eauploaderMeta = prefabInstance.AddComponent<EAUploaderMeta>();
            }

            eauploaderMeta.type = EAUploaderMeta.PrefabType.VRM;

            PrefabUtility.ApplyPrefabInstance(prefabInstance, InteractionMode.AutomatedAction);

            GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var (avatarIndex, blueprintId) in blueprintIds)
            {
                if (string.IsNullOrEmpty(blueprintId))
                {
                    continue;
                }
                rootGameObjects[avatarIndex].GetComponent<PipelineManager>().blueprintId = blueprintId;
            }

            DestroyImmediate(prefabInstance);

            Close();
        }
    }
}
#endif