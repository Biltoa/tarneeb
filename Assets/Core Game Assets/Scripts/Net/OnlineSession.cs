using System.Collections.Generic;
using Tarneeb.Config;
using Tarneeb.Core.Rules;
using Tarneeb.Game;
using UnityEngine;

namespace Tarneeb.Net
{
    /// <summary>
    /// Glue between a connected room (an INetTransport) and the Game scene. Runs only when a room is active (GameController.OnlineRequested);
    /// single player never touches it.
    /// </summary>
    public static class OnlineSession
    {
        /// <summary>Set by the lobby once the room is ready and the Game scene is about to load.</summary>
        public static INetTransport Transport;

        private static readonly string[] ViewNames = GameController.SeatNames;      // by view position: bottom, right, top, left

        /// <summary>Names by engine seat as seen from the local seat: Ahmad is always you (bottom), then Khalid, Omar, Sara around the table.</summary>
        public static string[] NamesFor(int localSeat)
        {
            var n = new string[4];
            int local = localSeat >= 0 && localSeat < 4 ? localSeat : 0;
            for (int s = 0; s < 4; s++) n[s] = ViewNames[(s - local + 4) & 3];
            return n;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            GameController.OnlineMatchFactory = CreateMatch;
        }

        /// <summary>Master builds the authoritative host; everyone else builds a replica client.</summary>
        public static MatchSetup CreateMatch()
        {
            INetTransport t = Transport;
            if (t == null)
            {
                Debug.LogError("[Online] No transport; falling back to a local game is the caller's job.");
                return null;
            }
            var rules = new RulesConfig();
            GameSettings.TurnTimerSeconds = rules.TurnTimerSeconds;

            if (t.IsMasterClient)
            {
                // Seat 0 is the master; the other players fill seats 1..3 in actor order; empty seats get bots.
                var seatByActor = new Dictionary<int, int>();
                int seat = 1;
                foreach (int actor in t.Actors)
                {
                    if (actor == t.LocalActor || seat > 3) continue;
                    seatByActor[actor] = seat++;
                }
                int seed = System.Environment.TickCount;
                var host = new NetHost(t, rules, seed, 0, seatByActor, s => new LocalHumanController(s, seed + s));
                new NetChat(t, actor => seatByActor.TryGetValue(actor, out int s) ? s : -1, () => 0).Attach();
                return host.Build(NamesFor(0));
            }

            var client = new NetClient(t, rules, s => new LocalHumanController(s, s));
            new NetChat(t, null, () => client.LocalSeat).Attach();
            client.NamesForSeat = NamesFor;
            return client.Build(NamesFor(-1));
        }
    }
}
