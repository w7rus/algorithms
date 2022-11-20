namespace Dijkstra
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var dijkstra = new Dijkstra<int>(new Dictionary<int, Dictionary<int, ulong>>
            {
                {
                    0, new Dictionary<int, ulong>
                    {
                        {1, 25},
                        {2, 15},
                        {3, 7},
                        {4, 2}
                    }
                },
                {
                    1, new Dictionary<int, ulong>
                    {
                        {0, 25},
                        {2, 6}
                    }
                },
                {
                    2, new Dictionary<int, ulong>
                    {
                        {0, 15},
                        {1, 6},
                        {3, 4}
                    }
                },
                {
                    3, new Dictionary<int, ulong>
                    {
                        {0, 7},
                        {2, 4},
                        {4, 3}
                    }
                },
                {
                    4, new Dictionary<int, ulong>
                    {
                        {0, 2},
                        {3, 3}
                    }
                },
            });
            
            var result = dijkstra.Path(0, 1);
            Console.WriteLine($"{string.Join(" -> ", result.path)} : {result.accLength}");
        }
    }

    public class Dijkstra<TNodeKey>
        where TNodeKey : IEquatable<TNodeKey>
    {
        private class DijkstraException : Exception
        {
            public DijkstraException()
            {
            }

            public DijkstraException(string? message) : base(message)
            {
            }
        }
        
        private readonly Dictionary<TNodeKey, Dictionary<TNodeKey, ulong>> _map;
        private readonly ulong _infinity = ulong.MaxValue;

        public Dijkstra(Dictionary<TNodeKey, Dictionary<TNodeKey, ulong>> map)
        {
            _map = map;
            var nodeKeys = _map.Select(_ => _.Key).Union(_map.SelectMany(_ => _.Value).Select(__ => __.Key)).ToArray();
            foreach (var (key, value) in _map)
            {
                foreach (var nodeKey in nodeKeys.Except(value.Select(__ => __.Key)))
                {
                    map[key].Add(nodeKey, _infinity);
                }
            }
        }

        public (Stack<TNodeKey> path, ulong accLength) Path(TNodeKey nodeKeyFrom, TNodeKey nodeKeyTo)
        {
            var path = new Stack<TNodeKey>();
            ulong length;

            try
            {
                var s = new List<TNodeKey>
                {
                    nodeKeyFrom
                };

                var d = new Dictionary<int, Dictionary<TNodeKey, (ulong accLength, TNodeKey from)>>
                {
                    [0] = new()
                };

                foreach (var kvNodeKeyTo in _map[nodeKeyFrom].Where(kvNodeKeyTo => !kvNodeKeyTo.Key.Equals(nodeKeyFrom)))
                {
                    d[0][kvNodeKeyTo.Key] = (kvNodeKeyTo.Value, nodeKeyFrom);
                }

                for (var dl = 0;; dl++)
                {
                    var minNodeKey = default(TNodeKey);
                    var minLength = _infinity;

                    foreach (var kvNodeKeyTo in _map[nodeKeyFrom].Where(kvNodeKeyTo => !s.Contains(kvNodeKeyTo.Key) && d[dl][kvNodeKeyTo.Key].accLength <= minLength))
                    {
                        minNodeKey = kvNodeKeyTo.Key;
                        minLength = d[dl][kvNodeKeyTo.Key].accLength;
                    }

                    s.Add(minNodeKey);
                    
                    if (s.Count == _map[nodeKeyFrom].Count)
                        break;

                    d.Add(dl + 1, new Dictionary<TNodeKey, (ulong accLength, TNodeKey from)>());

                    foreach (var kvNodeKeyTo in _map[nodeKeyFrom].Where(kvNodeKeyTo => !s.Contains(kvNodeKeyTo.Key)))
                    {
                        if (!_map[minNodeKey].TryGetValue(kvNodeKeyTo.Key, out var addLength))
                            addLength = _infinity;

                        var _1 = d[dl][kvNodeKeyTo.Key].accLength;
                        var _2 = SumClampMaxValue(d[dl][minNodeKey].accLength, addLength);

                        var isGreaterThan = _1 > _2;

                        d[dl + 1][kvNodeKeyTo.Key] = (isGreaterThan ? _2 : _1, isGreaterThan ? minNodeKey : d[dl][kvNodeKeyTo.Key].from);
                    }
                }
                
                s.RemoveAt(0);
                
                length = d[s.IndexOf(nodeKeyTo)][nodeKeyTo].accLength;

                path.Push(nodeKeyTo);
            
                var nextNodeKey = d[s.IndexOf(nodeKeyTo)][nodeKeyTo].from;
                path.Push(nextNodeKey);

                while (!nextNodeKey.Equals(nodeKeyFrom))
                {
                    nextNodeKey = d[s.IndexOf(nextNodeKey)][nextNodeKey].from;
                    path.Push(nextNodeKey);
                }
            }
            catch (Exception)
            {
                throw new DijkstraException("Graph data must follow Dijkstra algorithm requirements!");
            }
            
            return (path, length);
        }

        private static ulong SumClampMaxValue(ulong value1, ulong value2)
        {
            return ulong.MaxValue - value1 < value2 || ulong.MaxValue - value2 < value1 ? ulong.MaxValue : value1 + value2;
        }
    }
}
