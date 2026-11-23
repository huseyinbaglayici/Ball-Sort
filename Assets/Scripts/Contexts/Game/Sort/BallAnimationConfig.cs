using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    [CreateAssetMenu(menuName = "Project/Ball Animation Config", fileName = "BallAnimationConfig")]
    public sealed class BallAnimationConfig : ScriptableObject
    {
        [Header("Selection wave")]
        [Tooltip("How far every selected ball rises from its slot, in slot heights, whatever the tube's fill.")]
        [Range(0f, 2f)] public float LiftHeight = 1f;
        [Tooltip("Lifts the group to one fixed spot at the mouth, as a full tube would; off = a short lift from its slots.")]
        public bool LiftToMouth;
        [Tooltip("Lift speed in board units per second; the lift time is distance / speed within the limits below.")]
        public float LiftSpeed = 30f;
        [Tooltip("Shortest lift time.")]
        public float LiftMinDuration = 0.09f;
        [Tooltip("Longest lift time; only a ball lifted from deep in the tube reaches it.")]
        public float LiftMaxDuration = 0.12f;
        public AnimationCurve LiftCurve = Curve(0f, 0f, 2f, 2f, 1f, 1f, 0f, 0f);
        [Tooltip("Delay between ball i and ball i+1, top ball first. Small, so the balls below follow in line. The last entry repeats.")]
        public float[] WaveStagger = { 0.025f };
        [Tooltip("Lowering speed on deselect, in board units per second.")]
        public float LowerSpeed = 40f;
        [Tooltip("Shortest and longest lowering time.")]
        public float LowerMinDuration = 0.09f;
        public float LowerMaxDuration = 0.12f;
        public AnimationCurve LowerCurve = Curve(0f, 0f, 0f, 0f, 1f, 1f, 0f, 0f);
        [Tooltip("Overshoot past LiftHeight in slot heights, settling back over LiftSettleDuration; 0 = none.")]
        [Range(0f, 1f)] public float LiftOvershoot = 0f;
        [Tooltip("Time to settle back from the overshoot into the mouth.")]
        public float LiftSettleDuration = 0.08f;
        [Tooltip("While it settles back into the mouth the ball gives this small squash (area kept), peaking halfway.")]
        public Vector2 LiftSettleSquash = new(1.05f, 0.95f);
        [Tooltip("On the tap, the balls that stay in the tube give one tiny up-and-down. Height in board units and time.")]
        public float TapBob = 0.04f;
        public float TapBobDuration = 0.16f;
        [Tooltip("Float of the lifted balls while selected, in board units; 0 = still.")]
        public float HoverHeight = 0.03f;
        [Tooltip("One full float up and down, in seconds.")]
        public float HoverPeriod = 0.6f;

        [Header("Warp (the jump between tubes)")]
        [Tooltip("Time from the second tap to the ball fully in the source ring, the same from any depth.")]
        public float WarpCross = 0.26f;
        [Tooltip("How many times as tall the ball is when fully inside a portal (area kept, so it gets thinner).")]
        public float VanishStretch = 1.08f;
        [Tooltip("Gap from a tube's mouth to the portal ring's lower edge, in board units, so the ring sits on the mouth like a cap.")]
        public float PortalHeight = 0.1f;
        [Tooltip("Portal ring art, 1 unit wide: Back is its face and void, Front its near half; the entry ring is flipped.")]
        public Sprite PortalBack;
        public Sprite PortalFront;
        [Tooltip("Tint of the portal art; white keeps its colors. Its alpha fades with the portal.")]
        public Color PortalColor = Color.white;
        [Tooltip("Portal width, as a multiple of the ball diameter.")]
        public float PortalWidth = 1.6f;
        [Tooltip("Iris-like portal open time; both portals open together on the target tap.")]
        public float PortalOpen = 0.12f;
        [Tooltip("Portal close time: it swells a little and collapses, starting once the move's last ball is through.")]
        public float PortalClose = 0.14f;
        [Tooltip("Target portal close delay after the source one, so the eye ends on the target; keep to 2-4 frames.")]
        public float PortalCloseLag = 0.05f;
        [Tooltip("Ring art's outer half width as a share of its width; with the flatness it sets where the ring sits on the mouth.")]
        public float PortalRim = 0.49f;
        [Tooltip("Ring side wall height as a share of its width; sets where the ring sits over the mouth and where it splits the balls.")]
        public float PortalWall = 0.085f;

        [Header("Narrow mouth and snap")]
        [Tooltip("Least share of its exit speed the ball keeps when it hits its slot after the portal (0 = may ease to a stop).")]
        [Range(0f, 1f)] public float ExitKeep = 0.35f;
        [Tooltip("Ball scale at its tightest in the mouth; keep x*y near 1 and y under TubeArt.MaxStackStretch.")]
        public Vector2 NeckSqueeze = new(0.985f, 1.015f);
        [Tooltip("The hard stop in the slot: hit pose, linear hit time, and an eased release with no overshoot.")]
        public Vector2 SnapSquash = new(1.16f, 0.82f);
        [Tooltip("Long enough that the hit pose is always drawn for at least 2 frames.")]
        public float SnapHit = 0.04f;
        public float SnapRecover = 0.07f;

        [Header("Motion smear (keeps fast moves continuous)")]
        [Tooltip("Share of each frame's travel the ball stretches back over, so no gap shows between frames; 1 = the whole step.")]
        public float SmearAmount = 1f;
        [Tooltip("Longest the smeared ball may get, as a multiple of its diameter.")]
        public float MaxSmear = 1.15f;
        [Tooltip("How much thinner the ball gets as it stretches (0 = keeps its width).")]
        [Range(0f, 0.5f)] public float SmearThin = 0.12f;

        [Header("Drop into the slot")]
        [Tooltip("From the first ball entering the target mouth to the last in its slot; the same for any group size.")]
        public float EntryWindow = 0.25f;
        [Tooltip("Each ball's entry as a share of the window; the rest is the stagger, so the last ball lands at the window's end.")]
        [Range(0.2f, 1f)] public float EntryShare = 0.55f;

        [Header("Return after a partial transfer")]
        [Tooltip("In board units per second, with a shortest time.")]
        public float ReturnSpeed = 106.8f;
        public float ReturnMinDuration = 0.27f;
        public AnimationCurve ReturnCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Landing settle")]
        [Tooltip("Delay before the squash; a ball squashes on contact.")]
        public float SettleDelay;
        [Tooltip("Squash, stretch and recover poses and times of a landing ball.")]
        public Vector2 SquashScale = new(1.12f, 0.88f);
        public float SquashDuration = 0.05f;
        public AnimationCurve SquashCurve = Curve(0f, 0f, 0f, 0f, 1f, 1f, 2f, 2f);
        public Vector2 StretchScale = new(0.96f, 1.05f);
        public float StretchDuration = 0.08f;
        public AnimationCurve StretchCurve = Curve(0f, 0f, 0f, 0f, 1f, 1f, 0f, 0f);
        public float RecoverDuration = 0.16f;
        public AnimationCurve RecoverCurve = Curve(0f, 0f, 0f, 0f, 1f, 1f, 0f, 0f);

        [Header("Ripple shape, played by the balls under a landing ball")]
        public Vector2 TubeSquash = new(1.07f, 0.95f);
        public float TubeSquashDuration = 0.04f;
        public Vector2 TubeStretch = new(0.97f, 1.04f);
        public float TubeStretchDuration = 0.07f;
        public float TubeRecoverDuration = 0.20f;

        [Header("Tube reactions (they add up; the tube art and its resting balls squash from the bottom)")]
        [Tooltip("Picked: the tube presses down (wide, short and this far down), then releases, over the lift time.")]
        public Vector2 SelectPress = new(1.04f, 0.96f);
        public float SelectDrop = 0.03f;
        public float SelectPressDuration = 0.03f;
        public float SelectReleaseDuration = 0.06f;
        [Tooltip("Deselected, or the balls left after a partial move go back: a small catch when they are back in.")]
        public Vector2 CatchSquash = new(1.02f, 0.98f);
        public float CatchInDuration = 0.03f;
        public float CatchOutDuration = 0.08f;
        [Tooltip("Tapped as a target that cannot take the balls: a sideways shake that dies out, in board units.")]
        public float RejectShake = 0.05f;
        public float RejectCycles = 3f;
        public float RejectDuration = 0.2f;
        [Tooltip("Tapped while it cannot be picked (empty, completed or busy): a small press only.")]
        public Vector2 LockedPress = new(1.02f, 0.98f);
        public float LockedDuration = 0.06f;
        [Tooltip("A ball shoots out of the mouth: the tube kicks back down and wide, springs up a little and settles.")]
        public Vector2 RecoilSquash = new(1.04f, 0.96f);
        [Tooltip("How far the kick pushes the tube down, in board units.")]
        public float RecoilDrop = 0.04f;
        public Vector2 RecoilStretch = new(0.98f, 1.03f);
        public float RecoilHit = 0.04f;
        public float RecoilStretchDuration = 0.07f;
        public float RecoilRecover = 0.2f;
        [Tooltip("A ball goes into the mouth: the tube widens by this share for a moment, like a gulp.")]
        public float SwallowWiden = 0.03f;
        public float SwallowIn = 0.02f;
        public float SwallowOut = 0.04f;
        [Tooltip("A ball lands in its slot: the tube takes the hit (wide and short) and recovers.")]
        public Vector2 ImpactSquash = new(1.04f, 0.97f);
        public float ImpactIn = 0.04f;
        public float ImpactOut = 0.12f;
        [Tooltip("In a multi-ball move, each later ball's launch, gulp and impact is this share of the one before.")]
        [Range(0f, 1f)] public float ReactionFalloff = 0.6f;

        [Header("Anticipation and follow-through")]
        [Tooltip("Small press (wider, shorter) before a selected ball rises, taken out of the lift time.")]
        public Vector2 AnticipationSquash = new(1.06f, 0.94f);
        public float AnticipationDuration = 0.02f;

        [Header("Impact wave on the balls below")]
        [Tooltip("Share of the tube squash replayed by the ball right below a landing one; the wave runs top to bottom.")]
        [Range(0f, 1f)] public float RippleStrength = 0.6f;
        [Tooltip("Each ball further down gets this share of the one above, so the wave dies out toward the bottom.")]
        [Range(0f, 1f)] public float RippleFalloff = 0.6f;
        [Tooltip("Delay between one ball of the wave and the next one down.")]
        public float RippleStep = 0.04f;

        [Header("Highlight")]
        [Tooltip("Fade-in and fade-out times; every selected ball lights up at once and goes out almost at once.")]
        public float HighlightInDuration = 0.12f;
        public float HighlightOutDuration = 0.06f;
        [Tooltip("How far the thin bright rim on each selected ball's edge reaches past the ball, relative to the ball.")]
        public float ContourScale = 1.04f;
        [Tooltip("Multiplies the ball's own color, brought to full brightness. White = the ball's color as is.")]
        public Color ContourColor = new(1f, 1f, 1f, 1f);
        [Tooltip("How far the rim goes from the ball's color toward white: a light accent in the ball's hue.")]
        [Range(0f, 1f)] public float ContourWhiten = 0.55f;
        [Tooltip("Outer size of the light round a lifted ball, relative to it; also sets how far lifted balls stand apart.")]
        public float GlowScale = 1.18f;
        [Tooltip("Multiplies the ball's own color, brought to full brightness. White = the ball's color as is.")]
        public Color GlowColor = new(1f, 1f, 1f, 0.9f);
        [Tooltip("How far the light goes from the ball's color toward white: a light tint of the ball's hue.")]
        [Range(0f, 1f)] public float GlowWhiten = 0.4f;

        [Header("Ball at rest")]
        [Tooltip("Orb art's own halo strength; above 0 neighbouring halos touch and soften the edges, 0 keeps them crisp.")]
        [Range(0f, 1f)] public float OrbHaloAlpha;

        [Header("Completed bottle")]
        [Tooltip("Front glass color while the tube is not completed; it is additive, so grey dims it. Completion brings it back to white.")]
        public Color GlassRestTint = new(0.5f, 0.5f, 0.5f, 1f);
        [Tooltip("Time for the front glass to brighten to white when the tube is completed, from the last landing.")]
        public float GlassCompleteBrighten = 0.25f;
        [Tooltip("Charge before the victory hop: the tube twitches and slowly rises for this long after the last landing.")]
        public float CompleteChargeDuration = 0.65f;
        [Tooltip("Twitch during the charge: tilt in degrees each way, time per quarter swing, and how many full swings.")]
        public float CompleteTwitchAngle = 1f;
        public float CompleteTwitchStep = 0.05f;
        public int CompleteTwitchLoops = 3;
        [Tooltip("How far the tube creeps up during the charge, in board units, easing in.")]
        public float CompleteChargeRise = 0.27f;
        public AnimationCurve CompleteChargeCurve = Curve(0f, 0f, 0f, 0f, 1f, 1f, 2f, 2f);
        [Tooltip("Hop height in board units, from rest. The hop starts when the charge ends.")]
        public float CompleteHop = 0.445f;
        public float CompleteRise = 0.28f;
        public float CompleteFall = 0.05f;
        [Tooltip("Landing after the hop: squash, stretch and recover.")]
        public Vector2 CompleteSquash = new(1.07f, 0.95f);
        public float CompleteSquashIn = 0.04f;
        public Vector2 CompleteStretch = new(0.97f, 1.04f);
        public float CompleteStretchDuration = 0.07f;
        public float CompleteSquashOut = 0.2f;
        [Tooltip("The completed cap pops out above the mouth when the charge ends: start and peak heights over its seat, in board units.")]
        public float CapPopStart = 0.74f;
        public float CapPopPeak = 1.58f;
        [Tooltip("Rise to the peak, then the slam down onto the mouth, timed to meet the tube at the top of its hop.")]
        public float CapPopDuration = 0.2f;
        public float CapSlamDuration = 0.1f;
        [Tooltip("The cap appears small and fades in, stretches tall on the way up, squashes on the slam and settles.")]
        public Vector2 CapPopScale = new(0.4f, 0.6f);
        public Vector2 CapStretch = new(0.9f, 1.2f);
        public float CapStretchIn = 0.1f;
        public float CapFadeIn = 0.17f;
        public Vector2 CapSquash = new(1.094f, 0.975f);
        public float CapSquashIn = 0.05f;
        public float CapSettle = 0.07f;

        [Header("Completion finish")]
        [Tooltip("The finish when a tube completes: the singularity, the essences drawn from the orbs, its palette and timing.")]
        public FinishFxSettings Finish = new();
        [Tooltip("Post-slam ball hops, bottom to top: delay, height, hop time, stagger and squash share.")]
        public float BallWaveDelay = 0.06f;
        public float BallWaveHop = 0.1f;
        public float BallWaveDuration = 0.16f;
        public float BallWaveStagger = 0.04f;
        [Range(0f, 1f)] public float BallWaveLand = 0.5f;
        [Tooltip("Wait before the win popup after the last tube completes, so the win beat plays out (see WinFx.PanelAt).")]
        public float WinPopupDelay = 3.05f;
        [Tooltip("As the cap lands, an additive glow around the tube flashes up in this color and fades out.")]
        public Color CompleteGlowColor = new(1f, 0.858f, 0.179f, 0.85f);
        public float CompleteGlowIn = 0.12f;
        public float CompleteGlowHold = 0.1f;
        public float CompleteGlowOut = 0.6f;

        [Header("Result")]
        [Tooltip("The level-won beat over the board: caps, orbit sweep, burst, mascot and ribbon before the win panel.")]
        public WinFxSettings Win = new();
        [Tooltip("The small contraction when the fail panel opens.")]
        public ResultFxSettings Result = new();

        [Header("Boosters")]
        [Tooltip("Undo's cap-off, the extra tube's descent and the Free Move target hints.")]
        public BoosterFxSettings Boosters = new();

        [Header("Hidden ball")]
        [Tooltip("Shell tremble before the break, harder toward the end: time, peak scale, tilt in degrees and side shift.")]
        public float HiddenShakeDuration = 0.56f;
        public float HiddenShakeScale = 1.06f;
        public float HiddenShakeDegrees = 6f;
        public float HiddenShakeShift = 4f / 190f;
        [Tooltip("Charge after the shake: the shell squashes to HiddenSquash as a ring closes between these ball widths.")]
        public float HiddenChargeTime = 0.08f;
        public float HiddenCrackTime = 0.12f;
        public Vector2 HiddenSquash = new(1.1f, 0.88f);
        public float HiddenRingFrom = 1.7f;
        public float HiddenRingTo = 1.05f;
        [Tooltip("At the break the ring widens to this many ball widths and fades.")]
        public float HiddenHorizonTime = 0.22f;
        public float HiddenHorizonTo = 1.9f;
        [Tooltip("The shell's halves part this far on short arcs, drop, turn and grow to this scale while they fade.")]
        public float HiddenBreakDuration = 0.32f;
        public float HiddenBreakScale = 1.1f;
        public float HiddenShardDistance = 0.22f;
        public float HiddenShardDrop = 0.16f;
        public float HiddenShardSpin = 14f;
        [Tooltip("The real orb pops in under the shards this long after the break, from this scale, with this overshoot.")]
        public float HiddenOrbDelay = 0.04f;
        public float HiddenOrbDuration = 0.48f;
        public float HiddenOrbFrom = 0.55f;
        public float HiddenOrbOvershoot = 2.4f;
        [Tooltip("The orb's rim light flares to full and settles over this time as it pops in.")]
        public float HiddenFlareTime = 0.36f;
        [Tooltip("A gold glint on the orb starts at this share of the pop: time and size in ball widths.")]
        [Range(0f, 1f)] public float HiddenSparkAt = 0.5f;
        public float HiddenSparkTime = 0.2f;
        public float HiddenSparkSize = 0.42f;

        [Header("Frozen tube")]
        [Tooltip("On-screen width of the needed color's orb on the ice badge, in tube units (the tube is about 1.8 wide).")]
        public float FrozenBadgeOrbDiameter = 0.36f;
        [Tooltip("When it breaks, the ice first trembles: time and scale amount, fading out.")]
        public float MeltWobbleDuration = 0.3f;
        public float MeltWobble = 0.012f;
        public float MeltWobbleCycles = 3f;
        [Tooltip("Ice cracks out in beats and breaks this long after the start; the tube is usable MeltOutDuration later.")]
        public float MeltDuration = 1.03f;
        public float MeltOutDuration = 0.17f;
        [Tooltip("Crack beats after the tremble, each jumping the cracks in this time and knocking the ice by this scale.")]
        public int FrozenCrackBeats = 3;
        public float FrozenBeatTime = 0.07f;
        [Range(0f, 0.1f)] public float FrozenPunch = 0.025f;
        [Tooltip("Chunks leave this far apart in order from the badge and drift this long; they turn to dust from this share of it.")]
        public float FrozenChunkStagger = 0.063f;
        public float FrozenChunkTime = 1.1f;
        [Range(0f, 1f)] public float FrozenChunkDissolveAt = 0.45f;
        [Tooltip("Chunk drift in low gravity: outward speed, upward kick and gravity, in tube units per second.")]
        public float FrozenChunkSpeed = 0.3f;
        public float FrozenChunkLift = 0.15f;
        public float FrozenChunkGravity = 1.6f;
        [Tooltip("Chunk turn in degrees: in depth (so its sides show) and in the screen; it shrinks to this scale.")]
        public float FrozenChunkTilt = 35f;
        public float FrozenChunkSpin = 22f;
        public float FrozenChunkShrink = 0.9f;
        [Tooltip("Once the ice is gone the balls hop bottom to top: time per ball and delay to the next.")]
        public float MeltHopDuration = 0.64f;
        public float MeltHopStagger = 0.36f;
        [Tooltip("Hop height in board units after a frozen tube or shutter opens.")]
        public float ObstacleHopHeight = 22f / 190f;

        [Header("Shutter")]
        [Tooltip("The padlock swings from its shackle, growing to this angle, while the shutter ticks sideways by ShutterRattle.")]
        public float ShutterShakeDuration = 0.08f;
        public float ShutterShakeDegrees = 12f;
        public float ShutterShakeCycles = 2f;
        public float ShutterRattle = 1.5f / 190f;
        [Tooltip("The opened padlock drops this far, turns and fades, alongside the roll.")]
        public float LockDropDuration = 0.2f;
        public float LockDropDistance = 90f / 190f;
        public float LockDropDegrees = -20f;
        [Tooltip("Pause after the shake, then the curtain rolls up ShutterRollDistance; the tubes are usable when it is up.")]
        public float ShutterRollDelay = 0.02f;
        public float ShutterRollDuration = 0.25f;
        public float ShutterRollDistance = 850f / 190f;
        [Tooltip("Then housing and frame fade out.")]
        public float ShutterFadeDuration = 0.1f;
        public float ShutterHopDuration = 0.56f;
        public float ShutterHopStagger = 0.24f;
        [Header("Shutter open FX")]
        [Tooltip("Charge during the shake: an orbit ring spins in between these padlock widths as the lock squashes to this scale.")]
        public float ShutterFxSnapFrom = 1.4f;
        public float ShutterFxSnapTo = 0.55f;
        public Vector2 ShutterFxSquash = new(1.12f, 0.88f);
        [Tooltip("Release hit: horizon ring time and reach (lock widths), flash size, gold sparks and the frame's scale punch.")]
        public float ShutterFxHorizonTime = 0.22f;
        public float ShutterFxHorizonTo = 1.6f;
        public float ShutterFxFlashSize = 1f;
        public int ShutterFxSparks = 5;
        public Vector2 ShutterFxSparkSpeed = new(3f, 5.5f);
        [Range(0f, 0.1f)] public float ShutterFxPunch = 0.035f;
        [Tooltip("Seam on the curtain's lower edge and violet spill in the opened glass: seam height and alpha, spill alpha.")]
        public float ShutterFxSeamHeight = 0.12f;
        [Range(0f, 1f)] public float ShutterFxSeamAlpha = 0.7f;
        [Range(0f, 1f)] public float ShutterFxSpillAlpha = 0.3f;
        [Tooltip("Once the tubes are usable the spill sinks and the rims flash gold over this time: rim height and peak alpha.")]
        public float ShutterFxLinger = 0.25f;
        public float ShutterFxRimHeight = 0.8f;
        [Range(0f, 1f)] public float ShutterFxRimAlpha = 0.45f;

        [Header("Fail")]
        [Tooltip("The fail board stays readable this long after its last meaningful change before the panel opens.")]
        public float FailReadableWindow = 1.5f;

        // From the key to the tubes being usable (curtain up).
        public float ShutterClearTime => ShutterShakeDuration + ShutterRollDelay + ShutterRollDuration;

        // From a reveal's start to its color fully shown.
        public float HiddenReadableTime => HiddenShakeDuration + HiddenOrbDelay + HiddenOrbDuration;

        // From the explosion's start to the blast fully in.
        public float ExplodeReadableTime => ExplodeShake + ExplodeIn;
        [Tooltip("The used key grows to this scale with a small overshoot, then fades.")]
        public float KeyUseScale = 1.25f;
        public float KeyUsePop = 0.2f;
        public float KeyUseFade = 0.4f;
        [Tooltip("A freed key glides on one eased curve into the padlock; sizes are relative to its mark and the lock.")]
        public float KeyFlyToCentre = 0.26f;
        public float KeyFlyToLock = 0.24f;
        public float KeyFlyCentreScale = 2.3f;
        public float KeyFlyLockScale = 1f;
        [Tooltip("Height of the curve's bends above the key's start and above the keyhole.")]
        public float KeyFlyArc = 2f;
        [Tooltip("The push into the keyhole and the quarter turn that opens the lock.")]
        public float KeyFlyInsertTime = 0.08f;
        public float KeyFlyTurn = 0.1f;

        [Header("Bomb")]
        [Tooltip("A lit bomb's red light fades in, then pulses between BombPulseMin and full; faster on the last BombFastFuse moves.")]
        public float BombLitFadeIn = 0.24f;
        public float BombPulsePeriod = 0.48f;
        public float BombPulseFastPeriod = 0.22f;
        [Range(0f, 1f)] public float BombPulseMin = 0.65f;
        public int BombFastFuse = 2;
        [Tooltip("Defuse: the split art pops from DefuseFrom to 1, holds, then grows to DefuseOutScale while it fades.")]
        public float DefuseIn = 0.2f;
        public float DefuseFrom = 0.92f;
        public float DefuseHold = 0.2f;
        public float DefuseOut = 0.48f;
        public float DefuseOutScale = 1.1f;
        [Tooltip("The orb pops in under it this long after the defuse starts, from DefuseOrbFrom.")]
        public float DefuseOrbDelay = 0.08f;
        public float DefuseOrbDuration = 0.4f;
        public float DefuseOrbFrom = 0.6f;
        [Tooltip("Explosion: the bomb trembles and swells, then the blast grows in, holds while growing a little, and fades out growing.")]
        public float ExplodeShake = 0.64f;
        public float ExplodeShakeScale = 1.08f;
        public float ExplodeShakeDegrees = 8f;
        public float ExplodeIn = 0.2f;
        public float ExplodeFrom = 0.5f;
        public float ExplodePeak = 1.6f;
        public float ExplodeHold = 0.44f;
        public float ExplodeHoldScale = 1.75f;
        public float ExplodeOut = 0.72f;
        public float ExplodeEndScale = 2f;

        [Header("Contacts")]
        [Tooltip("Gap between landings when a group drops back into its own tube, bottom first, so each is heard.")]
        public float ReturnContactStagger = 0.05f;

        public float WaveDelay(int index)
        {
            var delay = 0f;
            for (var i = 0; i < index; i++) delay += At(WaveStagger, i);
            return delay;
        }

        // Same entry time for every ball so a single ball is as brisk; the rest of the window is stagger.
        public void EntryTiming(int count, out float entry, out float stagger)
        {
            entry = EntryWindow * EntryShare;
            stagger = count <= 1 ? 0f : (EntryWindow - entry) / (count - 1);
        }

        public float LiftDuration(float distance) =>
            Mathf.Clamp(distance / Mathf.Max(LiftSpeed, 0.01f), LiftMinDuration, Mathf.Max(LiftMinDuration, LiftMaxDuration));

        public float LowerDuration(float distance) =>
            Mathf.Clamp(distance / Mathf.Max(LowerSpeed, 0.01f), LowerMinDuration, Mathf.Max(LowerMinDuration, LowerMaxDuration));

        public float ReturnDuration(float distance) => Mathf.Max(distance / Mathf.Max(ReturnSpeed, 0.01f), ReturnMinDuration);

        private static float At(float[] values, int index) =>
            values == null || values.Length == 0 ? 0f : values[Mathf.Min(index, values.Length - 1)];

        private static AnimationCurve Curve(float t0, float v0, float in0, float out0, float t1, float v1, float in1, float out1) =>
            new(new Keyframe(t0, v0, in0, out0), new Keyframe(t1, v1, in1, out1));
    }
}
