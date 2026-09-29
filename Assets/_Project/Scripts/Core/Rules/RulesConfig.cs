namespace Tarneeb.Core.Rules
{
    /// <summary>Every rule variant lives here; defaults are classic Tarneeb.</summary>
    public sealed class RulesConfig
    {
        public int MinimumBid = 7;
        public int MaximumBid = 13;
        public bool Counterclockwise = true;
        public bool RedealOnAllPass = true;
        public bool DeclarerLeadsFirstTrick = true;
        public bool DefendersScoreWhenContractMade = false;
        public int KabootBonusScore = 16;
        public int TargetScore = 41;
        public int TurnTimerSeconds = 15;
    }
}
