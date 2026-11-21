using System.Collections.Generic;
using Project.Contexts.Game.Items;
using UnityEditor;
using UnityEngine;

namespace Project.Editor
{
    // Builds the result beat prefab; the board prefab builder registers it with the other items.
    public static class ResultFxBuilder
    {
        private const string FxFolder = "Assets/Art/Sprites/Fx";
        private const string PrefabPath = "Assets/Prefabs/Items/ResultFx.prefab";
        // Far halves and the flash sit behind idle tubes; the near halves and grains draw over the whole board.
        private const int BehindOrder = -2;
        private const int FrontOrder = 30;
        private const int RingCount = 2;

        // Null when a texture is missing; textures come from ArtSource/Fx/make_fx.py.
        public static ResultFx Build()
        {
            var halo = CompleteFxBuilder.ImportSprite("Halo", 128f);
            var lineNear = CompleteFxBuilder.ImportSprite("TouchLineNear", 128f);
            var lineFar = CompleteFxBuilder.ImportSprite("TouchLineFar", 128f);
            var glowNear = CompleteFxBuilder.ImportSprite("TouchGlowNear", 128f);
            var glowFar = CompleteFxBuilder.ImportSprite("TouchGlowFar", 128f);
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Dot.png");
            var bead = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Bead.png");
            if (halo == null || lineNear == null || lineFar == null || glowNear == null || glowFar == null || dot == null || bead == null)
            {
                Debug.LogError($"[ResultFxBuilder] Missing textures; run ArtSource/Fx/make_fx.py {FxFolder}.");
                return null;
            }

            var additive = CompleteFxBuilder.AdditiveMaterial("FxResultSprite", null);
            var root = new GameObject("ResultFx");
            var fx = root.AddComponent<ResultFx>();
            var flash = CompleteFxBuilder.AddSprite(root.transform, "Flash", halo, BehindOrder - 1, additive);
            var ringsNear = new List<Object>();
            var ringsFar = new List<Object>();
            var glowsNear = new List<Object>();
            var glowsFar = new List<Object>();
            for (var i = 0; i < RingCount; i++)
            {
                glowsFar.Add(CompleteFxBuilder.AddSprite(root.transform, $"GlowFar{i}", glowFar, BehindOrder, additive));
                ringsFar.Add(CompleteFxBuilder.AddSprite(root.transform, $"RingFar{i}", lineFar, BehindOrder, additive));
                glowsNear.Add(CompleteFxBuilder.AddSprite(root.transform, $"GlowNear{i}", glowNear, FrontOrder, additive));
                ringsNear.Add(CompleteFxBuilder.AddSprite(root.transform, $"RingNear{i}", lineNear, FrontOrder + 1, additive));
            }

            var so = new SerializedObject(fx);
            so.FindProperty("_flash").objectReferenceValue = flash;
            CompleteFxBuilder.SetArray(so.FindProperty("_ringsNear"), ringsNear);
            CompleteFxBuilder.SetArray(so.FindProperty("_ringsFar"), ringsFar);
            CompleteFxBuilder.SetArray(so.FindProperty("_glowsNear"), glowsNear);
            CompleteFxBuilder.SetArray(so.FindProperty("_glowsFar"), glowsFar);
            so.FindProperty("_dust").objectReferenceValue = Grains(root.transform, "Dust",
                CompleteFxBuilder.ParticleMaterial("FxResultDust", bead), 0.12f, 0.35f, false);
            so.FindProperty("_sparks").objectReferenceValue = Grains(root.transform, "Sparks",
                CompleteFxBuilder.ParticleMaterial("FxResultSpark", dot), 0.02f, 0f, true);
            so.FindProperty("_motes").objectReferenceValue = Grains(root.transform, "Motes",
                CompleteFxBuilder.ParticleMaterial("FxResultMote", dot), 0f, 0f, false);
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ResultFx>();
        }

        // Emitted from code; each grain slows by drag, falls by gravity and shrinks as it fades.
        private static ParticleSystem Grains(Transform parent, string name, Material material, float drag, float gravity,
            bool stretched)
        {
            var system = CompleteFxBuilder.Particles(parent, name, 128, material, FrontOrder + 2);
            var main = system.main;
            main.gravityModifier = gravity;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = CompleteFxBuilder.Fade((0f, 1f), (0.6f, 0.8f), (1f, 0f));
            if (drag > 0f)
            {
                var limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = drag;
            }
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 2f;
            }
            return system;
        }
    }
}
