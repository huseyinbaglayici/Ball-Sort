using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    [CreateAssetMenu(menuName = "Project/Ball Animation Config", fileName = "BallAnimationConfig")]
    public sealed class BallAnimationConfig : ScriptableObject
    {
        [Header("Selection wave")]
        [Tooltip("How far every selected ball rises from its slot, in slot heights. The same for a full tube and a single ball at the bottom, so a selection always reads the same. The balls keep their stack spacing.")]
        [Range(0f, 2f)] public float LiftHeight = 1f;
        [Tooltip("The selected group always rises to one fixed spot at the mouth instead of by LiftHeight from its slots. The top ball ends where a full tube's top ball lifts to (LiftHeight above the top slot), wherever the stack ends; the group keeps its stack spacing below it. Off = the fixed short lift.")]
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
        [Tooltip("Follow-through: the balls first rise this much further, in slot heights, then settle back down to LiftHeight over LiftSettleDuration. 0 = no overshoot.")]
        [Range(0f, 1f)] public float LiftOvershoot = 0f;
        [Tooltip("Time to settle back from the overshoot into the mouth.")]
        public float LiftSettleDuration = 0.08f;
        [Tooltip("While it settles back into the mouth the ball gives this small squash (area kept), peaking halfway.")]
        public Vector2 LiftSettleSquash = new(1.05f, 0.95f);
        [Tooltip("On the tap, the balls that stay in the tube give one tiny up-and-down. Height in board units and time.")]
        public float TapBob = 0.04f;
        public float TapBobDuration = 0.16f;
        [Tooltip("While selected, the lifted balls float up and down by this much (board units), so the selection stays alive while the player decides. 0 = still.")]
        public float HoverHeight = 0.03f;
        [Tooltip("One full float up and down, in seconds.")]
        public float HoverPeriod = 0.6f;

        [Header("Warp (the jump between tubes)")]
        [Tooltip("Time from the second tap until the ball is all in the source ring (so all out of the target ring), on an ease-in 'toward' curve: no anticipation, it sets off at once and is pulled in faster and faster. Short, so the whole move (crossing, drop into the tube and landing) reads as one quick motion. The same from any depth: a ball deep in its tube travels faster, not longer.")]
        public float WarpCross = 0.26f;
        [Tooltip("How many times as tall the ball is when fully inside a portal (area kept, so it gets thinner).")]
        public float VanishStretch = 1.08f;
        [Tooltip("Gap between a tube's mouth and the bottom edge of the portal ring over it, in board units, at both ends of a move. Small, so the ring sits on the mouth like a cap and the ball goes straight from the tube into the ring.")]
        public float PortalHeight = 0.1f;
        [Tooltip("The portal, a thick ring 1 unit wide seen from above like the tubes (ArtSource/Hole/make_hole.py), pivoted on the center of its face. Back is the face and the void through the opening, Front the near half and the side wall under it. The ring a ball goes into is turned upside down (seen from below, its opening toward the tube) and draws under the ball: the ball rises over its rim and lit inner wall into the black. The one it comes out of faces up and draws over the ball, which comes down from behind its side wall.")]
        public Sprite PortalBack;
        public Sprite PortalFront;
        [Tooltip("Tint of the portal art; white keeps its colors. Its alpha fades with the portal.")]
        public Color PortalColor = Color.white;
        [Tooltip("Portal width, as a multiple of the ball diameter.")]
        public float PortalWidth = 1.6f;
        [Tooltip("Time for a portal to open like an iris: a flat slit that widens, then opens up with a small overshoot. Both portals open together on the target tap, as the balls set off; the selection opens none.")]
        public float PortalOpen = 0.12f;
        [Tooltip("Time for a portal to close: it swells a little and collapses into its center. It starts once the last ball of the move is through.")]
        public float PortalClose = 0.14f;
        [Tooltip("The target portal starts closing this long after the source one, so the door shuts behind the ball and the eye ends on the target. 2-4 frames; much more and they read as two separate events.")]
        public float PortalCloseLag = 0.05f;
        [Tooltip("The ring art's outer half width, as a share of its width (make_hole.py: 0.5 minus the padding). With the flatness it sets how far the ring reaches under its center, so it sits just over the mouth.")]
        public float PortalRim = 0.49f;
        [Tooltip("Height of the ring art's side wall under its face, as a share of its width (make_hole.py THICK). With the rim and the flatness it sets the ring's lowest edge: where it sits over the mouth and where it splits the balls.")]
        public float PortalWall = 0.085f;

        [Header("Narrow mouth and snap")]
        [Tooltip("Out of the target portal the ball keeps falling, straight through the mouth into its slot, starting at the speed it came out with. A deep slot keeps speeding it up, like gravity; a near one slows it down, but it always hits the slot with at least this share of its exit speed, and the snap takes the impact. 0 = it may ease to a stop.")]
        [Range(0f, 1f)] public float ExitKeep = 0.35f;
        [Tooltip("Ball scale at its tightest in the mouth, on the way in and out. Keep x times y near 1 so the ball keeps its volume, and y under TubeArt.MaxStackStretch so a stretched ball never reaches its neighbour.")]
        public Vector2 NeckSqueeze = new(0.985f, 1.015f);
        [Tooltip("The hard stop in the slot: hit pose, linear hit time, and an eased release with no overshoot.")]
        public Vector2 SnapSquash = new(1.16f, 0.82f);
        [Tooltip("Long enough that the hit pose is always drawn for at least 2 frames.")]
        public float SnapHit = 0.04f;
        public float SnapRecover = 0.07f;

        [Header("Motion smear (keeps fast moves continuous)")]
        [Tooltip("Each frame the ball stretches back over the distance it just travelled, times this, so there is never a gap between two frames for the eye to jump. 1 = it covers the whole step.")]
        public float SmearAmount = 1f;
        [Tooltip("Longest the smeared ball may get, as a multiple of its diameter.")]
        public float MaxSmear = 1.15f;
        [Tooltip("How much thinner the ball gets as it stretches (0 = keeps its width).")]
        [Range(0f, 0.5f)] public float SmearThin = 0.12f;

        [Header("Drop into the slot")]
        [Tooltip("The entry window of a move, from the first ball starting into the target mouth to the last ball in its slot. The same for any number of balls, so a move never grows with the group: more balls share it. A single ball only uses its own entry (EntryShare of it).")]
        public float EntryWindow = 0.25f;
        [Tooltip("Each ball's own entry, as a share of the window, for any number of balls; with several balls the rest is spread as the stagger between them, so they land one after another and the last one lands at the end of the window.")]
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
        [Tooltip("Picked: the tube presses down (wide and short, and this far down) as if pushing the balls up, then releases. Together they last as long as the lift.")]
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
        [Tooltip("With several balls in one move, each later ball's launch, gulp and impact is this share of the one before; the first ball reacts hardest.")]
        [Range(0f, 1f)] public float ReactionFalloff = 0.6f;

        [Header("Anticipation and follow-through")]
        [Tooltip("Before a selected ball rises it presses down a little (wider, shorter). Taken out of the lift time, so the lift still ends on time.")]
        public Vector2 AnticipationSquash = new(1.06f, 0.94f);
        public float AnticipationDuration = 0.02f;

        [Header("Impact wave on the balls below")]
        [Tooltip("When a ball lands, each ball under it plays a small copy of the tube squash (TubeSquash, TubeStretch and their times), top to bottom. Share of the full squash for the ball right below.")]
        [Range(0f, 1f)] public float RippleStrength = 0.6f;
        [Tooltip("Each ball further down gets this share of the one above, so the wave dies out toward the bottom.")]
        [Range(0f, 1f)] public float RippleFalloff = 0.6f;
        [Tooltip("Delay between one ball of the wave and the next one down.")]
        public float RippleStep = 0.04f;

        [Header("Highlight")]
        [Tooltip("Fade-in and fade-out times. Every selected ball lights up at once, holds steady while selected, and goes out almost at once.")]
        public float HighlightInDuration = 0.12f;
        public float HighlightOutDuration = 0.06f;
        [Tooltip("How far the thin bright rim on each selected ball's edge reaches past the ball, relative to the ball.")]
        public float ContourScale = 1.04f;
        [Tooltip("Multiplies the ball's own color, brought to full brightness. White = the ball's color as is.")]
        public Color ContourColor = new(1f, 1f, 1f, 1f);
        [Tooltip("How far the rim goes from the ball's color toward white: a light accent in the ball's hue.")]
        [Range(0f, 1f)] public float ContourWhiten = 0.55f;
        [Tooltip("Outer diameter of the soft light around each selected ball, relative to the ball. It is full at the ball's edge and fades out to here. It also sets the lifted spacing: the selected balls stand apart so their lights only meet at the faint tails (smaller = tighter group).")]
        public float GlowScale = 1.18f;
        [Tooltip("Multiplies the ball's own color, brought to full brightness. White = the ball's color as is.")]
        public Color GlowColor = new(1f, 1f, 1f, 0.9f);
        [Tooltip("How far the light goes from the ball's color toward white: a light tint of the ball's hue.")]
        [Range(0f, 1f)] public float GlowWhiten = 0.4f;

        [Header("Ball at rest")]
        [Tooltip("Strength of the orb art's own halo around every ball. Resting balls are only a few pixels apart, narrower than the halo, so any value above 0 lets neighbours' halos touch and blurs the ball edges; 0 keeps every ball's edge crisp against the tube.")]
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
        [Tooltip("After the slam the balls hop bottom to top, each landing with a soft squash: delay, height, hop time, stagger and squash share.")]
        public float BallWaveDelay = 0.06f;
        public float BallWaveHop = 0.1f;
        public float BallWaveDuration = 0.16f;
        public float BallWaveStagger = 0.04f;
        [Range(0f, 1f)] public float BallWaveLand = 0.5f;
        [Tooltip("When the last tube completes, the win popup waits this long so the completion plays out.")]
        public float WinPopupDelay = 1.1f;
        [Tooltip("As the cap lands, an additive glow around the tube flashes up in this color and fades out.")]
        public Color CompleteGlowColor = new(1f, 0.858f, 0.179f, 0.85f);
        public float CompleteGlowIn = 0.12f;
        public float CompleteGlowHold = 0.1f;
        public float CompleteGlowOut = 0.6f;

        [Header("Result")]
        [Tooltip("The orbital burst before the win panel and the small contraction when the fail panel opens.")]
        public ResultFxSettings Result = new();

        [Header("Boosters")]
        [Tooltip("Undo's cap-off, the extra tube's descent and the Free Move target hints.")]
        public BoosterFxSettings Boosters = new();

        [Header("Hidden ball")]
        [Tooltip("Before it breaks the shell trembles, harder toward the end: time, peak scale, peak tilt in degrees and peak side shift in board units.")]
        public float HiddenShakeDuration = 0.56f;
        public float HiddenShakeScale = 1.06f;
        public float HiddenShakeDegrees = 6f;
        public float HiddenShakeShift = 4f / 190f;
        [Tooltip("The shell is swapped for its break art, which grows to this scale while it fades out.")]
        public float HiddenBreakDuration = 0.64f;
        public float HiddenBreakScale = 1.45f;
        [Tooltip("The real orb pops in under the break this long after the break starts, from this scale.")]
        public float HiddenOrbDelay = 0.04f;
        public float HiddenOrbDuration = 0.48f;
        public float HiddenOrbFrom = 0.55f;

        [Header("Frozen tube")]
        [Tooltip("On-screen width of the needed color's orb on the ice badge, in tube units (the tube is about 1.8 wide).")]
        public float FrozenBadgeOrbDiameter = 0.36f;
        [Tooltip("When it melts, the ice first wobbles like jelly: time and scale amount, fading out.")]
        public float MeltWobbleDuration = 0.8f;
        public float MeltWobble = 0.012f;
        public float MeltWobbleCycles = 4f;
        [Tooltip("The ice fades to MeltHalfAlpha over MeltDuration, then out over MeltOutDuration.")]
        public float MeltDuration = 1.5f;
        [Range(0f, 1f)] public float MeltHalfAlpha = 0.5f;
        public float MeltOutDuration = 0.25f;
        [Tooltip("The melt art shows quickly as the melt starts, then fades out slowly in place.")]
        public float MeltShardIn = 0.25f;
        public float MeltShardOut = 2.4f;
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

        [Header("Fail")]
        [Tooltip("The fail board stays readable this long after its last meaningful change before the panel opens.")]
        public float FailReadableWindow = 1f;

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
        [Tooltip("When a group drops back into its own tube, its balls land this far apart, bottom ball first, so each landing is heard on its own.")]
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
