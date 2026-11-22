using Project.Contexts.Game.Items;
using Project.Contexts.Game.Sort;
using Spine.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor
{
    // Builds the level-won prefab: dim and flash behind the tubes, the orbit ring halves, the grains, and the stage skeleton
    // (mascot rig, burst and ribbon) with the ribbon's title following its bone.
    public static class WinFxBuilder
    {
        private const string FxFolder = "Assets/Art/Sprites/Fx";
        private const string PrefabPath = "Assets/Prefabs/Items/WinFx.prefab";
        private const string FontPath = "Assets/Art/Fonts/PeaceSans SDF.asset";
        // The flash sits behind idle tubes, the far ring half just over them; the near half over the board, the dim over
        // the tubes under the grains; the stage over everything the board draws, under the bomb blast's group.
        private const int DimOrder = 29;
        private const int BehindOrder = -2;
        private const int FrontOrder = 30;
        private const int StageOrder = 40;

        // Null when an input is missing; textures come from ArtSource/Fx/make_fx.py, the atlas from ArtSource/Spine/make_win_atlas.py.
        public static WinFx Build(WinFxSettings settings)
        {
            var halo = CompleteFxBuilder.ImportSprite("Halo", 128f);
            var lineNear = CompleteFxBuilder.ImportSprite("TouchLineNear", 128f);
            var lineFar = CompleteFxBuilder.ImportSprite("TouchLineFar", 128f);
            var glowNear = CompleteFxBuilder.ImportSprite("TouchGlowNear", 128f);
            var glowFar = CompleteFxBuilder.ImportSprite("TouchGlowFar", 128f);
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Dot.png");
            var bead = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Bead.png");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (halo == null || lineNear == null || lineFar == null || glowNear == null || glowFar == null || dot == null
                || bead == null || font == null)
            {
                Debug.LogError($"[WinFxBuilder] Missing inputs; run ArtSource/Fx/make_fx.py {FxFolder} and build the UI font.");
                return null;
            }
            var skeletonData = WinSkeletonBuilder.Build(settings);
            if (skeletonData == null) return null;

            var additive = CompleteFxBuilder.AdditiveMaterial("FxResultSprite", null);
            var root = new GameObject("WinFx");
            var fx = root.AddComponent<WinFx>();
            // The dim's unit sprite is set at runtime.
            var dim = CompleteFxBuilder.AddSprite(root.transform, "Dim", null, DimOrder, null);
            var flash = CompleteFxBuilder.AddSprite(root.transform, "Flash", halo, BehindOrder - 1, additive);
            var glowFarRenderer = CompleteFxBuilder.AddSprite(root.transform, "GlowFar", glowFar, BehindOrder, additive);
            var ringFarRenderer = CompleteFxBuilder.AddSprite(root.transform, "RingFar", lineFar, BehindOrder, additive);
            var glowNearRenderer = CompleteFxBuilder.AddSprite(root.transform, "GlowNear", glowNear, FrontOrder, additive);
            var ringNearRenderer = CompleteFxBuilder.AddSprite(root.transform, "RingNear", lineNear, FrontOrder + 1, additive);
            var dust = ResultFxBuilder.Grains(root.transform, "Dust",
                CompleteFxBuilder.ParticleMaterial("FxResultDust", bead), 0.12f, 0.35f, false, FrontOrder + 2);
            var sparks = ResultFxBuilder.Grains(root.transform, "Sparks",
                CompleteFxBuilder.ParticleMaterial("FxResultSpark", dot), 0.02f, 0f, true, FrontOrder + 2);

            var stage = new GameObject("Stage");
            stage.transform.SetParent(root.transform, false);
            stage.AddComponent<SortingGroup>().sortingOrder = StageOrder;
            var skeletonObject = new GameObject("Skeleton");
            skeletonObject.transform.SetParent(stage.transform, false);
            var components = SkeletonAnimation.AddToGameObject(skeletonObject, skeletonData);
            var skeleton = components.skeletonAnimation;
            skeleton.loop = false;
            var meshRenderer = skeletonObject.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = 1;
            meshRenderer.enabled = false;

            // The title rides the ribbon bone: position, turn and scale.
            var text = new GameObject("Title").AddComponent<TextMeshPro>();
            text.transform.SetParent(skeletonObject.transform, false);
            text.font = font;
            text.fontSize = settings.RibbonFontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(6f, 2f);
            text.sortingOrder = 2;
            text.enabled = false;
            var letters = text.gameObject.AddComponent<TitleLetters>();
            var lettersSo = new SerializedObject(letters);
            lettersSo.FindProperty("_text").objectReferenceValue = text;
            lettersSo.ApplyModifiedPropertiesWithoutUndo();
            var follower = text.gameObject.AddComponent<BoneFollower>();
            follower.skeletonRenderer = components.skeletonRenderer;
            follower.boneName = "ribbon";
            follower.followLocalScale = true;
            follower.followZPosition = false;

            var so = new SerializedObject(fx);
            so.FindProperty("_dim").objectReferenceValue = dim;
            so.FindProperty("_flash").objectReferenceValue = flash;
            so.FindProperty("_ringNear").objectReferenceValue = ringNearRenderer;
            so.FindProperty("_ringFar").objectReferenceValue = ringFarRenderer;
            so.FindProperty("_glowNear").objectReferenceValue = glowNearRenderer;
            so.FindProperty("_glowFar").objectReferenceValue = glowFarRenderer;
            so.FindProperty("_dust").objectReferenceValue = dust;
            so.FindProperty("_sparks").objectReferenceValue = sparks;
            so.FindProperty("_stage").objectReferenceValue = skeleton;
            so.FindProperty("_stageRenderer").objectReferenceValue = meshRenderer;
            so.FindProperty("_ribbonText").objectReferenceValue = text;
            so.FindProperty("_letters").objectReferenceValue = letters;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<WinFx>();
        }
    }
}
