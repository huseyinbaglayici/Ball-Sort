using System.Collections.Generic;
using Project.Contexts.Game.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor
{
    // Builds the key flight prefab: the key, two crisp ghost frames, the keyhole mask and the click star; the board prefab builder registers it.
    public static class KeyFlightFxBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Items/KeyFlightFx.prefab";
        // Over the tubes and shutters, under the bomb blast.
        private const int GroupOrder = 25;
        private const int GhostCount = 2;
        private const int KeyOrder = 3;

        // Null when an input is missing.
        public static KeyFlightFx Build(Sprite key)
        {
            var star = CompleteFxBuilder.ImportSprite("ClickStar", 128f);
            if (key == null || star == null)
            {
                Debug.LogError("[KeyFlightFxBuilder] Missing sprites; run ArtSource/Fx/make_fx.py and import the obstacle art.");
                return null;
            }

            var root = new GameObject("KeyFlightFx");
            root.AddComponent<SortingGroup>().sortingOrder = GroupOrder;
            var fx = root.AddComponent<KeyFlightFx>();
            var ghosts = new List<Object>();
            for (var i = 0; i < GhostCount; i++)
                ghosts.Add(CompleteFxBuilder.AddSprite(root.transform, $"Ghost{i}", key, 1, null));
            var keyRenderer = CompleteFxBuilder.AddSprite(root.transform, "Key", key, KeyOrder, null);
            // Hidden where it has gone into the lock.
            keyRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            // Crisp drawn star, same rendering as the art pack, not an additive glow.
            var flash = CompleteFxBuilder.AddSprite(root.transform, "ClickStar", star, 6, null);
            var keyhole = new GameObject("Keyhole").AddComponent<SpriteMask>();
            keyhole.transform.SetParent(root.transform, false);
            keyhole.isCustomRangeActive = true;
            keyhole.backSortingOrder = KeyOrder - 1;
            keyhole.frontSortingOrder = KeyOrder;
            keyhole.enabled = false;

            var so = new SerializedObject(fx);
            so.FindProperty("_key").objectReferenceValue = keyRenderer;
            so.FindProperty("_flash").objectReferenceValue = flash;
            so.FindProperty("_keyhole").objectReferenceValue = keyhole;
            CompleteFxBuilder.SetArray(so.FindProperty("_ghosts"), ghosts);
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<KeyFlightFx>();
        }
    }
}
