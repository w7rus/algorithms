namespace Kraskal
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var map = new Dictionary<int, Dictionary<int, ulong>>
            {
                {
                    1, new Dictionary<int, ulong>
                    {
                        {2, 20},
                        {7, 1},
                        {6, 23},
                    }
                },
                {
                    2, new Dictionary<int, ulong>
                    {
                        {3, 5},
                        {7, 4},
                    }
                },
                {
                    3, new Dictionary<int, ulong>
                    {
                        {7, 9},
                        {4, 3},
                    }
                },
                {
                    4, new Dictionary<int, ulong>
                    {
                        {7, 16},
                        {5, 17},
                    }
                },
                {
                    5, new Dictionary<int, ulong>
                    {
                        {6, 28},
                        {7, 25},
                    }
                },
                {
                    6, new Dictionary<int, ulong>
                    {
                        {7, 36},
                    }
                },
            };

            var kraskal = new Kraskal<int>(map);
            var result = kraskal.Set();
            Console.WriteLine($"{string.Join(" + ", result)}");
        }
    }

    public class Kraskal<TNodeKey>
        where TNodeKey : IEquatable<TNodeKey>
    {
        private class KraskalException : Exception
        {
            public KraskalException()
            {
            }

            public KraskalException(string message) : base(message)
            {
            }
        }

        private readonly Dictionary<TNodeKey, Dictionary<TNodeKey, ulong>> _map;

        public Kraskal(Dictionary<TNodeKey, Dictionary<TNodeKey, ulong>> map)
        {
            _map = map;
        }

        public List<(TNodeKey, TNodeKey)> Set()
        {
            var set = new List<(TNodeKey, TNodeKey)>();
            var nodeKeys = _map.Select(_ => _.Key).Union(_map.SelectMany(_ => _.Value).Select(__ => __.Key)).ToArray();

            try
            {
                var q = new PriorityQueue<(TNodeKey, TNodeKey), ulong>();
                foreach (var (nodeKeyFrom, kvNodeKeyTo) in _map)
                {
                    foreach (var (nodeKeyTo, length) in kvNodeKeyTo)
                    {
                        q.Enqueue((nodeKeyFrom, nodeKeyTo), length);
                    }
                }

                var joinComponents = nodeKeys.Select(_ => new List<TNodeKey>
                {
                    _
                }).ToList();

                while (q.Count > 0 && joinComponents.Count > 1)
                {
                    var v = q.Dequeue();
                    
                    var _1 = joinComponents.FindIndex(_ => _.Contains(v.Item1));
                    var _2 = joinComponents.FindIndex(_ => _.Contains(v.Item2));

                    if (_1 == _2) continue;
                    
                    set.Add(v);

                    joinComponents[_1].AddRange(joinComponents[_2]);
                    joinComponents.RemoveAt(_2);
                }
            }
            catch (Exception)
            {
                throw new KraskalException("Graph data must follow Kraskal algorithm requirements!");
            }

            return set;
        }
    }
}
