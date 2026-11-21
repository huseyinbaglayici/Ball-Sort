using Project.Contexts.Game.Items;
using UnityEditor;
using UnityEngine;

namespace Project.Editor
{
    // Builds the fail beat prefab; the board prefab builder registers it with the other items.
    public static class ResultFxBuilder
    {
        private const string FxFolder = "Assets/Art/Sprites/Fx";
        private const string PrefabPath = "Assets/Prefabs/Items/ResultFx.prefab";
        // Far halves sit behind idle tubes; the near halves and motes draw over the whole board.
        private const int BehindOrder = -2;
        private const int FrontOrder = 30;

        // Null when a texture is missing; textures come from ArtSource/Fx/make_fx.py.
        public static ResultFx Build()
        {
            var lineNear = CompleteFxBuilder.ImportSprite("TouchLineNear", 128f);
            var lineFar = CompleteFxBuilder.ImportSprite("TouchLineFar", 128f);
            var glowNear = CompleteFxBuilder.ImportSprite("TouchGlowNear", 128f);
            var glowFar = CompleteFxBuilder.ImportSprite("TouchGlowFar", 128f);
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Dot.png");
            if (lineNear == null || lineFar == null || glowNear == null || glowFar == null || dot == null)
            {
                Debug.LogError($"[ResultFxBuilder] Missing textures; run ArtSource/Fx/make_fx.py {FxFolder}.");
                return null;
            }

            var additive = CompleteFxBuilder.AdditiveMaterial("FxResultSprite", null);
            var root = new GameObject("ResultFx");
            var fx = root.AddComponent<ResultFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("_glowFar").objectReferenceValue = CompleteFxBuilder.AddSprite(root.transform, "GlowFar", glowFar, BehindOrder, additive);
            so.FindProperty("_ringFar").objectReferenceValue = CompleteFxBuilder.AddSprite(root.transform, "RingFar", lineFar, BehindOrder, additive);
            so.FindProperty("_glowNear").objectReferenceValue = CompleteFxBuilder.AddSprite(root.transform, "GlowNear", glowNear, FrontOrder, additive);
            so.FindProperty("_ringNear").objectReferenceValue = CompleteFxBuilder.AddSprite(root.transform, "RingNear", lineNear, FrontOrder + 1, additive);
            so.FindProperty("_motes").objectReferenceValue = Grains(root.transform, "Motes",
                CompleteFxBuilder.ParticleMaterial("FxResultMote", dot), 0f, 0f, false, FrontOrder + 2);
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ResultFx>();
        }

        // Emitted from code; each grain slows by drag, falls by gravity and shrinks as it fades.
        internal static ParticleSystem Grains(Transform parent, string name, Material material, float drag, float gravity,
            bool stretched, int order)
        {
            var system = CompleteFxBuilder.Particles(parent, name, 128, material, order);
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
