using System;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // Back and glass layers are shared by every color.
    [Serializable]
    public struct OrbSkin
    {
        public Sprite OuterGlow;
        public Sprite Core;
        public Sprite InnerGlow;
        // Core shader brightness and saturation scales for this color.
        public float Shade;
        public float Saturation;
    }
}
