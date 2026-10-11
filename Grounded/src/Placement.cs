using System.Collections.Generic;
using UnityEngine;

namespace Grounded
{
    /// <summary>Where Grounded aims the game instead: what PieceRayTest hands back to UpdatePlacementGhost.</summary>
    internal sealed class Aim
    {
        internal Vector3 Point;
        internal Vector3 Normal;
        internal Piece Piece;
        internal Heightmap Heightmap;
        internal Collider WaterSurface;
    }

    /// <summary>
    /// The work of each frame the key works and a piece is in hand (the card A74's design; the research's section 4):
    /// cast the game's own aiming ray again, past every listed rug; if the first thing behind them is a listed floor,
    /// aim at that floor's top straight under the spot the cursor points at, as low as the settings say and a step above
    /// any rug already lying there. The game then places, snaps, checks and tints the ghost from there as always, and the
    /// click places it there too (TryPlacePiece positions the ghost through the same ray first). Nothing is sent to
    /// anyone: a rug placed this way is an ordinary piece.
    /// </summary>
    internal static class Placement
    {
        /// <summary>How far the game's aiming ray reaches (Player.PieceRayTest); Grounded's own ray stops sooner, past
        /// the reach of the eyes from the camera.</summary>
        private const float RayLength = 50f;

        /// <summary>A hit surface faces up enough to look straight down onto it. Below this (a floor's side or underside)
        /// the floor is aimed at as it was hit, and the game judges it as it would.</summary>
        private const float FacesUp = 0.1f;

        /// <summary>The game refuses a rug on a surface whose normal points up less than this (Piece.m_notOnTiltingSurface,
        /// Player.UpdatePlacementGhost): a rug's corner only looks for floors at least this level.</summary>
        private const float Level = 0.8f;

        /// <summary>How far above the floor found under the cursor a corner looks down from, and how far down.</summary>
        private const float CornerAbove = 0.3f;

        private const float CornerDepth = 0.6f;

        /// <summary>A corner is looked for this far inside the rug's edge, so a wall it only touches can't count.</summary>
        private const float CornerInset = 0.05f;

        /// <summary>The rugs lying under a new one are found by their colliders in a box this much smaller than its
        /// footprint on each side, so a rug that only touches its edge doesn't count.</summary>
        private const float OverlapInset = 0.02f;

        /// <summary>How far below and above the new rug's drawing's bottom the rugs under it are looked for.</summary>
        private const float SearchBelow = 0.25f;

        private const float SearchAbove = 0.35f;

        private static readonly RaycastHit[] hits = new RaycastHit[64];
        private static readonly RaycastHit[] cornerHits = new RaycastHit[32];
        private static readonly Collider[] overlaps = new Collider[128];
        private static readonly List<HitKind> kinds = new List<HitKind>(64);
        private static readonly List<float> bottoms = new List<float>();
        private static readonly HashSet<int> seen = new HashSet<int>();
        private static readonly NearestFirst nearestFirst = new NearestFirst();

        private static readonly KindRules rules = new KindRules
        {
            Rugs = new NameList(() => Settings.Rugs?.Value),
            Floors = new NameList(() => Settings.Floors?.Value),
        };

        /// <summary>What a piece is to Grounded, as the settings stand.</summary>
        internal static PieceKind KindOf(Piece piece)
        {
            rules.RugGroupCounts = Settings.RugGroup.Value;
            rules.FloorTagCounts = Settings.FloorCategory.Value;
            rules.PlansCount = Settings.PlansCount.Value;
            return Kinds.Classify(piece.gameObject.name, piece.m_comfortGroup == Piece.ComfortGroup.Carpet,
                                  (piece.m_usage & Piece.UsageTagFlags.Floor) != 0, rules);
        }

