using System;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>The color-specific layers of the orb art; the Back and the Front (glass) are the same for every color.</summary>
    [Serializable]
    public struct OrbSkin
    {
        public Sprite OuterGlow;
        public Sprite Core;
        public Sprite InnerGlow;
    }
}
