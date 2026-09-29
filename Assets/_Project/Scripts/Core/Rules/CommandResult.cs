namespace Tarneeb.Core.Rules
{
    public enum CommandError
    {
        None,
        WrongPhase,
        NotYourTurn,
        InvalidSeat,
        BidBelowMinimum,
        BidAboveMaximum,
        BidNotHigher,
        AlreadyPassed,
        CardNotInHand,
        MustFollowSuit,
        InvalidSuit
    }

    public readonly struct CommandResult
    {
        public readonly bool Success;
        public readonly CommandError Error;

        private CommandResult(bool ok, CommandError e)
        {
            Success = ok;
            Error = e;
        }

        public static CommandResult Ok => new CommandResult(true, CommandError.None);
        public static CommandResult Fail(CommandError e) => new CommandResult(false, e);

        public override string ToString() => Success ? "Ok" : "Fail(" + Error + ")";
    }
}