        /// <summary>
        /// Where to aim instead of <paramref name="gamePoint"/>, the point the game's own ray found; null to leave the
        /// game's own aim as it is.
        /// </summary>
        internal static Aim Find(Player player, bool water, Vector3 gamePoint)
        {
            GameObject ghost = GameHooks.PlacementGhost(player);
            if (ghost == null)
            {
                return null;
            }
            Piece inHand = ghost.GetComponent<Piece>();
            if (inHand == null)
            {
                return null;
            }
            bool rugInHand = KindOf(inHand) == PieceKind.Rug;
            if (!rugInHand && (Settings.Pieces.Value != WhichPieces.AnyPiece || !HammerInHand(player)))
            {
                return null;
            }
            GameCamera camera = GameCamera.instance;
            if (camera == null)
            {
                return null;
            }

            // The game's own ray (PieceRayTest): from the camera, the same layers, each hit judged as the game judges its
            // one (no body of its own). It needn't go farther than the eyes can reach from where the camera is: the game
            // places nothing beyond its reach of the eyes, and Grounded judges that on the point it hands back.
            int mask = GameHooks.RayMask(player, water);
            Transform view = camera.transform;
            float reach = player.m_maxPlaceDistance + inHand.m_extraPlacementDistance;
            Vector3 eye = player.m_eye != null ? player.m_eye.position : view.position;
            float length = Mathf.Min(RayLength, Vector3.Distance(view.position, eye) + reach + 0.5f);
            int count = Physics.RaycastNonAlloc(view.position, view.forward, hits, length, mask);
            if (count <= 0)
            {
                return null;
            }
            System.Array.Sort(hits, 0, count, nearestFirst);
            kinds.Clear();
            for (int i = 0; i < count; i++)
            {
                kinds.Add(KindOf(hits[i]));
            }
            Choice choice = HitWalk.Choose(kinds, Settings.GroundIsFloor.Value, Settings.WithoutFloor.Value);
            if (choice.Outcome == Outcome.GameOwn)
            {
                return null;
            }
            RaycastHit hit = hits[choice.Index];
            Aim aim = choice.Outcome == Outcome.Behind
                ? AsHit(hit)
                : OnFloor(player, ghost, inHand, rugInHand, hit, kinds[choice.Index] == HitKind.Ground, gamePoint, mask);
            // Within the game's reach of the eyes (its own rule, PieceRayTest), judged where the piece will go.
            return aim != null && Vector3.Distance(eye, aim.Point) < reach ? aim : null;
        }

        /// <summary>The hammer is in hand ("Any piece" is for the hammer's pieces only, never the hoe's, the cultivator's
        /// or the serving tray's), by the game's own test (Player's "$item_hammer").</summary>
        private static bool HammerInHand(Player player)
        {
            ItemDrop.ItemData item = player.RightItem;
            return item != null && item.m_shared != null && item.m_shared.m_name == "$item_hammer";
        }

        /// <summary>One hit of the ray, as Grounded sees it.</summary>
        private static HitKind KindOf(RaycastHit hit)
        {
            Collider collider = hit.collider;
            if (collider == null || collider.attachedRigidbody != null)
            {
                return HitKind.Skip;
            }
            if (collider.gameObject.layer == GameHooks.WaterLayer)
            {
                return HitKind.Other;
            }
            if (collider.GetComponent<Heightmap>() != null)
            {
                return HitKind.Ground;
            }
            Piece piece = collider.GetComponentInParent<Piece>();
            if (piece == null)
            {
                return HitKind.Other;
            }
            switch (KindOf(piece))
            {
                case PieceKind.Rug:
                    return HitKind.Rug;
                case PieceKind.Floor:
                    return HitKind.Floor;
                default:
                    return HitKind.Other;
            }
        }

        /// <summary>Aim at what the ray hit, as the game's ray would have reported it.</summary>
        private static Aim AsHit(RaycastHit hit) => new Aim
        {
            Point = hit.point,
            Normal = hit.normal,
            Piece = hit.collider.GetComponentInParent<Piece>(),
            Heightmap = hit.collider.GetComponent<Heightmap>(),
            WaterSurface = hit.collider.gameObject.layer == GameHooks.WaterLayer ? hit.collider : null,
        };

        private static Aim OnFloor(Player player, GameObject ghost, Piece inHand, bool rugInHand, RaycastHit hit, bool ground,
                                   Vector3 gamePoint, int mask)
        {
            // The floor's top straight under the spot the cursor points at: the game's own point, which may be on top of a
            // rug, looked at straight down onto this floor's collider alone (nothing above it can get in the way). If the
            // look down misses it (the cursor past its edge), the floor where the ray met it. If the floor was met on its
            // side or underside, the game's own aim stands: a rug can't lie there, and Grounded never makes a placement
            // the game allows into one it refuses.
            Collider floor = hit.collider;
            if (hit.normal.y <= FacesUp)
            {
                return null;
            }
            Vector3 top = hit.point;
            Vector3 normal = hit.normal;
            float startY = Mathf.Max(gamePoint.y, hit.point.y) + 0.05f;
            var down = new Ray(new Vector3(gamePoint.x, startY, gamePoint.z), Vector3.down);
            if (floor.Raycast(down, out RaycastHit under, startY - hit.point.y + 1f) && under.normal.y > FacesUp)
            {
                top = under.point;
                normal = under.normal;
            }

            Piece floorPiece = ground ? null : floor.GetComponentInParent<Piece>();
            float gameTop = top.y;
            float visibleTop = ground ? gameTop : Heights.VisibleTop(gameTop, Drawn.Top(floorPiece));
            float yaw = GameHooks.PlaceYaw(player, ghost);
            Footprint footprint = Drawn.FootprintOf(ghost);

            if (Settings.Look.Value == LookUnder.WholeRug && !footprint.IsEmpty)
            {
                float[] corners = footprint.Corners(yaw, CornerInset);
                for (int i = 0; i < corners.Length; i += 2)
                {
                    var from = new Vector3(top.x + corners[i], top.y + CornerAbove, top.z + corners[i + 1]);
                    if (CornerFloor(from, mask, out float cornerTop, out float cornerVisible))
                    {
                        gameTop = Mathf.Max(gameTop, cornerTop);
                        visibleTop = Mathf.Max(visibleTop, cornerVisible);
                    }
                }
            }

            HowLow mode = Settings.Height.Value;
            float lowerBy = Settings.LowerBy.Value / 100f;
            float y;
            // The game puts a clip-everything piece's centre point on the point aimed at (every rug), and a ground piece's
            // on the ground; any other piece so its collider's underside touches it (Player.UpdatePlacementGhost).
            bool centreOnPoint = inHand.m_clipEverything || ((inHand.m_groundPiece || inHand.m_clipGround) && ground);
            if (centreOnPoint)
            {
                float drawnMin = footprint.IsEmpty ? 0f : footprint.MinY;
                y = Heights.BasePivot(mode, gameTop, visibleTop, drawnMin, lowerBy, Settings.KeepPickable.Value, footprint.PickTop);
                float step = Settings.Step.Value / 100f;
                if (rugInHand && step > 0f && !footprint.IsEmpty)
                {
                    float bottom = y + drawnMin;
                    RugsUnder(new Vector3(top.x, bottom, top.z), yaw, footprint, mask);
                    y = Heights.Stack(bottom, bottoms, step, Settings.Overlapping.Value) - drawnMin;
                }
            }
            else
            {
                y = Heights.BaseSurface(mode, gameTop, visibleTop, lowerBy);
            }

            return new Aim
            {
                Point = new Vector3(top.x, y, top.z),
                Normal = normal,
                Piece = floorPiece,
                Heightmap = ground ? floor.GetComponent<Heightmap>() : null,
                WaterSurface = null,
            };
        }

