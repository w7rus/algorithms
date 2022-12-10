using System.Numerics;

namespace DFS
{
    public class Dfs<TNodeKey, TDim>
        where TNodeKey : IEquatable<TNodeKey>
        where TDim : IBinaryInteger<TDim>, IMinMaxValue<TDim>, IUnsignedNumber<TDim>
    {
        private class DfsException : Exception
        {
            public DfsException()
            {
            }

            public DfsException(string message) : base(message)
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

        public Dfs(Dictionary<TNodeKey, Dictionary<TNodeKey, TDim>> map)
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
                var q = new Stack<TNodeKey>();
                var s = nodeKeys.ToDictionary(_ => _, _ => new NodeKeyData
                {
                    Visited = false, 
                    AccLength = default,
                    From = default
                });
                
                q.Push(nodeKeyFrom);
                s[nodeKeyFrom].Visited = true;
                s[nodeKeyFrom].AccLength = default;

                while (q.Count > 0)
                {
                    var v = q.Pop();

                    foreach (var n in _map[v].Where(_ => !s[_.Key].Visited || s[_.Key].AccLength > SumClampMaxValue(s[v].AccLength, _.Value)))
                    {
                        s[n.Key].Visited = true;
                        q.Push(n.Key);
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
                throw new DfsException("Graph data must follow DFS algorithm requirements!");
            }
            
            return (path, length);
        }
        
        private static TDim SumClampMaxValue(TDim value1, TDim value2)
        {
            return TDim.MaxValue - value1 < value2 || TDim.MaxValue - value2 < value1 ? TDim.MaxValue : value1 + value2;
        }
    }
    
    internal class Program
    {
        static void Main(string[] args)
        {
            //It takes small amount of time to actually find path in a large graph
            
            var w = 1000;
            var h = 1000;
            
            var map = new Dictionary<int, Dictionary<int, ulong>>();
            
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
                    map[idx] = new Dictionary<int, ulong>();
                    if (col + 1 < w)
                    {
                        map[idx].Add(idx + 1, 1);
                    }
                    if (row + 1 < h)
                    {
                        map[idx].Add(idx + w, 1);
                    }
                }
            }
            
            var dfs = new Dfs<int, ulong>(map);

            var result = dfs.Path(0, w * h - 1);
            Console.WriteLine($"{string.Join(" -> ", result.path)} : {result.accLength}");
            
            //Another proof to find shortest path

            map = new Dictionary<int, Dictionary<int, ulong>>
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
            
            dfs = new Dfs<int, ulong>(map);
            
            result = dfs.Path(0, 1);
            Console.WriteLine($"{string.Join(" -> ", result.path)} : {result.accLength}");
        }
    }
}
