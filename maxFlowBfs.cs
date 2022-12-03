using System.Numerics;
using System.Text.Json;

namespace MaxFlow
{
    public class Bfs<TNodeKey, TDim>
        where TNodeKey : IEquatable<TNodeKey>
        where TDim : IBinaryInteger<TDim>, IMinMaxValue<TDim>, IUnsignedNumber<TDim>
    {
        private class BfsException : Exception
        {
            public BfsException()
            {
            }

            public BfsException(string message) : base(message)
            {
            }
        }

        private record NodeKeyData
        {
            public bool Visited;
            public TDim AccLength;
            public TNodeKey From;
        }
        
        private readonly Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> _map;

        public Bfs(Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> map)
        {
            _map = map;
        }

        public (Stack<TNodeKey> path, TDim accLength) Path(TNodeKey nodeKeyFrom, TNodeKey nodeKeyTo)
        {
            var nodeKeys = _map.Select(_ => _.Key).Union(_map.SelectMany(_ => _.Value).Select(__ => __.Key)).ToArray();
            var path = new Stack<TNodeKey>();
            TDim length;

            try
            {
                var q = new Queue<TNodeKey>();
                var s = nodeKeys.ToDictionary(_ => _, _ => new NodeKeyData
                {
                    Visited = false, 
                    AccLength = default,
                    From = default
                });
                
                q.Enqueue(nodeKeyFrom);
                s[nodeKeyFrom].Visited = true;
                s[nodeKeyFrom].AccLength = default;

                while (q.Count > 0)
                {
                    var v = q.Dequeue();

                    foreach (var n in _map[v].Where(_ => !s[_.Key].Visited || s[_.Key].AccLength > SumClampMaxValue(s[v].AccLength, _.Value)))
                    {
                        s[n.Key].Visited = true;
                        q.Enqueue(n.Key);
                        s[n.Key].AccLength = SumClampMaxValue(s[v].AccLength, n.Value);
                        s[n.Key].From = v;
                    }
                }
                
                length = s[nodeKeyTo].AccLength;

                for(var nextNodeKey = nodeKeyTo; !nextNodeKey.Equals(nodeKeyFrom);)
                {
                    path.Push(nextNodeKey);
                    nextNodeKey = s[nextNodeKey].From;
                }
                
                path.Push(nodeKeyFrom);
            }
            catch (Exception)
            {
                throw new BfsException("Graph data must follow BFS algorithm requirements!");
            }
            
            return (path, length);
        }
        
        private static TDim SumClampMaxValue(TDim value1, TDim value2)
        {
            return TDim.MaxValue - value1 < value2 || TDim.MaxValue - value2 < value1 ? TDim.MaxValue : value1 + value2;
        }
    }

    public class MaxFlow<TNodeKey, TDim>
        where TNodeKey : IEquatable<TNodeKey>
        where TDim : IBinaryInteger<TDim>, IMinMaxValue<TDim>, IUnsignedNumber<TDim>
    {
        private class MaxFlowException : Exception
        {
            public MaxFlowException()
            {
            }

            public MaxFlowException(string message) : base(message)
            {
            }
        }
        
        private Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> _map;

        public MaxFlow(Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> map)
        {
            _map = JsonSerializer.Deserialize<Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>>>(JsonSerializer.Serialize(map)) ?? throw new MaxFlowException();
        }

        public (Queue<(Stack<TNodeKey> path, TDim maxFlow)> flows, TDim maxFlowTotal) Flows(TNodeKey nodeKeyFrom, TNodeKey nodeKeyTo)
        {
            var maxFlow = TDim.Zero;
            var flows = new Queue<(Stack<TNodeKey> path, TDim maxFlow)>();

            while (true)
            {
                (Stack<TNodeKey> path, TDim accLength) result;
                
                var bfs = new Bfs<TNodeKey, TDim>(_map);
                result = bfs.Path(nodeKeyFrom, nodeKeyTo);

                if (result.accLength == TDim.Zero)
                    break;

                var path = result.path.ToList();

                var minFlowLength = TDim.MaxValue;

                for (var i = 0; i < path.Count - 1; i++)
                {
                    var pathNodeKeyFrom = path[i];
                    var pathNodeKeyTo = path[i + 1];
                    var length = _map[pathNodeKeyFrom][pathNodeKeyTo];

                    minFlowLength = length <= minFlowLength ? length : minFlowLength;
                }
                
                maxFlow += minFlowLength;
                
                flows.Enqueue((result.path, maxFlow));

                var mapNew = new Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>>();
                
                foreach (var kvNodeKeyFrom in _map)
                {
                    mapNew[kvNodeKeyFrom.Key] = new Dictionary<TNodeKey, TDim>();
                        
                    foreach (var kvNodeKeyTo in kvNodeKeyFrom.Value)
                    {
                        mapNew[kvNodeKeyFrom.Key][kvNodeKeyTo.Key] = kvNodeKeyTo.Value;
                    }
                }
                
                for (var i = 0; i < path.Count - 1; i++)
                {
                    var pathNodeKeyFrom = path[i];
                    var pathNodeKeyTo = path[i + 1];

                    var value = SubClampMinValue(_map[pathNodeKeyFrom][pathNodeKeyTo], minFlowLength);

                    if (value == TDim.Zero)
                        mapNew[pathNodeKeyFrom].Remove(pathNodeKeyTo);
                    else
                        mapNew[pathNodeKeyFrom][pathNodeKeyTo] = value;
                }

                _map = mapNew;
            }

            return (flows, maxFlow);
        }
        
        private static TDim SubClampMinValue(TDim value1, TDim value2)
        {
            return TDim.MinValue + value1 < value2 ? TDim.MinValue : value1 - value2;
        }
    }
    
    internal class Program
    {
        static void Main(string[] args)
        {
            var map = new Dictionary<int, Dictionary<int, ulong>>
            {
                {
                    0, new Dictionary<int, ulong>
                    {
                        {1, 10},
                        {3, 2},
                        {4, 4}
                    }
                },
                {
                    1, new Dictionary<int, ulong>
                    {
                        {3, 7},
                        {2, 5}
                    }
                },
                {
                    2, new Dictionary<int, ulong>
                    {
                        {6, 8}
                    }
                },
                {
                    3, new Dictionary<int, ulong>
                    {
                        {2, 6},
                        {6, 2}
                    }
                },
                {
                    4, new Dictionary<int, ulong>
                    {
                        {5, 10}
                    }
                },
                {
                    5, new Dictionary<int, ulong>
                    {
                        {6, 13}
                    }
                },
                {
                    6, new Dictionary<int, ulong>()
                }
            };

            var maxFlow = new MaxFlow<int, ulong>(map);
            var result = maxFlow.Flows(0, 6);

            foreach (var kvFlow in result.flows)
            {
                Console.WriteLine($"{string.Join(" -> ", kvFlow.path)} : {kvFlow.maxFlow}");
            }
        }
    }
}
