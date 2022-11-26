    using System.Numerics;

    namespace Dijkstra
    {
        internal class Program
        {
            static void Main(string[] args)
            {
                var map = new Dictionary<int, Dictionary<int, ulong>>
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
                };
                
                var dijkstra = new Dijkstra<int, ulong>(map);
                
                var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var result = dijkstra.Path(0, 1);
                var end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Console.WriteLine($"{string.Join(" -> ", result.path)} : {result.accLength} [{end - start}]");
                
                var dijkstraHeap = new DijkstraHeap<int, ulong>(map);
                
                var startHeap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var resultHeap = dijkstraHeap.Path(0, 1);
                var endHeap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Console.WriteLine($"{string.Join(" -> ", resultHeap.path)} : {resultHeap.accLength} [{endHeap - startHeap}]");
                
                var w = 100;
                var h = 100;
                
                var map2 = new Dictionary<int, Dictionary<int, uint>>();
                
                /*
                 * Generates graph:
                 * 
                 *      • → • → • →   …   → •   ⮝ 
                 *      ↓   ↓   ↓           ↓   
                 *      • → • → • →   …   → •   
                 *      ↓   ↓   ↓           ↓   
                 *                              h
                 *      …   …   …     …     …   
                 *                              
                 *      ↓   ↓   ↓           ↓   
                 *      • → • → • →   …   → •   ⮟
                 * 
                 *      ⮜          w        ⮞
                 */
                for (var row = 0; row < h; row++)
                {
                    for (var col = 0; col < w; col++)
                    {
                        var idx = row * w + col;
                        map2[idx] = new Dictionary<int, uint>();
                        if (col + 1 < w)
                        {
                            map2[idx].Add(idx + 1, 1);
                        }
                        if (row + 1 < h)
                        {
                            map2[idx].Add(idx + w, 1);
                        }
                    }
                }
                
                var dijkstra2 = new Dijkstra<int, uint>(map2);
                
                start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                result = dijkstra2.Path(0, w * h - 1);
                end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Console.WriteLine($"{string.Join(" -> ", result.path)} : {result.accLength} [{end - start}]");
                
                var dijkstraHeap2 = new DijkstraHeap<int, uint>(map2);
                
                startHeap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                resultHeap = dijkstraHeap2.Path(0, w * h - 1);
                endHeap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Console.WriteLine($"{string.Join(" -> ", resultHeap.path)} : {resultHeap.accLength} [{endHeap - startHeap}]");
            }
        }

        public class Dijkstra<TNodeKey, TDim>
            where TNodeKey : IEquatable<TNodeKey>
            where TDim : IBinaryInteger<TDim>, IMinMaxValue<TDim>, IUnsignedNumber<TDim>
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
            
            private readonly Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> _map;
            private readonly TDim _infinity = TDim.MaxValue;

            public Dijkstra(Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> map)
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

            public (Stack<TNodeKey> path, TDim accLength) Path(TNodeKey nodeKeyFrom, TNodeKey nodeKeyTo)
            {
                var path = new Stack<TNodeKey>();
                TDim length;
                
                var nodeKeys = _map.Select(_ => _.Key).Union(_map.SelectMany(_ => _.Value).Select(__ => __.Key)).ToArray();

                try
                {
                    var s = new List<TNodeKey>
                    {
                        nodeKeyFrom
                    };
                    
                    var v = nodeKeys.ToDictionary(_ => _, _ => true);
                    v[nodeKeyFrom] = false;

                    var d = new Dictionary<int, Dictionary<TNodeKey, (TDim accLength, TNodeKey from)>>
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
                        v[minNodeKey] = false;
                        
                        if (s.Count == _map[nodeKeyFrom].Count)
                            break;

                        d.Add(dl + 1, new Dictionary<TNodeKey, (TDim accLength, TNodeKey from)>());

                        var kvNodeKeyToList = _map[nodeKeyFrom].Where(kvNodeKeyTo => v[kvNodeKeyTo.Key]).ToList();
                        foreach (var kvNodeKeyTo in kvNodeKeyToList)
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

            private static TDim SumClampMaxValue(TDim value1, TDim value2)
            {
                return TDim.MaxValue - value1 < value2 || TDim.MaxValue - value2 < value1 ? TDim.MaxValue : value1 + value2;
            }
        }
        
        public class DijkstraHeap<TNodeKey, TDim>
            where TNodeKey : IEquatable<TNodeKey>
            where TDim : IBinaryInteger<TDim>, IMinMaxValue<TDim>, IUnsignedNumber<TDim>
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
            
            private readonly Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> _map;
            private readonly TDim _infinity = TDim.MaxValue;

            public DijkstraHeap(Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> map)
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

            public (Stack<TNodeKey> path, TDim accLength) Path(TNodeKey nodeKeyFrom, TNodeKey nodeKeyTo)
            {
                var path = new Stack<TNodeKey>();
                TDim length;
                
                var nodeKeys = _map.Select(_ => _.Key).Union(_map.SelectMany(_ => _.Value).Select(__ => __.Key)).ToArray();

                try
                {
                    var s = new List<TNodeKey>
                    {
                        nodeKeyFrom
                    };

                    var v = nodeKeys.ToDictionary(_ => _, _ => true);
                    v[nodeKeyFrom] = false;

                    var d = new Dictionary<int, Dictionary<TNodeKey, (TDim accLength, TNodeKey from)>>
                    {
                        [0] = new()
                    };

                    foreach (var kvNodeKeyTo in _map[nodeKeyFrom].Where(kvNodeKeyTo => !kvNodeKeyTo.Key.Equals(nodeKeyFrom)))
                    {
                        d[0][kvNodeKeyTo.Key] = (kvNodeKeyTo.Value, nodeKeyFrom);
                    }
                    
                    var queue = new PriorityQueue<TNodeKey, TDim>();

                    for (var dl = 0;; dl++)
                    {
                        foreach (var nodeKey in v.Where(_ => _.Value))
                        {
                            queue.Enqueue(nodeKey.Key, _map[nodeKeyFrom][nodeKey.Key]);
                        }
                        
                        var minNodeKey = queue.Dequeue();

                        s.Add(minNodeKey);
                        v[minNodeKey] = false;
                        
                        if (s.Count == _map[nodeKeyFrom].Count)
                            break;

                        d.Add(dl + 1, new Dictionary<TNodeKey, (TDim accLength, TNodeKey from)>());
                        
                        var kvNodeKeyToList = _map[nodeKeyFrom].Where(kvNodeKeyTo => v[kvNodeKeyTo.Key]).ToList();
                        foreach (var kvNodeKeyTo in kvNodeKeyToList)
                        {
                            if (!_map[minNodeKey].TryGetValue(kvNodeKeyTo.Key, out var addLength))
                                addLength = _infinity;

                            var _1 = d[dl][kvNodeKeyTo.Key].accLength;
                            var _2 = SumClampMaxValue(d[dl][minNodeKey].accLength, addLength);

                            var isGreaterThan = _1 > _2;

                            d[dl + 1][kvNodeKeyTo.Key] = (isGreaterThan ? _2 : _1, isGreaterThan ? minNodeKey : d[dl][kvNodeKeyTo.Key].from);
                        }
                        
                        queue.Clear();
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

            private static TDim SumClampMaxValue(TDim value1, TDim value2)
            {
                return TDim.MaxValue - value1 < value2 || TDim.MaxValue - value2 < value1 ? TDim.MaxValue : value1 + value2;
            }
        }
    }
