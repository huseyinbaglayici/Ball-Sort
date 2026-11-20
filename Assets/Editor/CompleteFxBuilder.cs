using System.Collections.Generic;
using Project.Contexts.Game.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor
{
    // Builds the completion finish prefab; the board prefab builder registers it with the other items.
    public static class CompleteFxBuilder
    {
        private const string FxFolder = "Assets/Art/Sprites/Fx";
        private const string MaterialFolder = "Assets/Art/Materials";
        private const string PrefabPath = "Assets/Prefabs/Items/CompleteFx.prefab";
        // Behind every tube, and above idle, raised and receiving tubes but below the target portal.
        private const int BackOrder = -1;
        private const int FrontOrder = 15;
        // Three beads on each of the disk's three rings.
        private const int BeadCount = 9;
        // One essence per orb, enough for the tallest tube.
        private const int EssenceCount = 12;

        // Null when a texture is missing; textures come from ArtSource/Fx/make_fx.py.
        public static CompleteFx Build()
        {
            var halo = ImportSprite("Halo", 128f);
            var bead = ImportSprite("Bead", 64f);
            var fresnel = ImportSprite("Fresnel", 128f);
            var gloss = ImportSprite("Gloss", 128f);
            var photon = ImportSprite("PhotonRing", 128f);
            var lens = ImportSprite("Lens", 128f);
            var core = ImportSprite("Core", 64f);
            var trail = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Trail.png");
            var wisp = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Wisp.png");
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Dot.png");
            var glowTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FxFolder}/Fx_Glow.png");
            if (halo == null || bead == null || fresnel == null || gloss == null || photon == null || lens == null || core == null
                || trail == null || wisp == null || dot == null || glowTexture == null)
            {
                Debug.LogError($"[CompleteFxBuilder] Missing textures; run ArtSource/Fx/make_fx.py {FxFolder}.");
                return null;
            }

            var additive = AdditiveMaterial("FxFinishSprite", null);
            var line = AdditiveMaterial("FxFinishLine", trail);
            var wispLine = AdditiveMaterial("FxFinishWisp", wisp);

            var root = new GameObject("CompleteFx");
            var fx = root.AddComponent<CompleteFx>();
            var back = Group(root.transform, "Back", BackOrder);
            var front = Group(root.transform, "Front", FrontOrder);

            // Back to front: halo, far disk half and trails, dark body, rim, gloss, photon ring, then the near disk half.
            var lensRenderer = AddSprite(front, "Lens", lens, 0, additive);
            var heatRenderer = AddSprite(front, "Heat", lens, 0, additive);
            var diskBack = AddLine(front, "DiskBack", line, 1);
            var wisps = new List<Object>();
            var soulGlows = new List<Object>();
            var soulCores = new List<Object>();
            for (var i = 0; i < EssenceCount; i++)
            {
                wisps.Add(AddLine(front, $"EssenceTrail{i}", line, 1));
                // The glow copy takes each orb's own glow sprite when it plays.
                soulGlows.Add(AddSprite(front, $"EssenceGlow{i}", null, 1, additive));
                soulCores.Add(AddSprite(front, $"EssenceCore{i}", bead, 1, additive));
            }
            var coreRenderer = AddSprite(front, "Body", core, 2, null);
            var rimRenderer = AddSprite(front, "Rim", fresnel, 3, additive);
            var glossRenderer = AddSprite(front, "Gloss", gloss, 4, additive);
            var photonRenderer = AddSprite(front, "PhotonRing", photon, 4, additive);
            var diskFront = AddLine(front, "DiskFront", line, 5);
            // Closing beat.
            var ringBack = AddLine(back, "HorizonBack", line, 2);
            var ringFront = AddLine(front, "HorizonFront", line, 0);
            var rippleBack = AddLine(back, "RippleBack", line, 2);
            var rippleFront = AddLine(front, "RippleFront", line, 1);
            var haloRenderer = AddSprite(front, "GemGlow", halo, 0, additive);
            var glint = AddSprite(front, "GemGlint", bead, 7, additive);
            var disk = Disk(root.transform, back, front, bead, halo, additive, wispLine, dot, glowTexture);

            var so = new SerializedObject(fx);
            so.FindProperty("_front").objectReferenceValue = front;
            so.FindProperty("_disk").objectReferenceValue = disk;
            so.FindProperty("_lens").objectReferenceValue = lensRenderer;
            so.FindProperty("_heat").objectReferenceValue = heatRenderer;
            so.FindProperty("_core").objectReferenceValue = coreRenderer;
            so.FindProperty("_rim").objectReferenceValue = rimRenderer;
            so.FindProperty("_gloss").objectReferenceValue = glossRenderer;
            so.FindProperty("_photon").objectReferenceValue = photonRenderer;
            so.FindProperty("_diskBack").objectReferenceValue = diskBack;
            so.FindProperty("_diskFront").objectReferenceValue = diskFront;
            SetArray(so.FindProperty("_soulGlows"), soulGlows);
            SetArray(so.FindProperty("_soulCores"), soulCores);
            SetArray(so.FindProperty("_wisps"), wisps);
            so.FindProperty("_ringBack").objectReferenceValue = ringBack;
            so.FindProperty("_ringFront").objectReferenceValue = ringFront;
            so.FindProperty("_rippleBack").objectReferenceValue = rippleBack;
            so.FindProperty("_rippleFront").objectReferenceValue = rippleFront;
            so.FindProperty("_halo").objectReferenceValue = haloRenderer;
            so.FindProperty("_glint").objectReferenceValue = glint;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CompleteFx>();
        }

        // Per bead a core and an aura that change sides of the disk, a trail on each side; dust on each side and the glow behind.
        private static AccretionDisk Disk(Transform root, Transform back, Transform front, Sprite bead, Sprite aura,
            Material additive, Material wispLine, Texture dot, Texture glowTexture)
        {
            var go = new GameObject("Disk");
            go.transform.SetParent(root, false);
            var disk = go.AddComponent<AccretionDisk>();
            var cores = new List<Object>();
            var glows = new List<Object>();
            var trailsFront = new List<Object>();
            var trailsBack = new List<Object>();
            for (var i = 0; i < BeadCount; i++)
            {
                glows.Add(AddSprite(front, $"BeadAura{i}", aura, 5, additive));
                cores.Add(AddSprite(front, $"Bead{i}", bead, 6, additive));
                trailsFront.Add(AddLine(front, $"BeadTrailFront{i}", wispLine, 4));
                trailsBack.Add(AddLine(back, $"BeadTrailBack{i}", wispLine, 4));
            }
            var dustMaterial = ParticleMaterial("FxFinishDust", dot);
            var so = new SerializedObject(disk);
            so.FindProperty("_back").objectReferenceValue = back;
            so.FindProperty("_front").objectReferenceValue = front;
            SetArray(so.FindProperty("_cores"), cores);
            SetArray(so.FindProperty("_glows"), glows);
            SetArray(so.FindProperty("_trailsFront"), trailsFront);
            SetArray(so.FindProperty("_trailsBack"), trailsBack);
            so.FindProperty("_dustFront").objectReferenceValue = Dust(front, "DustFront", dustMaterial);
            so.FindProperty("_dustBack").objectReferenceValue = Dust(back, "DustBack", dustMaterial);
            so.FindProperty("_glow").objectReferenceValue = Glow(back, ParticleMaterial("FxFinishGlow", glowTexture));
            so.ApplyModifiedPropertiesWithoutUndo();
            return disk;
        }

        // Emitted from code along the rings; each grain shrinks, slows and fades.
        private static ParticleSystem Dust(Transform parent, string name, Material material)
        {
            var dust = Particles(parent, name, 160, material, 3);
            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            var color = dust.colorOverLifetime;
            color.enabled = true;
            color.color = Fade((0f, 1f), (0.5f, 0.7f), (1f, 0f));
            var limit = dust.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.1f;
            limit.dampen = 0.08f;
            var noise = dust.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 1.2f;
            return dust;
        }

        // Large soft glow behind the tube that swells to its peak around the slam and fades.
        private static ParticleSystem Glow(Transform parent, Material material)
        {
            var system = Particles(parent, "Glow", 1, material, 0);
            var main = system.main;
            main.startLifetime = 1.1f;
            main.startSize = 5.3f;
            var emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.43f, 0.75f), new Keyframe(1f, 0f)));
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = Fade((0f, 0f), (0.23f, 0.18f), (1f, 0f));
            return system;
        }

        private static ParticleSystem Particles(Transform parent, string name, int max, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.maxParticleSize = 1f;
            return system;
        }

        private static ParticleSystem.MinMaxGradient Fade(params (float time, float alpha)[] keys)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                System.Array.ConvertAll(keys, k => new GradientAlphaKey(k.alpha, k.time)));
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        // White textures with the shape in alpha, imported as centred sprites.
        private static Sprite ImportSprite(string name, float pixelsPerUnit)
        {
            var path = $"{FxFolder}/Fx_{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Transform Group(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<SortingGroup>().sortingOrder = order;
            return go.transform;
        }

        private static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, int order, Material material)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (material != null) renderer.sharedMaterial = material;
            renderer.enabled = false;
            return renderer;
        }

        private static LineRenderer AddLine(Transform parent, string name, Material material, int order)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sharedMaterial = material;
            line.sortingOrder = order;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private static void SetArray(SerializedProperty property, List<Object> values)
        {
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // Additive; sprites bring their own texture, lines use the given one.
        private static Material AdditiveMaterial(string name, Texture texture) =>
            SaveMaterial(name, "Mobile/Particles/Additive", texture, false);

        // Doubles brightness through its tint, so the dust and glow read on the dark sky.
        private static Material ParticleMaterial(string name, Texture texture) =>
            SaveMaterial(name, "Legacy Shaders/Particles/Additive", texture, true);

        private static Material SaveMaterial(string name, string shaderName, Texture texture, bool tinted)
        {
            var shader = Shader.Find(shaderName);
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.mainTexture = texture;
            if (tinted) material.SetColor("_TintColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
