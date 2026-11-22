using System.Collections.Generic;
using Project.Contexts.Game.Items;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor
{
    // Builds the level-won prefab: dim and flash behind the tubes, the orbit ring halves, the grains, the mascot and the ribbon.
    public static class WinFxBuilder
    {
        private const string FxFolder = "Assets/Art/Sprites/Fx";
        private const string PrefabPath = "Assets/Prefabs/Items/WinFx.prefab";
        private const string MascotPath = "Assets/Art/Sprites/UI/Journey/Luno_512.png";
        private const string RibbonPath = "Assets/Art/Sprites/UI/Popup/UI_Popup_Ribbon_Win.png";
        private const string FontPath = "Assets/Art/Fonts/PeaceSans SDF.asset";
        // Flash behind tubes, far ring over them, near ring and dim over the board, mascot and ribbon on top under the blast.
        private const int DimOrder = 29;
        private const int BehindOrder = -2;
        private const int FrontOrder = 30;
        private const int StageOrder = 40;
        private const int GhostCount = 3;

        // Null when an input is missing; textures come from ArtSource/Fx/make_fx.py.
        public static WinFx Build()
        {
            var halo = CompleteFxBuilder.ImportSprite("Halo", 128f);
            var lineNear = CompleteFxBuilder.ImportSprite("TouchLineNear", 128f);
            var lineFar = CompleteFxBuilder.ImportSprite("TouchLineFar", 128f);
            var glowNear = CompleteFxBuilder.ImportSprite("TouchGlowNear", 128f);
            var glowFar = CompleteFxBuilder.ImportSprite("TouchGlowFar", 128f);
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Dot.png");
            var bead = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Bead.png");
            var mascot = AssetDatabase.LoadAssetAtPath<Sprite>(MascotPath);
            var ribbon = AssetDatabase.LoadAssetAtPath<Sprite>(RibbonPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (halo == null || lineNear == null || lineFar == null || glowNear == null || glowFar == null || dot == null
                || bead == null || mascot == null || ribbon == null || font == null)
            {
                Debug.LogError($"[WinFxBuilder] Missing inputs; run ArtSource/Fx/make_fx.py {FxFolder}, import the UI art and build the UI font.");
                return null;
            }

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
            var mascotGlow = CompleteFxBuilder.AddSprite(stage.transform, "MascotGlow", halo, 0, additive);
            var ghosts = new List<Object>();
            for (var i = 0; i < GhostCount; i++)
                ghosts.Add(CompleteFxBuilder.AddSprite(stage.transform, $"Ghost{i}", mascot, 1, additive));
            var mascotRenderer = CompleteFxBuilder.AddSprite(stage.transform, "Mascot", mascot, 2, null);
            var ribbonRenderer = CompleteFxBuilder.AddSprite(stage.transform, "Ribbon", ribbon, 3, null);
            var text = new GameObject("Title").AddComponent<TextMeshPro>();
            text.transform.SetParent(ribbonRenderer.transform, false);
            text.font = font;
            text.fontSize = 9f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(ribbon.bounds.size.x * 0.8f, ribbon.bounds.size.y);
            text.sortingOrder = 4;
            text.enabled = false;

            var so = new SerializedObject(fx);
            so.FindProperty("_dim").objectReferenceValue = dim;
            so.FindProperty("_flash").objectReferenceValue = flash;
            so.FindProperty("_ringNear").objectReferenceValue = ringNearRenderer;
            so.FindProperty("_ringFar").objectReferenceValue = ringFarRenderer;
            so.FindProperty("_glowNear").objectReferenceValue = glowNearRenderer;
            so.FindProperty("_glowFar").objectReferenceValue = glowFarRenderer;
            so.FindProperty("_dust").objectReferenceValue = dust;
            so.FindProperty("_sparks").objectReferenceValue = sparks;
            so.FindProperty("_mascot").objectReferenceValue = mascotRenderer;
            so.FindProperty("_mascotGlow").objectReferenceValue = mascotGlow;
            CompleteFxBuilder.SetArray(so.FindProperty("_ghosts"), ghosts);
            so.FindProperty("_ribbon").objectReferenceValue = ribbonRenderer;
            so.FindProperty("_ribbonText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<WinFx>();
        }
    }
}
