namespace Frog
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var frog = new Frog(new long[] {0, 1, 2, 3, 5, 7, 8, 12, 13, 15, 20, 24});
            var result = frog.Path();
            Console.WriteLine($"{string.Join(" -> ", result)}");
        }
    }

    public class Frog
    {
        private class FrogException : Exception
        {
            public FrogException()
            {
            }

            public FrogException(string message) : base(message)
            {
            }
        }

        private record NodeKeyData
        {
            public long Position;
            public long Velocity;
            public long Step;
        }

        private readonly long[] _map;

        public Frog(long[] map)
        {
            if (map.Any(_ => _ < 0))
                throw new FrogException("Map data must follow Frog algorithm requirements!");
            _map = map;
        }

        public Stack<long> Path()
        {
            var path = new Stack<long>();

            try
            {
                var d = new List<NodeKeyData>
                {
                    new NodeKeyData
                    {
                        Position = 0,
                        Velocity = 0,
                        Step = 0
                    },
                };

                for (var position = _map[0]; position <= _map[^1]; position++)
                {
                    if (!_map.Contains(position)) continue;

                    for (long velocity = 0; velocity < _map.Length; velocity++)
                    {
                        var unchanged = d.Find(_ => _.Position == position - velocity && _.Velocity == velocity)?.Step ?? int.MaxValue;
                        var increase = d.Find(_ => _.Position == position - velocity && _.Velocity == velocity + 1)?.Step ?? int.MaxValue;
                        var decrease = d.Find(_ => _.Position == position - velocity && _.Velocity == velocity - 1)?.Step ?? int.MaxValue;

                        if (unchanged == int.MaxValue && increase == int.MaxValue && decrease == int.MaxValue)
                            continue;

                        if (!d.Any(_ => _.Position == position && _.Velocity == velocity))
                            d.Add(new NodeKeyData
                            {
                                Position = position,
                                Velocity = velocity,
                                Step = Math.Min(unchanged, Math.Min(increase, decrease)) + 1
                            });
                    }
                }

                var nextNodeKey = d.Last();
                path.Push(nextNodeKey.Position);

                for (var limit = nextNodeKey.Step - 1; limit >= 0; limit--) path.Push(d.FindLast(_ => _.Step <= limit)?.Position ?? throw new FrogException());
            }
            catch (Exception)
            {
                throw new FrogException("Map data must follow Frog algorithm requirements!");
            }

            return path;
        }
    }
}
