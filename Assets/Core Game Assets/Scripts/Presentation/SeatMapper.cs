namespace Tarneeb.Presentation
{
    public enum ViewPos { Bottom = 0, Right = 1, Top = 2, Left = 3 }

    /// <summary>Maps engine seats to screen positions so the local player is always at the bottom.</summary>
    public sealed class SeatMapper
    {
        private readonly int _localSeat;
        private readonly bool _counterclockwise;

        public SeatMapper(int localSeat, bool counterclockwise)
        {
            _localSeat = localSeat;
            _counterclockwise = counterclockwise;
        }

        public int LocalSeat => _localSeat;

        public ViewPos ToView(int seat)
        {
            int rel = (seat - _localSeat + 4) & 3;
            // Counterclockwise play seen from above: bottom -> right -> top -> left.
            if (_counterclockwise) return (ViewPos)rel;
            return (ViewPos)((4 - rel) & 3);
        }

        public int ToSeat(ViewPos pos)
        {
            for (int s = 0; s < 4; s++) if (ToView(s) == pos) return s;
            return 0;
        }
    }
}
