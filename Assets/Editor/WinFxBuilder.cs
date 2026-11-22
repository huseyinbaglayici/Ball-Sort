using Project.Contexts.Game.Items;
using Project.Contexts.Game.Sort;
using Spine.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor
{
    // Builds the level-won prefab: dim, flash, orbit ring halves, grains and the stage skeleton with its ribbon title.
    public static class WinFxBuilder
    {
        private const string FxFolder = "Assets/Art/Sprites/Fx";
        private const string PrefabPath = "Assets/Prefabs/Items/WinFx.prefab";
        private const string FontPath = "Assets/Art/Fonts/PeaceSans SDF.asset";
        private const string TitleMaterialPath = "Assets/Art/Fonts/PeaceSans SDF - Title.mat";
        // Flash behind the tubes, ring halves around them, dim under the grains, stage over the board and under the bomb blast.
        private const int DimOrder = 29;
        private const int BehindOrder = -2;
        private const int FrontOrder = 30;
        private const int StageOrder = 40;
        // Ribbon face measured on its art between the gold borders: size, arch rise per unit squared and end shrink.
        private static readonly Vector2 RibbonFace = new(4.8f, 1.27f);
        private const float RibbonArch = 0.043f;
        private const float RibbonFalloff = 0.05f;
        // The title fills this share of the face's width and height.
        private static readonly Vector2 TitleFill = new(0.92f, 0.95f);

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
            var star = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_ClickStar.png");
            var ring = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_PhotonRing.png");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (halo == null || lineNear == null || lineFar == null || glowNear == null || glowFar == null || dot == null
                || bead == null || star == null || ring == null || font == null)
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
            // Salvos: comet streaks and orb sparks fall under gravity, stars and rings pop in place, stardust drifts down.
            var comets = ResultFxBuilder.Grains(root.transform, "Comets",
                CompleteFxBuilder.ParticleMaterial("FxResultSpark", dot), 0.05f, 0.45f, true, FrontOrder + 2);
            var orbSparks = ResultFxBuilder.Grains(root.transform, "OrbSparks",
                CompleteFxBuilder.ParticleMaterial("FxResultDust", bead), 0.1f, 0.5f, false, FrontOrder + 2);
            var stars = Pops(root.transform, "Stars", CompleteFxBuilder.ParticleMaterial("FxWinStar", star), true);
            var rings = Pops(root.transform, "Rings", CompleteFxBuilder.ParticleMaterial("FxWinRing", ring), false);
            var stardust = ResultFxBuilder.Grains(root.transform, "Stardust",
                CompleteFxBuilder.ParticleMaterial("FxResultMote", dot), 0f, 0.03f, false, FrontOrder + 2);
            var drift = stardust.noise;
            drift.enabled = true;
            drift.strength = 0.25f;
            drift.frequency = 0.6f;

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
            text.fontSharedMaterial = TitleMaterial(font);
            // Laid out flat at a fixed size; the letters component scales it onto the face and bends it along the arch.
            text.fontSize = settings.RibbonFontSize;
            text.alignment = TextAlignmentOptions.Capline;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(12f, 3f);
            text.sortingOrder = 2;
            text.enabled = false;
            var letters = text.gameObject.AddComponent<TitleLetters>();
            var lettersSo = new SerializedObject(letters);
            lettersSo.FindProperty("_text").objectReferenceValue = text;
            lettersSo.FindProperty("_face").vector2Value = RibbonFace;
            lettersSo.FindProperty("_arch").floatValue = RibbonArch;
            lettersSo.FindProperty("_falloff").floatValue = RibbonFalloff;
            lettersSo.FindProperty("_fill").vector2Value = TitleFill;
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
            so.FindProperty("_comets").objectReferenceValue = comets;
            so.FindProperty("_stars").objectReferenceValue = stars;
            so.FindProperty("_orbSparks").objectReferenceValue = orbSparks;
            so.FindProperty("_rings").objectReferenceValue = rings;
            so.FindProperty("_stardust").objectReferenceValue = stardust;
            so.FindProperty("_stage").objectReferenceValue = skeleton;
            so.FindProperty("_stageRenderer").objectReferenceValue = meshRenderer;
            so.FindProperty("_ribbonText").objectReferenceValue = text;
            so.FindProperty("_letters").objectReferenceValue = letters;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<WinFx>();
        }

        // Crisp shapes that pop in place: grow fast, hold, shrink away; the stars also turn a little.
        private static ParticleSystem Pops(Transform parent, string name, Material material, bool spin)
        {
            var system = CompleteFxBuilder.Particles(parent, name, 32, material, FrontOrder + 3);
            var main = system.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.25f, 1.1f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = CompleteFxBuilder.Fade((0f, 1f), (0.7f, 1f), (1f, 0f));
            if (spin)
            {
                var rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            }
            return system;
        }

        // The UI font's look (white face, dark outline) plus a soft shadow under the letters, so they sit on the banner.
        private static Material TitleMaterial(TMP_FontAsset font)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, TitleMaterialPath);
            }
            material.CopyPropertiesFromMaterial(font.material);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0.1f, 0.05f, 0.25f, 0.6f));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -0.4f);
            material.SetFloat("_UnderlayDilate", 0.1f);
            material.SetFloat("_UnderlaySoftness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
