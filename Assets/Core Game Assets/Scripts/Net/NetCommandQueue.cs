using System.Collections.Generic;

namespace Tarneeb.Net
{
    /// <summary>Accepted commands from the master, in the order the master applied them.</summary>
    public sealed class NetCommandQueue
    {
        private readonly Queue<NetCommand> _q = new Queue<NetCommand>();

        public int Count => _q.Count;
        public void Enqueue(NetCommand c) => _q.Enqueue(c);
        public NetCommand Peek() => _q.Peek();
        public NetCommand Dequeue() => _q.Dequeue();
    }
}
