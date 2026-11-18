using System;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // CoreGlow is the same white light for every color and empty for a hidden ball; back and glass layers are shared.
    [Serializable]
    public struct OrbSkin
    {
        public Sprite OuterGlow;
        public Sprite Core;
        public Sprite CoreGlow;
        public Sprite InnerGlow;
    }
}
