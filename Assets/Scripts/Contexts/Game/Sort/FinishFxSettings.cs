using System;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Completion finish settings, one palette for every tube; sizes in tube units unless named in tube widths.
    [Serializable]
    public sealed class FinishFxSettings
    {
        [Header("Palette (the same on every tube, whatever the ball colour)")]
        [Tooltip("Deep space indigo for trail tails and the glow behind the tube.")]
        public Color Deep = new Color32(46, 30, 128, 255);
        [Tooltip("Space violet for trails, bead auras and the lensing halo.")]
        public Color Violet = new Color32(128, 72, 236, 255);
        [Tooltip("Hot white-cyan for bead cores, the photon ring and the event horizon.")]
        public Color Core = new Color32(214, 250, 255, 255);
        [Tooltip("Thin gold accent tying to the cap and gem.")]
        public Color Gold = new Color32(255, 201, 0, 255);
        [Range(0f, 1f)] public float GoldShare = 0.15f;

        [Header("Disk beads")]
        [Tooltip("Angular speed of the beads in radians per second, from the start of the charge to the slam.")]
        public float ChargeSpeedFrom = 2f;
        public float ChargeSpeedTo = 15f;
        [Tooltip("Bead core and aura size before the disk share.")]
        public float BeadSize = 0.38f;
        public float BeadGlowSize = 0.75f;
        [Tooltip("Trail: seconds of past motion it shows, its longest arc in radians and its width.")]
        public float TrailTime = 0.16f;
        public float TrailMaxArc = 1.9f;
        public float TrailWidth = 0.36f;
        [Tooltip("The glow behind the tube starts this long before the slam.")]
        public float GlowLead = 0.45f;

        [Header("Singularity")]
        [Tooltip("Centre above the mouth, sphere diameter, when it starts forming and how long it takes.")]
        public float SingularityHeight = 0.75f;
        public float SingularitySize = 0.7f;
        public float FormStart = 0.05f;
        public float FormTime = 0.5f;
        [Tooltip("The sphere reads like a dark glass orb: body colour, fresnel rim alpha (brighter toward the lower right like the orbs) and the gloss cap alpha (light from the upper left).")]
        public Color Body = new Color32(9, 6, 28, 255);
        [Range(0f, 1f)] public float RimAlpha = 0.85f;
        [Range(0f, 1f)] public float GlossAlpha = 0.35f;
        [Tooltip("Soft violet halo behind the sphere: diameter over the sphere's and its alpha.")]
        public float LensSize = 2.6f;
        [Range(0f, 1f)] public float LensAlpha = 0.35f;
        [Tooltip("Hot rim round the sphere in the portal edge's heat colours: outer and inner colour, diameter over the sphere's and alpha.")]
        public Color Heat = new Color32(236, 94, 159, 255);
        public Color HeatCore = new Color32(255, 203, 129, 255);
        public float HeatSize = 1.75f;
        [Range(0f, 1f)] public float HeatAlpha = 0.55f;
        [Tooltip("Thin photon ring hugging the sphere: diameter over the sphere's, alpha and spin of its bright side in degrees per second.")]
        public float PhotonSize = 1.22f;
        [Range(0f, 1f)] public float PhotonAlpha = 0.9f;
        public float PhotonSpin = 160f;
        [Tooltip("Accretion disk: diameter over the sphere's, height over width, tilt in degrees, line width, how much faster its beads turn and their size share.")]
        public float DiskSize = 2.1f;
        [Range(0.1f, 0.6f)] public float DiskAspect = 0.26f;
        public float DiskTilt = -10f;
        public float DiskWidth = 0.08f;
        public float AccretionSpin = 1.8f;
        [Range(0.1f, 1f)] public float DiskBeadScale = 0.32f;
        [Tooltip("Disk beads keep their auras soft and shed little dust, so the sphere and its disk stay readable.")]
        [Range(0f, 1f)] public float DiskAuraAlpha = 0.4f;
        public float DiskDustSpacing = 0.9f;
        [Tooltip("The singularity drops into the gem over this long, ending when the cap seats.")]
        public float CollapseTime = 0.12f;
        [Tooltip("Each ball in turn, bottom first, has its glow drained into the singularity as a wisp: first start, longest stagger, travel time, tail lag, width, side reach in tube units and how far the colour lifts toward white.")]
        public float WispStart = 0.1f;
        public float WispStagger = 0.065f;
        public float WispTime = 0.3f;
        public float WispLag = 0.12f;
        public float WispWidth = 0.08f;
        public float WispSide = 0.55f;
        [Tooltip("The last wisp leaves by this time, so tall tubes stagger faster; the glow dims to this share and comes back over this long as the cap seats.")]
        public float DrainEnd = 0.65f;
        [Range(0f, 1f)] public float DrainShare = 0.4f;
        public float DrainTime = 0.12f;
        public float GlowReturnTime = 0.25f;
        [Tooltip("The orb exhales its essence toward the pull above: how much it shrinks, stretches toward the singularity (with a slight squash across) and drifts up in tube units, the shortest shrink (it otherwise lasts one stagger, so the next orb starts as it peaks) and how long it takes to fill back out.")]
        [Range(0f, 0.2f)] public float ExhaleShrink = 0.1f;
        [Range(0f, 0.2f)] public float ExhaleStretch = 0.08f;
        public float ExhaleDrift = 0.07f;
        public float ExhaleMinTime = 0.06f;
        public float ExhaleRecover = 0.18f;
        [Tooltip("The essence: a ghostly copy of the orb's inner glow that brightens the orb for this share of its travel while rising out of its top, then drifts off; start and end size over the ball's, stretch at full speed, core share, colour lift, alpha, alpha left on arrival and rise in tube units.")]
        [Range(0.05f, 0.6f)] public float SoulPeel = 0.3f;
        public float SoulStartSize = 0.3f;
        public float SoulSize = 0.6f;
        public float SoulEndSize = 0.35f;
        public float SoulStretch = 0.9f;
        [Range(0f, 1f)] public float SoulCore = 0.6f;
        [Range(0f, 1f)] public float SoulLift = 0.35f;
        [Range(0f, 1f)] public float SoulAlpha = 0.75f;
        [Range(0f, 1f)] public float SoulEndAlpha = 0.3f;
        public float SoulRise = 0.45f;
        [Tooltip("The singularity inhales each essence: a quick contraction just before it arrives, then a pulse; pulse size and time, contraction size and lead, its growth once all are in, and how much the photon ring tightens once all are in.")]
        public float SwallowPulse = 0.16f;
        public float SwallowTime = 0.12f;
        public float SwallowGrowth = 0.12f;
        public float SwallowContract = 0.12f;
        public float SwallowLead = 0.06f;
        [Range(0f, 0.5f)] public float PhotonTighten = 0.15f;
        [Tooltip("Completion beat when the cap seats: one thin ring from the gem (radius from and to, time, line width from and to).")]
        public float HorizonFrom = 0.25f;
        public float HorizonTo = 0.9f;
        public float HorizonDuration = 0.4f;
        public float HorizonLineFrom = 0.05f;
        public float HorizonLineTo = 0.012f;
        [Tooltip("Faint lensing ripple down the glass after the cap seats: delay, time, radius, alpha and line width.")]
        public float RippleDelay = 0.1f;
        public float RippleDuration = 0.5f;
        public float RippleRadius = 0.8f;
        [Range(0f, 1f)] public float RippleAlpha = 0.28f;
        public float RippleLine = 0.05f;
        [Tooltip("Soft residual glow and glint on the gem once the cap settles: glow size in tube widths, strength, glint size and time.")]
        public float ResidualSize = 0.6f;
        [Range(0f, 1f)] public float ResidualStrength = 0.45f;
        public float GlintSize = 0.45f;
        public float GlintTime = 0.35f;

        // When ball index (bottom first) of count starts draining; the stagger shrinks so the last one still leaves by DrainEnd.
        public float DrainStart(int index, int count) => WispStart + DrainStagger(count) * index;

        // The beat between orbs; it shrinks so the last one still leaves by DrainEnd.
        public float DrainStagger(int count)
        {
            var room = Mathf.Max(0f, DrainEnd - WispStart - WispTime);
            return count > 1 ? Mathf.Min(WispStagger, room / (count - 1)) : 0f;
        }

        // An orb's shrink lasts one beat, so the next orb starts as it peaks.
        public float ExhaleTime(int count) => Mathf.Max(ExhaleMinTime, DrainStagger(count));
    }
}