        /// <summary>
        /// The floor under one corner of the rug: looking straight down from <paramref name="from"/>, past the rugs, the
        /// first thing met decides; only a listed floor (or the ground, where it counts) facing up enough for a rug gives a
        /// top. False otherwise.
        /// </summary>
        private static bool CornerFloor(Vector3 from, int mask, out float top, out float visible)
        {
            top = 0f;
            visible = 0f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, cornerHits, CornerDepth, mask);
            System.Array.Sort(cornerHits, 0, count, nearestFirst);
            bool groundCounts = Settings.GroundIsFloor.Value;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = cornerHits[i];
                Collider collider = hit.collider;
                if (collider == null || collider.attachedRigidbody != null)
                {
                    continue;
                }
                bool level = hit.normal.y >= Level;
                if (collider.GetComponent<Heightmap>() != null)
                {
                    if (!groundCounts || !level)
                    {
                        return false;
                    }
                    top = hit.point.y;
                    visible = top;
                    return true;
                }
                Piece piece = collider.GetComponentInParent<Piece>();
                PieceKind kind = piece != null ? KindOf(piece) : PieceKind.Other;
                if (kind == PieceKind.Rug)
                {
                    continue;
                }
                if (kind != PieceKind.Floor || !level)
                {
                    return false;
                }
                top = hit.point.y;
                visible = Heights.VisibleTop(top, Drawn.Top(piece));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Fills <see cref="bottoms"/> with the bottom of the drawing of every listed rug whose colliders reach into the
        /// new rug's footprint near the height it would lie at (<paramref name="at"/>: under its centre point, at its
        /// drawing's bottom). Heights.Stack then keeps only those it could flicker with.
        /// </summary>
        private static void RugsUnder(Vector3 at, float yaw, Footprint footprint, int mask)
        {
            bottoms.Clear();
            seen.Clear();
            footprint.Middle(yaw, out float middleX, out float middleZ);
            // Well around it: a rug's colliders lie a few centimetres around its centre point, which is near the bottom of
            // its drawing, and a pile of rugs a step apart can stand some way up.
            float low = at.y - SearchBelow;
            float high = at.y + SearchAbove;
            var centre = new Vector3(at.x + middleX, (low + high) / 2f, at.z + middleZ);
            var half = new Vector3(footprint.HalfX(OverlapInset), (high - low) / 2f, footprint.HalfZ(OverlapInset));
            int count = Physics.OverlapBoxNonAlloc(centre, half, overlaps, Quaternion.Euler(0f, yaw, 0f), mask);
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                Piece piece = collider != null ? collider.GetComponentInParent<Piece>() : null;
                if (piece == null || !seen.Add(piece.GetInstanceID()) || KindOf(piece) != PieceKind.Rug)
                {
                    continue;
                }
                float bottom = Drawn.Bottom(piece);
                if (!float.IsNaN(bottom))
                {
                    bottoms.Add(bottom);
                }
            }
        }

        /// <summary>Forgets what the last frame found (an undo step).</summary>
        internal static void Forget()
        {
            kinds.Clear();
            bottoms.Clear();
            seen.Clear();
            System.Array.Clear(overlaps, 0, overlaps.Length);
            System.Array.Clear(hits, 0, hits.Length);
            System.Array.Clear(cornerHits, 0, cornerHits.Length);
        }

        private sealed class NearestFirst : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
