using System.Collections.Generic;
using Tarneeb.Config;
using Tarneeb.Core.Rules;
using Tarneeb.Game;
using UnityEngine;

namespace Tarneeb.Net
{
    /// <summary>
    /// Glue between a connected room (an INetTransport) and the Game scene. Online play is switched off by
    /// GameSettings.OnlineEnabled until Photon accounts are connected; nothing here runs in the default build.
    /// </summary>
    public static class OnlineSession
    {
        /// <summary>Set by the lobby once the room is ready and the Game scene is about to load.</summary>
        public static INetTransport Transport;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (GameSettings.OnlineEnabled) GameController.OnlineMatchFactory = CreateMatch;
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
                return host.Build(GameController.SeatNames);
            }

            var client = new NetClient(t, rules, s => new LocalHumanController(s, s));
            return client.Build(GameController.SeatNames);
        }
    }
}
