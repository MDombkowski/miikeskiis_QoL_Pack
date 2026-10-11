// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>
    /// Far Fetched, a mod of his for the server alone (card A70, 2026-10-08), and what it promises this game. On a server
    /// the game hands a chest only to a player within about 64 to 128 m of it (ChestClaims), so a chest farther away
    /// answers no one. Far Fetched makes the server hand such chests to a player on the pack too, up to the smaller of its
    /// own limit and the range he asked for, and only chests his game has loaded. This game asks for that with a hello to
    /// the server (routed message "modprojects.farfetched.hello": protocol, the range he wants, where to reply), once he
    /// is in the world, every ten seconds, and whenever the range he wants changes; Far Fetched replies at once with the
    /// range it grants. A server without it never replies, and nothing changes. While a grant stands, a chest within it
    /// counts as one the server hands him (ChestClaims.OutsideHandOut): a claim waits for that hand-out, every two
    /// seconds, rather than claiming the chest itself. The range he wants is the largest of the modules' ranges that are
    /// on (ChestUser.Reach).
    /// </summary>
    internal static class ServerReach
    {
        internal const string HelloName = "modprojects.farfetched.hello";

        private const string ReplyPrefix = "modprojects.farfetched.reach.";

        /// <summary>The protocol this copy speaks.</summary>
        internal const int Protocol = 1;

        /// <summary>This copy's own reply name, so the copy that asked gets the answer even with two plugins carrying the
        /// service at once.</summary>
        internal static readonly string ReplyName = ReplyPrefix + typeof(ServerReach).Assembly.GetName().Name.ToLowerInvariant();

        /// <summary>How often the hello is said again, in seconds: Far Fetched forgets a player after 90 s without one, and
        /// each reply also tells this game whether Far Fetched is still on there with the same reach (card A69, review 1, M3).</summary>
        internal const float HelloEvery = 10f;

        /// <summary>The least time between two hellos, in seconds, however often the range changes (a slider dragged in the
        /// settings window changes it every frame).</summary>
        internal const float HelloAtMostEvery = 2f;

        /// <summary>How far inside the grant a chest must be to count as kept with him, and how far outside it may be and
        /// still count as one the server may hand him, in metres: the server measures from where this game last told it he
        /// stands, which lags behind him a little (Covers).</summary>
        internal const float Margin = 10f;

        /// <summary>How long every one of his recent positions must have put a chest inside the grant before this game
        /// changes it (SteadilyCovers), in seconds, plus the round trip: the server judges from where this game last told
        /// it he stands (every two seconds), and lets a chest go only after it has been out of his grant for two of its
        /// rounds (Far Fetched), so this game always stops changing a chest before the server can let it go (card A69,
        /// Far Fetched's review, M1).</summary>
        internal const float SteadyFor = 3f;

        /// <summary>How often his position is noted for SteadilyCovers, in seconds.</summary>
        private const float SampleEvery = 0.25f;

        // His reference position as noted lately, oldest first, with when (real time).
        private static readonly List<KeyValuePair<float, Vector3>> Recent = new List<KeyValuePair<float, Vector3>>();
        private static float sampledAt = float.NegativeInfinity;

        // The modules using the service (ChestHooks.Resolve): the range he wants is theirs.
        private static readonly List<ChestUser> Users = new List<ChestUser>();

        private static ZRoutedRpc registeredOn;
        private static bool replying;
        private static float saidAt = float.NegativeInfinity;
        private static float saidReach = -1f;
        private static long saidTo;

        // The last reply: from which server, whether Far Fetched is on there, and the range it grants.
        private static long grantedBy;
        private static bool on;
        private static float granted;
        private static float serverLimit;
        private static bool toldOfGrant;

        /// <summary>A module that uses the service (from ChestHooks.Resolve).</summary>
        internal static void Uses(ChestUser user)
        {
            if (user != null && !Users.Contains(user))
            {
                Users.Add(user);
            }
        }

        /// <summary>The range Far Fetched on this server grants him now, in metres; 0 when it isn't there (or is off, or
        /// hasn't replied yet).</summary>
        internal static float Granted
        {
            get
            {
                ZNet net = ZNet.instance;
                ZNetPeer server = net != null && !net.IsServer() ? net.GetServerPeer() : null;
                return on && server != null && server.m_uid == grantedBy && registeredOn != null && registeredOn == ZRoutedRpc.instance ? granted : 0f;
            }
        }

        /// <summary>
        /// Whether Far Fetched on this server hands him this chest (see the class): only a placed chest, as Far Fetched
        /// counts them (the game's own chest, built as a piece, never a cart's, a ship's or a gravestone: card A69, review
        /// 1, M2), within the grant measured across the ground from where he stands, and in the zones his game has loaded
        /// around him, as Far Fetched itself tests. <paramref name="loose"/>: the grant plus the margin, for "the server
        /// may hand it to him, so don't claim it here"; otherwise the grant less the margin, for "the server keeps it with
        /// him". False whenever there's no grant.
        /// </summary>
        internal static bool Covers(Container chest, bool loose)
        {
            if (chest == null || chest.GetType() != typeof(Container) || chest.GetComponent<Piece>() == null ||
                chest.GetComponent<TombStone>() != null || ChestClaims.OnVehicle(chest))
            {
                return false;
            }
            ZNet net = ZNet.instance;
            return net != null && CoversSpot(ChestFinder.Where(chest), net.GetReferencePosition(), loose ? Margin : -Margin);
        }

        /// <summary>The same as Covers, with no margin either way: whether the grant reaches this placed chest from where he
        /// stands now.</summary>
        internal static bool CoversExactly(Container chest)
        {
            if (chest == null || chest.GetType() != typeof(Container) || chest.GetComponent<Piece>() == null ||
                chest.GetComponent<TombStone>() != null || ChestClaims.OnVehicle(chest))
            {
                return false;
            }
            ZNet net = ZNet.instance;
            return net != null && CoversSpot(ChestFinder.Where(chest), net.GetReferencePosition(), 0f);
        }

        /// <summary>
        /// Whether Far Fetched keeps this chest with him steadily: a placed chest (as Covers), inside the grant (no margin)
        /// from where he stands now and from every position of his noted in the last SteadyFor seconds and a round trip,
        /// with notes covering all that time. For a chest he owns beyond any active area the game gives, where only Far
        /// Fetched keeps it with him: this game changes it only then (ChestClaims.MayChange).
        /// </summary>
        internal static bool SteadilyCovers(Container chest)
        {
            if (!CoversExactly(chest))
            {
                return false;
            }
            ZNet net = ZNet.instance;
            if (net == null || Recent.Count == 0)
            {
                return false;
            }
            Vector3 at = ChestFinder.Where(chest);
            float now = Time.realtimeSinceStartup;
            float span = SteadyFor + ChestClaims.PingSeconds();
            if (now - Recent[0].Key < span || !CoversSpot(at, net.GetReferencePosition(), 0f))
            {
                return false;   // not noted for long enough yet (just arrived), or out of the grant now
            }
            foreach (KeyValuePair<float, Vector3> each in Recent)
            {
                if (now - each.Key <= span && !CoversSpot(at, each.Value, 0f))
                {
                    return false;
                }
            }
            return true;
        }

        // Whether the grant, widened by extra metres (narrowed when negative), reaches this spot from where he stood at
        // centre (see Covers): across the ground, and in the zones his game keeps loaded around that spot.
        private static bool CoversSpot(Vector3 at, Vector3 centre, float extra)
        {
            float granted = Granted;
            float reach = granted + extra;
            if (granted <= 0f)
            {
                return false;
            }
            ZNet net = ZNet.instance;
            if (reach <= 0f || net == null || ZoneSystem.instance == null)
            {
                return false;
            }
            float dx = at.x - centre.x;
            float dz = at.z - centre.z;
            if (dx * dx + dz * dz > reach * reach)
            {
                return false;
            }
            SimulationDistance distance = net.GetSyncedSimulationDistance();
            int near = distance.NearSimulationDistance;
            Vector2s mine = ZoneSystem.GetZone(centre);
            Vector2s its = ZoneSystem.GetZone(at);
            if (distance.IsClassic)
            {
                return Math.Abs(its.x - mine.x) <= near && Math.Abs(its.y - mine.y) <= near;
            }
            return ZoneSystem.instance.ZonesWithinRadius(mine, its, near);
        }

        /// <summary>Every frame, from ChestClaims.Tick: registers for replies on each new connection, and says hello when
        /// due.</summary>
        internal static void Tick()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null)
            {
                return;
            }
            if (rpc != registeredOn)
            {
                Register(rpc);
            }
            ZNet net = ZNet.instance;
            ZNetPeer server = net != null && !net.IsServer() ? net.GetServerPeer() : null;
            if (!replying || server == null || Player.m_localPlayer == null)
            {
                Recent.Clear();
                return;
            }
            Note(net.GetReferencePosition());
            float wanted = Wanted();
            float now = Time.realtimeSinceStartup;
            bool same = Mathf.Abs(wanted - saidReach) < 0.5f && server.m_uid == saidTo;
            if (wanted <= 0f || (same && now - saidAt < HelloEvery) || (!same && server.m_uid == saidTo && now - saidAt < HelloAtMostEvery))
            {
                return;
            }
            saidAt = now;
            saidReach = wanted;
            saidTo = server.m_uid;
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(wanted);
            package.Write(ReplyName);
            rpc.InvokeRoutedRPC(server.m_uid, HelloName, package);
        }

        // Notes his position every SampleEvery seconds, and forgets what is older than SteadilyCovers looks back.
        private static void Note(Vector3 where)
        {
            float now = Time.realtimeSinceStartup;
            if (now - sampledAt >= SampleEvery)
            {
                sampledAt = now;
                Recent.Add(new KeyValuePair<float, Vector3>(now, where));
            }
            float keep = SteadyFor + ChestClaims.PingSeconds() + 1f;
            int old = 0;
            while (old < Recent.Count - 1 && now - Recent[old + 1].Key > keep)
            {
                old++;   // keeps the newest note older than the span, so the notes still reach back across it
            }
            if (old > 0)
            {
                Recent.RemoveRange(0, old);
            }
        }

        // The largest range among the modules that are on; 0 while none is.
        private static float Wanted()
        {
            float most = 0f;
            foreach (ChestUser user in Users)
            {
                if (user.IsOn)
                {
                    most = Mathf.Max(most, user.Reach);
                }
            }
            return most;
        }

        // A new connection: its reply handler, and everything known of the last one forgotten.
        private static void Register(ZRoutedRpc rpc)
        {
            registeredOn = rpc;
            replying = false;
            Recent.Clear();
            on = false;
            granted = 0f;
            grantedBy = 0L;
            saidAt = float.NegativeInfinity;
            saidReach = -1f;
            saidTo = 0L;
            toldOfGrant = false;
            try
            {
                rpc.Register<ZPackage>(ReplyName, OnReply);
                replying = true;
            }
            catch (ArgumentException)
            {
                // The same plugin twice: its first copy gets the replies (and says the hello).
            }
        }

        // Far Fetched's reply: the protocol it speaks, whether it is on, the range it grants him, and its own limit.
        private static void OnReply(long sender, ZPackage package)
        {
            try
            {
                ZNet net = ZNet.instance;
                ZNetPeer server = net != null && !net.IsServer() ? net.GetServerPeer() : null;
                if (server == null || sender != server.m_uid)
                {
                    return;   // only the server's word counts
                }
                int protocol = package.ReadInt();
                bool isOn = package.ReadBool();
                float reach = package.ReadSingle();
                float limit = package.ReadSingle();
                grantedBy = sender;
                on = protocol == Protocol && isOn && reach > 0f;
                granted = on ? Mathf.Min(reach, ChestSettings.MostRange) : 0f;
                serverLimit = limit;
                if (on && !toldOfGrant)
                {
                    toldOfGrant = true;
                    ZLog.Log($"[QoL Mods chest service] The server runs Far Fetched: it hands you the chests your game has loaded " +
                             $"up to {granted:0} m away (its own limit is {serverLimit:0} m), so they answer at once. Said once a connection.");
                }
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[QoL Mods chest service] Far Fetched's reply couldn't be read: {error.Message}");
            }
        }
    }
}
