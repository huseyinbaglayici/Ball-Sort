using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Project.Contexts.Game.Sort;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace Project.Editor
{
    // Writes the level-won skeleton (mascot, ribbon, burst; intro and celebrate) from the packed atlas and the win settings.
    public static class WinSkeletonBuilder
    {
        private const string Folder = "Assets/Art/Spine/WinFx";
        private const string Name = "WinFx";
        // Skeleton units per source pixel, and the mix between the intro and the loop.
        public const float Scale = 0.01f;
        private const float Mix = 0.15f;
        // The mascot's drawn width in its source image; the stage is laid out in mascot widths for this screen shape.
        public const float MascotPx = 512f;
        private const float Aspect = 9f / 19.5f;
        private const int SparkCount = 8;
        private const int Samples = 10;

        // Joints in the source image, y down.
        private static readonly Vector2 MascotPivot = new(256f, 256f);
        private static readonly Vector2 BodyPivot = new(240f, 380f);
        private static readonly Vector2 HeadPivot = new(238f, 300f);
        private static readonly Vector2 AntennaPivot = new(252f, 84f);
        private static readonly Vector2 ArmLPivot = new(112f, 262f);
        private static readonly Vector2 ArmRPivot = new(355f, 268f);
        private static readonly Vector2 OrbPivot = new(447f, 180f);
        private const float HeadTopPx = 80f;
        private const float RibbonBandLift = 0.153f;
        private static readonly string[] MascotSlots = { "body", "armL", "head", "antenna", "armR", "orb" };

        private sealed class Part
        {
            public int x, y, w, h;
        }

        // Null when the atlas is missing.
        public static SkeletonDataAsset Build(WinFxSettings w)
        {
            var atlasPath = $"{Folder}/{Name}.atlas.txt";
            var partsPath = $"{Folder}/{Name}.parts.json";
            var texturePath = $"{Folder}/{Name}.png";
            if (!File.Exists(atlasPath) || !File.Exists(partsPath) || !File.Exists(texturePath))
            {
                Debug.LogError($"[WinSkeletonBuilder] Missing atlas; run ArtSource/Spine/make_win_atlas.py.");
                return null;
            }
            var regions = ReadRegions(atlasPath);
            var parts = JsonConvert.DeserializeObject<Dictionary<string, Part>>(File.ReadAllText(partsPath));

            var jsonPath = $"{Folder}/{Name}.json";
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(Skeleton(w, regions, parts), Formatting.Indented));
            AssetDatabase.ImportAsset(jsonPath);
            ImportTexture(texturePath);

            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{Name}_Material.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Spine/Skeleton"));
                AssetDatabase.CreateAsset(material, $"{Folder}/{Name}_Material.mat");
            }
            material.shader = Shader.Find("Spine/Skeleton");
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            material.SetFloat("_StraightAlphaInput", 1f);
            material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
            EditorUtility.SetDirty(material);

            var atlas = LoadOrCreate<SpineAtlasAsset>($"{Folder}/{Name}_Atlas.asset");
            atlas.atlasFile = AssetDatabase.LoadAssetAtPath<TextAsset>(atlasPath);
            atlas.materials = new[] { material };
            atlas.Clear();
            EditorUtility.SetDirty(atlas);

            var data = LoadOrCreate<SkeletonDataAsset>($"{Folder}/{Name}_SkeletonData.asset");
            data.skeletonJSON = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            data.atlasAssets = new AtlasAssetBase[] { atlas };
            data.scale = Scale;
            data.defaultMix = Mix;
            data.Clear();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ImportTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // Region name -> size, from the atlas file's bounds lines.
        private static Dictionary<string, Vector2Int> ReadRegions(string path)
        {
            var regions = new Dictionary<string, Vector2Int>();
            string current = null;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("bounds:"))
                {
                    var p = line.Substring(7).Split(',');
                    regions[current] = new Vector2Int(int.Parse(p[2].Trim()), int.Parse(p[3].Trim()));
                }
                else if (!line.Contains(":") && !line.EndsWith(".png")) current = line;
            }
            return regions;
        }

        // ---------------------------------------------------------------- Skeleton

        private static Dictionary<string, object> Skeleton(WinFxSettings w, Dictionary<string, Vector2Int> regions,
            Dictionary<string, Part> parts)
        {
            // Source pixels per screen width and height, for the screen shape the stage is laid out for.
            var pxPerWidth = MascotPx / Mathf.Max(w.MascotSize, 0.01f);
            var pxPerHeight = pxPerWidth / Aspect;
            var rest = new Vector2(w.MascotRest.x * pxPerWidth, w.MascotRest.y * pxPerHeight);
            var from = new Vector2(w.MascotFrom.x * pxPerWidth, w.MascotFrom.y * pxPerHeight);
            var control = new Vector2(from.x * 0.85f, rest.y + 0.15f * pxPerHeight);
            var ribbonSize = regions["ribbon"];
            var ribbonScale = w.RibbonWidth * pxPerWidth / ribbonSize.x;
            var mascotTop = rest.y + (MascotPivot.y - HeadTopPx);
            var ribbonY = mascotTop + w.RibbonGap * pxPerWidth + ribbonSize.y * ribbonScale * 0.5f;
            var ribbonDrop = w.RibbonDrop * pxPerWidth;
            var flashScale = w.FlashSize * 0.6f * pxPerWidth / regions["halo"].x;
            var sparkReach = w.FlashSize * 0.3f * pxPerWidth;

            var bones = new List<object>
            {
                Bone("root", null, Vector2.zero),
                Bone("burst", "root", Vector2.zero),
                Bone("flash", "burst", Vector2.zero),
            };
            for (var i = 0; i < SparkCount; i++)
                bones.Add(Bone($"spark{i}", "burst", Vector2.zero, 360f / SparkCount * i + 22.5f));
            // Animated bones start at identity: Spine keys add to the setup pose, and the keys carry the rest values.
            bones.Add(Bone("mascot", "root", Vector2.zero));
            bones.Add(Bone("body", "mascot", Joint(BodyPivot, MascotPivot)));
            bones.Add(Bone("head", "body", Joint(HeadPivot, BodyPivot)));
            bones.Add(Bone("antenna", "head", Joint(AntennaPivot, HeadPivot)));
            bones.Add(Bone("armL", "body", Joint(ArmLPivot, BodyPivot)));
            bones.Add(Bone("armR", "body", Joint(ArmRPivot, BodyPivot)));
            bones.Add(Bone("orb", "armR", Joint(OrbPivot, ArmRPivot)));
            bones.Add(Bone("ribbon", "root", Vector2.zero));

            // Draw order: burst behind, then the mascot back to front, the ribbon over everything.
            var slots = new List<object> { Slot("flash", "flash", "halo", "ffd24d00") };
            for (var i = 0; i < SparkCount; i++) slots.Add(Slot($"spark{i}", $"spark{i}", "spark", "fff2b000"));
            foreach (var part in MascotSlots) slots.Add(Slot(part, part, part, "ffffff00"));
            slots.Add(Slot("ribbon", "ribbon", "ribbon", "ffffff00"));

            var attachments = new Dictionary<string, object>
            {
                ["flash"] = Region("halo", Vector2.zero, regions["halo"]),
                // The ribbon hangs under its bone so the bone sits on the middle of its face, measured on the art; the title follows the bone.
                ["ribbon"] = Region("ribbon", new Vector2(0f, -ribbonSize.y * RibbonBandLift), ribbonSize),
            };
            for (var i = 0; i < SparkCount; i++)
                attachments[$"spark{i}"] = Region("spark", new Vector2(regions["spark"].x * 0.5f, 0f), regions["spark"]);
            attachments["body"] = PartRegion("body", parts, BodyPivot);
            attachments["head"] = PartRegion("head", parts, HeadPivot);
            attachments["antenna"] = PartRegion("antenna", parts, AntennaPivot);
            attachments["armL"] = PartRegion("armL", parts, ArmLPivot);
            attachments["armR"] = PartRegion("armR", parts, ArmRPivot);
            attachments["orb"] = PartRegion("orb", parts, OrbPivot);

            var stage = new Stage
            {
                W = w, Rest = rest, From = from, Control = control, RibbonY = ribbonY, RibbonDrop = ribbonDrop,
                RibbonScale = ribbonScale, FlashScale = flashScale, SparkReach = sparkReach,
                BobPx = w.MascotBob * MascotPx,
            };
            return new Dictionary<string, object>
            {
                ["skeleton"] = new Dictionary<string, object>
                {
                    ["spine"] = "4.3.0", ["hash"] = Name, ["x"] = -pxPerWidth * 0.5f, ["y"] = -pxPerHeight * 0.5f,
                    ["width"] = pxPerWidth, ["height"] = pxPerHeight,
                },
                ["bones"] = bones,
                ["slots"] = slots,
                ["skins"] = new List<object>
                {
                    new Dictionary<string, object> { ["name"] = "default", ["attachments"] = attachments }
                },
                ["animations"] = new Dictionary<string, object>
                {
                    ["intro"] = Intro(stage),
                    ["celebrate"] = Celebrate(stage),
                },
            };
        }

        private static Vector2 Joint(Vector2 pivot, Vector2 parent) => new(pivot.x - parent.x, -(pivot.y - parent.y));

        private static Dictionary<string, object> Bone(string name, string parent, Vector2 at, float rotation = 0f, float scale = 1f)
        {
            var bone = new Dictionary<string, object> { ["name"] = name };
            if (parent != null) bone["parent"] = parent;
            if (at.x != 0f) bone["x"] = R(at.x);
            if (at.y != 0f) bone["y"] = R(at.y);
            if (rotation != 0f) bone["rotation"] = R(rotation);
            if (scale != 1f)
            {
                bone["scaleX"] = R(scale);
                bone["scaleY"] = R(scale);
            }
            return bone;
        }

        // Every slot starts invisible, so the setup pose shows nothing before the intro.
        private static Dictionary<string, object> Slot(string name, string bone, string attachment, string color) =>
            new() { ["name"] = name, ["bone"] = bone, ["attachment"] = attachment, ["color"] = color };

        private static Dictionary<string, object> Region(string name, Vector2 at, Vector2Int size) =>
            new()
            {
                [name] = new Dictionary<string, object>
                {
                    ["x"] = R(at.x), ["y"] = R(at.y), ["width"] = size.x, ["height"] = size.y
                }
            };

        // A cut part sits where it was in the source image, relative to its joint.
        private static Dictionary<string, object> PartRegion(string name, Dictionary<string, Part> parts, Vector2 pivot)
        {
            var p = parts[name];
            var centre = new Vector2(p.x + p.w * 0.5f, p.y + p.h * 0.5f);
            return Region(name, Joint(centre, pivot), new Vector2Int(p.w, p.h));
        }

        // ---------------------------------------------------------------- Animations

        private sealed class Stage
        {
            public WinFxSettings W;
            public Vector2 Rest, From, Control;
            public float RibbonY, RibbonDrop, RibbonScale, FlashScale, SparkReach, BobPx;
        }

        // Burst at 0, the mascot swings in and lands with a squash, the ribbon drops over it; every value ends on the loop's start.
        private static Dictionary<string, object> Intro(Stage s)
        {
            var w = s.W;
            var inAt = w.MascotDelay;
            var landAt = inAt + w.MascotTime;
            var ribbonAt = landAt + w.RibbonDelay;
            var ribbonEnd = ribbonAt + w.RibbonTime;
            // The letters land on the settled ribbon, after its slam.
            var end = ribbonEnd + w.LetterDelay + w.LettersTime + 0.1f;
            var flashTime = Mathf.Max(w.FlashTime, 0.1f);
            var squashEnd = landAt + w.MascotSquashTime + w.MascotRecover;
            var sq = w.MascotSquash;

            // Flight share and the lean into the heading, upright at the landing.
            float Flight(float t) => Mathf.Clamp01((t - inAt) / Mathf.Max(w.MascotTime, 0.0001f));
            Vector2 Path(float t)
            {
                var k = OutCubic(Flight(t));
                var u = 1f - k;
                var at = u * u * s.From + 2f * u * k * s.Control + k * k * s.Rest;
                // Two decaying hops after the landing.
                var d = t - landAt;
                if (d > 0.12f && d < 0.32f) at.y += 28f * Mathf.Sin(Mathf.PI * (d - 0.12f) / 0.2f);
                else if (d >= 0.34f && d < 0.48f) at.y += 10f * Mathf.Sin(Mathf.PI * (d - 0.34f) / 0.14f);
                return at;
            }
            float Lean(float t)
            {
                var f = Flight(t);
                if (f <= 0f || f >= 1f) return 0f;
                var v = Path(t + 0.01f) - Path(t - 0.01f);
                var heading = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                return Mathf.Clamp(Mathf.DeltaAngle(90f, heading) * 0.25f, -w.MascotLean, w.MascotLean) * (1f - f);
            }
            // A damped bounce after the landing.
            float Bounce(float t, float amplitude, float period, float damping)
            {
                var d = t - landAt;
                if (d <= 0f) return 0f;
                return amplitude * Mathf.Exp(-d * damping) * Mathf.Sin(d * Mathf.PI * 2f / period);
            }
            // Impact squash, a stretch back up, then two smaller bounces on the hops.
            Vector2 MascotScale(float t)
            {
                if (t < landAt) return Vector2.one * Mathf.Lerp(0.8f, 1f, OutCubic(Flight(t)));
                var d = t - landAt;
                var hit = Mathf.Max(w.MascotSquashTime, 0.02f);
                return new Vector2(
                    Env(d, (0f, 1f), (hit, sq.x), (0.16f, 2f - sq.x * 0.85f), (0.26f, 1.1f), (0.34f, 0.96f), (0.42f, 1.04f), (0.5f, 1f)),
                    Env(d, (0f, 1f), (hit, sq.y), (0.16f, 2f - sq.y * 0.9f), (0.26f, 0.92f), (0.34f, 1.05f), (0.42f, 0.97f), (0.5f, 1f)));
            }
            // The ribbon slams in: far past full size, squashed flat on impact, stretched back and settled sharp.
            float RibbonK(float t) => Mathf.Clamp01((t - ribbonAt) / Mathf.Max(w.RibbonTime, 0.0001f));
            Vector2 RibbonScale(float t)
            {
                var k = RibbonK(t);
                return new Vector2(
                    Env(k, (0f, 0.37f), (0.45f, 1.25f), (0.6f, 1.12f), (0.78f, 0.95f), (0.9f, 1.03f), (1f, 1f)),
                    Env(k, (0f, 0.37f), (0.45f, 1.25f), (0.6f, 0.86f), (0.78f, 1.06f), (0.9f, 0.98f), (1f, 1f)));
            }
            float RibbonDropK(float t)
            {
                var k = RibbonK(t);
                return Env(k, (0f, 1f), (0.45f, -0.04f), (0.6f, 0.02f), (0.78f, -0.015f), (1f, 0f));
            }

            var flightTimes = Times(inAt, landAt, Samples);
            var landTimes = Times(landAt, Mathf.Max(squashEnd, landAt + 0.5f), 14);
            var bounceTimes = Times(landAt, landAt + 0.6f, 10);
            var mascotTimes = Join(new[] { 0f }, flightTimes, landTimes, new[] { end });
            var bones = new Dictionary<string, object>
            {
                ["mascot"] = Dict(
                    ("translate", Keys2(Path, mascotTimes)),
                    ("scale", Keys2(MascotScale, mascotTimes, 1f)),
                    ("rotate", Keys1(Lean, Join(new[] { 0f }, flightTimes, new[] { end })))),
                ["head"] = Dict(("rotate", Keys1(t => -0.4f * Lean(t) + Bounce(t, 7f, 0.3f, 6f), Join(new[] { 0f }, flightTimes, bounceTimes, new[] { end })))),
                ["antenna"] = Dict(("rotate", Keys1(t => -1.2f * Lean(t) - 10f * Flight(t) * (1f - Flight(t)) + Bounce(t, 28f, 0.24f, 5f), Join(new[] { 0f }, flightTimes, bounceTimes, new[] { end })))),
                ["armL"] = Dict(("rotate", Keys1(t => -0.8f * Lean(t) - 18f * Mathf.Sin(Mathf.PI * Flight(t)) + Bounce(t, 14f, 0.32f, 6f), Join(new[] { 0f }, flightTimes, bounceTimes, new[] { end })))),
                ["armR"] = Dict(("rotate", Keys1(t => 14f * Mathf.Sin(Mathf.PI * Flight(t)) + Bounce(t, -10f, 0.32f, 6f), Join(new[] { 0f }, flightTimes, bounceTimes, new[] { end })))),
                ["orb"] = Dict(("scale", Keys2(t =>
                {
                    var d = t - landAt;
                    var hump = d <= 0f ? 0f : Mathf.Exp(-d * 6f) * Mathf.Sin(Mathf.PI * Mathf.Min(d / 0.25f, 1f));
                    return Vector2.one * (1f + 0.35f * hump);
                }, Join(new[] { 0f }, bounceTimes, new[] { end }), 1f))),
                ["flash"] = Dict(("scale", Keys2(t =>
                    Vector2.one * (s.FlashScale * (t < 0.12f ? OutCubic(t / 0.12f) : 1f + 0.25f * Mathf.Clamp01((t - 0.12f) / flashTime))),
                    Join(Times(0f, flashTime, 6), new[] { end }), 1f))),
                ["ribbon"] = Dict(
                    ("translate", Keys2(t => new Vector2(0f, s.RibbonY + s.RibbonDrop * RibbonDropK(t)), Join(new[] { 0f }, Times(ribbonAt, ribbonEnd, 12), new[] { end }))),
                    ("scale", Keys2(t => RibbonScale(t) * s.RibbonScale, Join(new[] { 0f }, Times(ribbonAt, ribbonEnd, 12), new[] { end }), 1f)),
                    // A small damped wobble while the letters land on it.
                    ("rotate", Keys1(t => t <= ribbonEnd ? 0f : 3f * Mathf.Exp(-(t - ribbonEnd) * 5f) * Mathf.Sin((t - ribbonEnd) * Mathf.PI * 2f / 0.36f),
                        Join(new[] { 0f, ribbonEnd }, Times(ribbonEnd, end, 10))))),
            };
            var sparkTimes = Join(Times(0f, 0.4f, 6), new[] { end });
            for (var i = 0; i < SparkCount; i++)
            {
                var spread = 0.8f + 0.4f * ((i * 7) % SparkCount) / (SparkCount - 1f);
                // Bone translation is in the burst's space, so the reach follows each spark's own angle.
                var angle = (360f / SparkCount * i + 22.5f) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bones[$"spark{i}"] = Dict(
                    ("translate", Keys2(t => direction * (s.SparkReach * spread * OutCubic(Mathf.Clamp01(t / 0.4f))), sparkTimes)),
                    ("scale", Keys2(t => new Vector2(0.5f + 2.2f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f)), 1f), sparkTimes, 1f)));
            }

            var slots = new Dictionary<string, object>
            {
                ["flash"] = Dict(("alpha", Keys1(t => t < 0.08f ? t / 0.08f : Mathf.Clamp01(1f - (t - 0.08f) / (flashTime - 0.08f)) * 0.75f,
                    Join(new[] { 0f, 0.08f }, Times(0.08f, flashTime, 4), new[] { end })))),
                ["ribbon"] = Dict(("alpha", Keys1(t => Mathf.Clamp01((t - ribbonAt) / Mathf.Max(w.RibbonTime * 0.2f, 0.0001f)),
                    new[] { 0f, ribbonAt, ribbonAt + w.RibbonTime * 0.2f, end }))),
            };
            foreach (var part in MascotSlots)
                slots[part] = Dict(("alpha", Keys1(t => Mathf.Clamp01((t - inAt) / 0.06f), new[] { 0f, inAt, inAt + 0.06f, end })));
            for (var i = 0; i < SparkCount; i++)
                slots[$"spark{i}"] = Dict(("alpha", Keys1(t => Mathf.Clamp01(1f - t / 0.4f), Join(Times(0f, 0.4f, 4), new[] { end }))));
            return new Dictionary<string, object> { ["slots"] = slots, ["bones"] = bones };
        }

        // The wait for the panel: a slow bob, the head and antenna sway, the orb hand waves, the ribbon breathes.
        private static Dictionary<string, object> Celebrate(Stage s)
        {
            var w = s.W;
            var period = Mathf.Max(w.MascotBobPeriod, 0.2f);
            var times = Times(0f, period, 12);
            float Wave(float t, float phase = 0f) => Mathf.Sin(Mathf.PI * 2f * t / period + phase);
            var bones = new Dictionary<string, object>
            {
                ["mascot"] = Dict(
                    ("translate", Keys2(t => s.Rest + Vector2.up * (s.BobPx * Wave(t)), times)),
                    ("scale", Keys2(t => Vector2.one, new[] { 0f }, 1f)),
                    ("rotate", Keys1(t => 0f, new[] { 0f }))),
                ["head"] = Dict(("rotate", Keys1(t => 3f * Wave(t, 0.6f), times))),
                ["antenna"] = Dict(("rotate", Keys1(t => 7f * Wave(t, 1.4f), times))),
                ["armL"] = Dict(("rotate", Keys1(t => -4f * Wave(t, 0.3f), times))),
                ["armR"] = Dict(("rotate", Keys1(t => 9f * Wave(t), times))),
                ["orb"] = Dict(("scale", Keys2(t => Vector2.one * (1f + 0.08f * (0.5f - 0.5f * Mathf.Cos(Mathf.PI * 4f * t / period))), times, 1f))),
                ["flash"] = Dict(("scale", Keys2(t => Vector2.one, new[] { 0f }, 1f))),
                ["ribbon"] = Dict(
                    ("translate", Keys2(t => new Vector2(0f, s.RibbonY + 4f * Wave(t, 1f)), times)),
                    ("scale", Keys2(t => Vector2.one * (s.RibbonScale * (1f + 0.01f * Wave(t, 1f))), times, 1f)),
                    ("rotate", Keys1(t => 1.2f * Wave(t, 0.3f), times))),
            };
            var slots = new Dictionary<string, object>
            {
                ["flash"] = Dict(("alpha", Keys1(t => 0f, new[] { 0f }))),
                ["ribbon"] = Dict(("alpha", Keys1(t => 1f, new[] { 0f }))),
            };
            foreach (var part in MascotSlots) slots[part] = Dict(("alpha", Keys1(t => 1f, new[] { 0f })));
            for (var i = 0; i < SparkCount; i++)
            {
                bones[$"spark{i}"] = Dict(("translate", Keys2(t => Vector2.zero, new[] { 0f })), ("scale", Keys2(t => Vector2.one, new[] { 0f }, 1f)));
                slots[$"spark{i}"] = Dict(("alpha", Keys1(t => 0f, new[] { 0f })));
            }
            return new Dictionary<string, object> { ["slots"] = slots, ["bones"] = bones };
        }

        // ---------------------------------------------------------------- Keys

        // Samples a motion at the given times; each key gets bezier handles from the motion's slope, so the curve stays smooth.
        private static List<object> Keys1(Func<float, float> f, float[] times)
        {
            var keys = new List<object>();
            for (var i = 0; i < times.Length; i++)
            {
                var key = new Dictionary<string, object> { ["time"] = R(times[i]), ["value"] = R(f(times[i])) };
                if (i < times.Length - 1) key["curve"] = Handles(f, times[i], times[i + 1]);
                keys.Add(key);
            }
            return keys;
        }

        private static List<object> Keys2(Func<float, Vector2> f, float[] times, float defaultValue = 0f)
        {
            var keys = new List<object>();
            for (var i = 0; i < times.Length; i++)
            {
                var v = f(times[i]);
                var key = new Dictionary<string, object> { ["time"] = R(times[i]) };
                if (v.x != defaultValue) key["x"] = R(v.x);
                if (v.y != defaultValue) key["y"] = R(v.y);
                if (i < times.Length - 1)
                {
                    var curve = Handles(t => f(t).x, times[i], times[i + 1]);
                    curve.AddRange(Handles(t => f(t).y, times[i], times[i + 1]));
                    key["curve"] = curve;
                }
                keys.Add(key);
            }
            return keys;
        }

        // Cubic handles a third of the way in from each end, along the slope there.
        private static List<object> Handles(Func<float, float> f, float t1, float t2)
        {
            var dt = t2 - t1;
            var e = Mathf.Max(dt * 0.02f, 0.0005f);
            var d1 = (f(t1 + e) - f(t1)) / e;
            var d2 = (f(t2) - f(t2 - e)) / e;
            return new List<object>
            {
                R(t1 + dt / 3f), R(f(t1) + d1 * dt / 3f), R(t2 - dt / 3f), R(f(t2) - d2 * dt / 3f)
            };
        }

        private static float[] Times(float from, float to, int count)
        {
            var times = new float[count];
            for (var i = 0; i < count; i++) times[i] = Mathf.Lerp(from, to, i / (count - 1f));
            return times;
        }

        // Concatenates in order and drops repeats, so a hold key never doubles the key before it.
        private static float[] Join(params float[][] parts)
        {
            var all = new List<float>();
            foreach (var part in parts)
                foreach (var t in part)
                    if (all.Count == 0 || t > all[all.Count - 1] + 0.0005f) all.Add(t);
            return all.ToArray();
        }

        private static Dictionary<string, object> Dict(params (string key, object value)[] entries)
        {
            var dict = new Dictionary<string, object>();
            foreach (var (key, value) in entries) dict[key] = value;
            return dict;
        }

        // A value through keyed points, eased between neighbours; held outside.
        private static float Env(float at, params (float time, float value)[] keys)
        {
            if (at <= keys[0].time) return keys[0].value;
            for (var i = 1; i < keys.Length; i++)
            {
                if (at > keys[i].time) continue;
                var k = (at - keys[i - 1].time) / Mathf.Max(keys[i].time - keys[i - 1].time, 0.0001f);
                return Mathf.Lerp(keys[i - 1].value, keys[i].value, 0.5f - 0.5f * Mathf.Cos(Mathf.PI * k));
            }
            return keys[keys.Length - 1].value;
        }

        private static float OutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        private static float OutQuad(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        private static float R(float value) => Mathf.Round(value * 1000f) / 1000f;
    }
}
