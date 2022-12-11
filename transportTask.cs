using System.Security.Cryptography;
using System.Text;
using MemoryPack;

namespace TransportTask
{
    public static class Helpers
    {
        private static readonly SHA256 Sha256 = SHA256.Create();

        public static string GetSha256(this byte[] array)
        {
            return Sha256
                .ComputeHash(array).Select(_ => _.ToString("x2"))
                .Aggregate((a, b) => a + b);
        }
    }

    public class TransportTask<TNodeKey>
        where TNodeKey : IEquatable<TNodeKey>
    {
        private class TransportTaskException : Exception
        {
            public TransportTaskException(string message) : base(message)
            {
            }
        }

        private readonly Dictionary<TNodeKey, int> _providers;
        private readonly Dictionary<TNodeKey, int> _consumers;
        private readonly Dictionary<TNodeKey, Dictionary<TNodeKey, int>> _lengths;
        private Dictionary<TNodeKey, Dictionary<TNodeKey, int?>> _resourceAllocation;
        private Dictionary<TNodeKey, int> _providerCoefficients;
        private Dictionary<TNodeKey, int> _consumerCoefficients;

        private readonly Dictionary<TNodeKey, int> _providerByIndex = new();
        private readonly Dictionary<TNodeKey, int> _consumerByIndex = new();

        private readonly bool _debugPrint;

        public TransportTask(
            Dictionary<TNodeKey, int> storages,
            Dictionary<TNodeKey, int> shops,
            Dictionary<TNodeKey, Dictionary<TNodeKey, int>> lengths,
            TNodeKey dummyNodeKey,
            bool debugPrint = false
        )
        {
            _debugPrint = debugPrint;
            
            DebugPrint("[INF] Init...");

            DebugPrint("[INF] Saving input data...");
            _providers = MemoryPackSerializer.Deserialize<Dictionary<TNodeKey, int>>(MemoryPackSerializer.Serialize(storages)) ??
                        throw new TransportTaskException($"Failed to save {nameof(storages)}!");
            _consumers = MemoryPackSerializer.Deserialize<Dictionary<TNodeKey, int>>(MemoryPackSerializer.Serialize(shops)) ??
                     throw new TransportTaskException($"Failed to save {nameof(shops)}!");
            _lengths = MemoryPackSerializer.Deserialize<Dictionary<TNodeKey, Dictionary<TNodeKey, int>>>(MemoryPackSerializer.Serialize(lengths)) ??
                       throw new TransportTaskException($"Failed to save {nameof(lengths)}!");

            var storageSum = _providers.Sum(_ => _.Value);
            var shopsSum = _consumers.Sum(_ => _.Value);

            //Providers has more resources than consumers require
            if (storageSum > shopsSum)
            {
                var diff = storageSum - shopsSum;

                DebugPrint($"[INF] Add dummy consumer with [{diff}] resources, so that TransportTask is closed!");

                //Add dummy consumer
                _consumers.Add(dummyNodeKey, storageSum - shopsSum);

                foreach (var storage in _providers)
                    _lengths[storage.Key].Add(dummyNodeKey, 0);
            }
            //Consumers has more resources than providers require
            else if (storageSum < shopsSum)
            {
                var diff = shopsSum - storageSum;

                DebugPrint($"[INF] Add dummy provider with [{diff}] resources, so that TransportTask is closed!");

                //Add dummy provider
                _providers.Add(dummyNodeKey, shopsSum - storageSum);

                foreach (var shop in _consumers)
                    _lengths[dummyNodeKey].Add(shop.Key, 0);
            }

            //Provider key-to-index dictionary. Used in Transport task cycle search algorithm
            var storageIndex = 0;
            foreach (var storage in _providers)
                _providerByIndex.Add(storage.Key, storageIndex++);

            //Consumer key-to-index dictionary. Used in Transport task cycle search algorithm
            var shopIndex = 0;
            foreach (var shop in _consumers)
                _consumerByIndex.Add(shop.Key, shopIndex++);

            DebugPrint("[INF] Allocate resources via North-West method...");

            //Initial resource allocation done via North-West method
            _resourceAllocation = new Dictionary<TNodeKey, Dictionary<TNodeKey, int?>>();

            foreach (var storage in _providers)
            {
                _resourceAllocation.Add(storage.Key, new Dictionary<TNodeKey, int?>());

                var storageResource = storage.Value;

                foreach (var shop in _consumers)
                {
                    _resourceAllocation[storage.Key].Add(shop.Key, null);

                    var shopResourcesAllocated = _resourceAllocation
                        .SelectMany(_ => _.Value)
                        .Where(_ => _.Value.HasValue && _.Key.Equals(shop.Key))
                        .Sum(_ => _.Value.Value);

                    if (shopResourcesAllocated == shop.Value)
                        continue;

                    var shopResourceAllocate = Math.Min(storageResource, shop.Value - shopResourcesAllocated);
                    storageResource -= shopResourceAllocate;
                    _resourceAllocation[storage.Key][shop.Key] = shopResourceAllocate > 0 ? shopResourceAllocate : null;
                }
            }

            DebugPrint("[INF] Init complete!");
        }

        private void DebugPrint(string value = null)
        {
            if (_debugPrint)
                Console.WriteLine(value);
        }

        /// <summary>
        /// Updates provider & consumer coefficients
        /// </summary>
        /// <exception cref="TransportTask{TNodeKey}.TransportTaskException"></exception>
        private void UpdateCoefficients()
        {
            _providerCoefficients = new Dictionary<TNodeKey, int>();
            _consumerCoefficients = new Dictionary<TNodeKey, int>
            {
                {_consumers.First().Key, 0}
            };

            var recalculateLater =
                new Queue<(TNodeKey provider, TNodeKey consumer)>(_resourceAllocation.SelectMany(_ => _.Value.Where(__ => __.Value.HasValue), (___, ____) => (___.Key, ____.Key)));

            while (recalculateLater.Count != 0 && !(_providerCoefficients.Count == _providers.Count && _consumerCoefficients.Count == _consumers.Count))
            {
                var recalculateLaterItem = recalculateLater.Dequeue();

                //If resource allocation intersection has consumer's coefficient already defined, find provider's one
                if (_consumerCoefficients.ContainsKey(recalculateLaterItem.consumer) && !_providerCoefficients.ContainsKey(recalculateLaterItem.provider))
                {
                    if (_providerCoefficients.ContainsKey(recalculateLaterItem.provider))
                    {
                        _providerCoefficients[recalculateLaterItem.provider] =
                            _lengths[recalculateLaterItem.provider][recalculateLaterItem.consumer] - _consumerCoefficients[recalculateLaterItem.consumer];
                    }
                    else
                    {
                        _providerCoefficients.Add(recalculateLaterItem.provider,
                            _lengths[recalculateLaterItem.provider][recalculateLaterItem.consumer] - _consumerCoefficients[recalculateLaterItem.consumer]);
                    }
                }
                //If resource allocation intersection has provider's coefficient already defined, find consumer's one
                else if (_providerCoefficients.ContainsKey(recalculateLaterItem.provider))
                {
                    if (_consumerCoefficients.ContainsKey(recalculateLaterItem.consumer))
                    {
                        _consumerCoefficients[recalculateLaterItem.consumer] =
                            _lengths[recalculateLaterItem.provider][recalculateLaterItem.consumer] - _providerCoefficients[recalculateLaterItem.provider];
                    }
                    else
                    {
                        _consumerCoefficients.Add(recalculateLaterItem.consumer,
                            _lengths[recalculateLaterItem.provider][recalculateLaterItem.consumer] - _providerCoefficients[recalculateLaterItem.provider]);
                    }
                }
                //Else, enqueue for later
                else
                {
                    if (recalculateLater.Count == 0)
                        throw new TransportTaskException($"Unable to find coefficient at {recalculateLaterItem}!");

                    recalculateLater.Enqueue(recalculateLaterItem);
                }
            }
        }

        /// <summary>
        /// Returns transport task function value
        /// </summary>
        /// <returns></returns>
        private int GetFunctionValue() =>
            _resourceAllocation
                .SelectMany(_ => _.Value, (__, ___) => !_resourceAllocation[__.Key][___.Key].HasValue ? 0 : _lengths[__.Key][___.Key] * _resourceAllocation[__.Key][___.Key].Value)
                .Sum();

        /// <summary>
        /// Checks if transport task plan is degenerate
        /// </summary>
        /// <returns></returns>
        private bool DegeneracyCheck() => _resourceAllocation.SelectMany(_ => _.Value).Select(_ => _.Value).Count(_ => _.HasValue) < _providers.Count + _consumers.Count - 1;

        /// <summary>
        /// Fixes initial transport task plan degeneracy
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private void InitialDegeneracyFix()
        {
            var tempStorageNodeKey = _providers.First().Key;
            var tempShopNodeKey = _consumers.First().Key;
            var tempDirections = new Stack<int>();
            tempDirections.Push(1); //Down moving direction
            tempDirections.Push(0); //Right moving direction

            var lastStorageNodeKey = _providers.Last().Key;
            var lastShopNodeKey = _consumers.Last().Key;

            while (!tempStorageNodeKey.Equals(lastStorageNodeKey) || !tempShopNodeKey.Equals(lastShopNodeKey))
            {
                var found = true;

                switch (tempDirections.Peek())
                {
                    case 0: //Moving to the right in transport task plan
                    {
                        var nextConsumerIdx = _consumerByIndex[tempShopNodeKey] + 1;
                        if (nextConsumerIdx > _consumerByIndex.Max(_ => _.Value))
                            found = false;
                        else
                        {
                            var nextShop = _consumerByIndex.First(_ => _.Value == nextConsumerIdx);

                            if (_resourceAllocation[tempStorageNodeKey][nextShop.Key].HasValue)
                                tempShopNodeKey = nextShop.Key;
                            else
                                found = false;
                        }

                        break;
                    }
                    case 1: //Moving down in transport task plan
                    {
                        var nextProviderIdx = _providerByIndex[tempStorageNodeKey] + 1;
                        if (nextProviderIdx > _providerByIndex.Max(_ => _.Value))
                            found = false;
                        else
                        {
                            var nextStorage = _providerByIndex.First(_ => _.Value == nextProviderIdx);

                            if (_resourceAllocation[nextStorage.Key][tempShopNodeKey].HasValue)
                                tempStorageNodeKey = nextStorage.Key;
                            else
                                found = false;
                        }

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (!found)
                    tempDirections.Pop();
                else
                {
                    tempDirections.Clear();
                    tempDirections.Push(1); //Down moving direction
                    tempDirections.Push(0); //Right moving direction
                }

                if (tempDirections.Count != 0) continue;

                //Fix degeneracy by setting 0 to the right
                var issueAtProvider = tempStorageNodeKey;
                var issueAtConsumer = _consumerByIndex.Single(_ => _.Value == _consumerByIndex[tempShopNodeKey] + 1).Key;

                DebugPrint($"[INF] Set zero at: {(issueAtProvider, issueAtConsumer)}!");
                _resourceAllocation[issueAtProvider][issueAtConsumer] = 0;

                tempStorageNodeKey = issueAtProvider;
                tempShopNodeKey = issueAtConsumer;

                tempDirections.Clear();
                tempDirections.Push(1); //Down moving direction
                tempDirections.Push(0); //Right moving direction
            }
        }

        private class PriorityQueueInverseComparer : IComparer<int>
        {
            public int Compare(int x, int y)
            {
                if (x == y)
                    return 0;

                return x < y ? 1 : -1;
            }
        }

        /// <summary>
        /// Returns unset resource reallocation origins which potentials greater than 0
        /// </summary>
        /// <returns></returns>
        private PriorityQueue<(TNodeKey storage, TNodeKey shop), int> GetResourceReallocationOrigins()
        {
            var priorityQueue = new PriorityQueue<(TNodeKey storage, TNodeKey shop), int>(new PriorityQueueInverseComparer());

            foreach (var providerCoefficient in _providerCoefficients)
            foreach (var consumerCoefficient in _consumerCoefficients)
            {
                var value = providerCoefficient.Value + consumerCoefficient.Value - _lengths[providerCoefficient.Key][consumerCoefficient.Key];

                if (!_resourceAllocation[providerCoefficient.Key][consumerCoefficient.Key].HasValue && value > 0)
                    priorityQueue.Enqueue((providerCoefficient.Key, consumerCoefficient.Key), providerCoefficient.Value + consumerCoefficient.Value - _lengths[providerCoefficient.Key][consumerCoefficient.Key]);
            }

            return priorityQueue;
        }

        private class ResourceReallocationCycleItem
        {
            public TNodeKey Provider { get; init; }
            public TNodeKey Consumer { get; init; }
            public Stack<int> Directions { get; init; }
            public int? DirectionOffset { get; set; }
        }

        /// <summary>
        /// Returns whether next resource reallocation cycle node is valid to be added
        /// </summary>
        /// <param name="resourceReallocationCycle"></param>
        /// <param name="resourceReallocationOrigin"></param>
        /// <param name="nextStorage"></param>
        /// <param name="nextShop"></param>
        /// <returns></returns>
        private static bool ResourceAllocationIsValid(
            IEnumerable<ResourceReallocationCycleItem> resourceReallocationCycle,
            (TNodeKey storage, TNodeKey shop) resourceReallocationOrigin,
            TNodeKey nextStorage,
            TNodeKey nextShop
        )
        {
            var isOrigin = nextStorage.Equals(resourceReallocationOrigin.storage) && nextShop.Equals(resourceReallocationOrigin.shop);
            var isInCycle = resourceReallocationCycle.Any(_ => _.Provider.Equals(nextStorage) && _.Consumer.Equals(nextShop));

            return !isInCycle || isOrigin;
        }

        /// <summary>
        /// Returns resource reallocation cycle for resource reallocation origin
        /// </summary>
        /// <param name="resourceReallocationOrigin">[Data] returned by GetResourceReallocationOrigins</param>
        /// <returns></returns>
        /// <exception cref="TransportTask{TNodeKey}.TransportTaskException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private List<(TNodeKey provider, TNodeKey consumer)> GetResourceReallocationCycle((TNodeKey storage, TNodeKey shop) resourceReallocationOrigin)
        {
            _resourceAllocation[resourceReallocationOrigin.storage][resourceReallocationOrigin.shop] ??= 0;

            var resourceReallocationCycle = new Stack<ResourceReallocationCycleItem>();

            var originDirections = new Stack<int>();
            originDirections.Push(0); //Right
            originDirections.Push(1); //Down
            originDirections.Push(2); //Left
            originDirections.Push(3); //Up

            resourceReallocationCycle.Push(new ResourceReallocationCycleItem
            {
                Directions = originDirections,
                Provider = resourceReallocationOrigin.storage,
                Consumer = resourceReallocationOrigin.shop,
                DirectionOffset = null
            });

            while (true)
            {
                var resourceReallocationCycleItem = resourceReallocationCycle.Peek();

                var tempStorageNodeKey = resourceReallocationCycleItem.Provider;
                var tempShopNodeKey = resourceReallocationCycleItem.Consumer;
                var found = false;

                //If out of directions for current resource reallocation cycle node, pop it
                if (resourceReallocationCycleItem.Directions.Count == 0)
                {
                    if (resourceReallocationCycle.Count <= 0) return null;

                    resourceReallocationCycle.Pop();
                    continue;
                }

                var tempDirection = resourceReallocationCycleItem.Directions.Peek();

                switch (tempDirection)
                {
                    case 0: //Moving to the right in transport task plan
                    {
                        if (!resourceReallocationCycleItem.DirectionOffset.HasValue)
                            resourceReallocationCycleItem.DirectionOffset = _consumerByIndex[tempShopNodeKey] + 1;
                        else
                            resourceReallocationCycleItem.DirectionOffset += 1;

                        for (var i = resourceReallocationCycleItem.DirectionOffset; i <= _consumerByIndex.Max(_ => _.Value); i++)
                        {
                            var nextConsumer = _consumerByIndex.First(_ => _.Value == i).Key;

                            if (!_resourceAllocation[tempStorageNodeKey][nextConsumer].HasValue ||
                                !ResourceAllocationIsValid(resourceReallocationCycle, resourceReallocationOrigin, tempStorageNodeKey, nextConsumer)) continue;

                            resourceReallocationCycleItem.DirectionOffset = i;
                            found = true;
                            tempShopNodeKey = nextConsumer;
                            break;
                        }

                        break;
                    }
                    case 1: //Moving down in transport task plan
                    {
                        if (!resourceReallocationCycleItem.DirectionOffset.HasValue)
                            resourceReallocationCycleItem.DirectionOffset = _providerByIndex[tempStorageNodeKey] + 1;
                        else
                            resourceReallocationCycleItem.DirectionOffset += 1;

                        for (var i = resourceReallocationCycleItem.DirectionOffset; i <= _providerByIndex.Max(_ => _.Value); i++)
                        {
                            var nextProvider = _providerByIndex.First(_ => _.Value == i).Key;

                            if (!_resourceAllocation[nextProvider][tempShopNodeKey].HasValue ||
                                !ResourceAllocationIsValid(resourceReallocationCycle, resourceReallocationOrigin, nextProvider, tempShopNodeKey)) continue;

                            resourceReallocationCycleItem.DirectionOffset = i;
                            found = true;
                            tempStorageNodeKey = nextProvider;
                            break;
                        }

                        break;
                    }
                    case 2: //Moving to the left in transport task plan
                    {
                        if (!resourceReallocationCycleItem.DirectionOffset.HasValue)
                            resourceReallocationCycleItem.DirectionOffset = _consumerByIndex[tempShopNodeKey] - 1;
                        else
                            resourceReallocationCycleItem.DirectionOffset -= 1;

                        for (var i = resourceReallocationCycleItem.DirectionOffset; i >= _consumerByIndex.Min(_ => _.Value); i--)
                        {
                            var nextConsumer = _consumerByIndex.First(_ => _.Value == i).Key;

                            if (!_resourceAllocation[tempStorageNodeKey][nextConsumer].HasValue ||
                                !ResourceAllocationIsValid(resourceReallocationCycle, resourceReallocationOrigin, tempStorageNodeKey, nextConsumer)) continue;

                            resourceReallocationCycleItem.DirectionOffset = i;
                            found = true;
                            tempShopNodeKey = nextConsumer;
                            break;
                        }

                        break;
                    }
                    case 3: //Moving up in transport task plan
                    {
                        if (!resourceReallocationCycleItem.DirectionOffset.HasValue)
                            resourceReallocationCycleItem.DirectionOffset = _providerByIndex[tempStorageNodeKey] - 1;
                        else
                            resourceReallocationCycleItem.DirectionOffset -= 1;

                        for (var i = resourceReallocationCycleItem.DirectionOffset; i >= _providerByIndex.Min(_ => _.Value); i--)
                        {
                            var nextProvider = _providerByIndex.First(_ => _.Value == i).Key;

                            if (!_resourceAllocation[nextProvider][tempShopNodeKey].HasValue ||
                                !ResourceAllocationIsValid(resourceReallocationCycle, resourceReallocationOrigin, nextProvider, tempShopNodeKey)) continue;

                            resourceReallocationCycleItem.DirectionOffset = i;
                            found = true;
                            tempStorageNodeKey = nextProvider;
                            break;
                        }

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                //If resource reallocation cycle node was found
                if (found)
                {
                    //Next resource reallocation cycle node moving directions should be perpendicular to the current moving direction
                    var nextDirections = new Stack<int>();

                    if (tempDirection % 2 == 0)
                    {
                        nextDirections.Push(1); //Down moving direction
                        nextDirections.Push(3); //Up moving direction
                    }
                    else
                    {
                        nextDirections.Push(0); //Right moving direction
                        nextDirections.Push(2); //Left moving direction
                    }

                    resourceReallocationCycle.Push(new ResourceReallocationCycleItem
                    {
                        Directions = nextDirections,
                        Provider = tempStorageNodeKey,
                        Consumer = tempShopNodeKey,
                        DirectionOffset = null
                    });

                    //If resource reallocation origin is appears 2 times in resource reallocation cycle then cycle has been made
                    if (resourceReallocationCycle.Count(_ => _.Provider.Equals(resourceReallocationOrigin.storage) && _.Consumer.Equals(resourceReallocationOrigin.shop)) > 1)
                        break;
                }
                //If resource reallocation cycle node was not found
                else
                {
                    //If theres any remaining directions to move at, pop them
                    if (resourceReallocationCycleItem.Directions.Count > 1)
                    {
                        resourceReallocationCycleItem.Directions.Pop();
                        resourceReallocationCycleItem.DirectionOffset = null;
                    }
                    else
                        resourceReallocationCycle.Pop();
                }
            }

            //Since its a stack, it should be reversed, direction of the cycle does matter
            return resourceReallocationCycle.Select(_ => (Storage: _.Provider, Shop: _.Consumer)).Reverse().ToList();
        }

        /// <summary>
        /// Returns resource reallocation value for resource reallocation cycle
        /// </summary>
        /// <param name="resourceReallocationCycle">Data returned by GetResourceReallocationCycle</param>
        /// <returns></returns>
        /// <exception cref="TransportTask{TNodeKey}.TransportTaskException"></exception>
        private int GetResourceReallocationValue(IReadOnlyCollection<(TNodeKey provider, TNodeKey consumer)> resourceReallocationCycle)
        {
            var priorityQueue = new PriorityQueue<int, int>();

            for (var i = 0; i < resourceReallocationCycle.Count - 1; i++)
            {
                if (i % 2 == 0) continue;

                var cycleItem = resourceReallocationCycle.ElementAt(i);

                if (!_resourceAllocation[cycleItem.provider][cycleItem.consumer].HasValue)
                    throw new TransportTaskException($"Reallocate resources cycle node {(cycleItem.provider, cycleItem.consumer)} must not be null!");

                priorityQueue.Enqueue(_resourceAllocation[cycleItem.provider][cycleItem.consumer].Value, _resourceAllocation[cycleItem.provider][cycleItem.consumer].Value);
            }

            //Return minimum value across resource reallocation cycle nodes that are going to have their resources subtracted
            return priorityQueue.Dequeue();
        }

        /// <summary>
        /// Reallocates resources in resource reallocation cycle
        /// </summary>
        /// <param name="resourceReallocationCycle">Data returned by GetResourceReallocationCycle</param>
        /// <param name="resourceReallocationValue">Data returned by GetResourceReallocationValue</param>
        /// <returns></returns>
        /// <exception cref="TransportTask{TNodeKey}.TransportTaskException"></exception>
        private List<(TNodeKey provider, TNodeKey consumer)> ReallocateResources(
            IReadOnlyCollection<(TNodeKey provider, TNodeKey consumer)> resourceReallocationCycle,
            int resourceReallocationValue
        )
        {
            var depletedReallocateResources = new List<(TNodeKey provider, TNodeKey consumer)>();

            var origin = resourceReallocationCycle.ElementAt(0);

            for (var i = 0; i < resourceReallocationCycle.Count - 1; i++)
            {
                (TNodeKey provider, TNodeKey consumer) cycleItem = resourceReallocationCycle.ElementAt(i);

                if (!_resourceAllocation[cycleItem.provider][cycleItem.consumer].HasValue)
                    throw new TransportTaskException($"Reallocate resources cycle node {(cycleItem.provider, cycleItem.consumer)} must not be null!");

                //(Add to/subtract from) resource reallocation cycle node
                _resourceAllocation[cycleItem.provider][cycleItem.consumer] += i % 2 == 0 ? resourceReallocationValue : -resourceReallocationValue;

                //If non-origin resource reallocation cycle nodes depleted - set them to null
                if (_resourceAllocation[cycleItem.provider][cycleItem.consumer]! > 0 || cycleItem.provider.Equals(origin.provider) && cycleItem.consumer.Equals(origin.consumer)) continue;

                depletedReallocateResources.Add((cycleItem.provider, cycleItem.consumer));
                _resourceAllocation[cycleItem.provider][cycleItem.consumer] = null;
            }

            return depletedReallocateResources;
        }

        private class TransportTaskSave
        {
            public byte[] ResourceAllocation { get; init; }
            public byte[] DepletedReallocationResources { get; init; }
            public int ExcludeIdx { get; set; }

            public string GetSha256()
            {
                var resourceAllocationHash = ResourceAllocation.GetSha256();
                var depletedReallocationResourcesHash = DepletedReallocationResources.GetSha256();

                return Encoding.UTF8.GetBytes(resourceAllocationHash + depletedReallocationResourcesHash + ExcludeIdx).GetSha256();
            }
        }

        public (Dictionary<TNodeKey, Dictionary<TNodeKey, int?>> resourceAllocation, int functionValue) Solve()
        {
            var solutions = new PriorityQueue<(Dictionary<TNodeKey, Dictionary<TNodeKey, int?>> resourceAllocation, int functionValue), int>();
            var transportTaskSaves = new Stack<TransportTaskSave>();
            var doDegeneracyFix = false;

            if (DegeneracyCheck())
            {
                DebugPrint("[WRN] Fix starting plan degeneracy...");
                InitialDegeneracyFix();
            }

            while (true)
            {
                //This block restores transport task save and does degeneracy fix by all possible combinations of zero filling N-1 depleted reallocation resources
                if (transportTaskSaves.Count > 0 && doDegeneracyFix)
                {
                    DebugPrint("[INF] Fix plan degeneracy...");

                    doDegeneracyFix = false;
                    var transportTaskSave = transportTaskSaves.Peek();

                    var resourceAllocationTemp = MemoryPackSerializer.Deserialize<Dictionary<TNodeKey, Dictionary<TNodeKey, int?>>>(transportTaskSave.ResourceAllocation) ??
                                                 throw new TransportTaskException($"Failed to restore {nameof(transportTaskSave.ResourceAllocation)}!");
                    var depletedReallocationResourcesTemp =
                        MemoryPackSerializer.Deserialize<List<(TNodeKey storage, TNodeKey shop)>>(transportTaskSave.DepletedReallocationResources) ??
                        throw new TransportTaskException($"Failed to restore {nameof(transportTaskSave.DepletedReallocationResources)}!");

                    _resourceAllocation = resourceAllocationTemp;

                    if (transportTaskSave.ExcludeIdx < depletedReallocationResourcesTemp.Count)
                    {
                        DebugPrint("[INF] Apply combination of resource zero fill to solve degeneracy...");

                        foreach (var kvStorageShop in depletedReallocationResourcesTemp.Where((_, idx) => idx != transportTaskSave.ExcludeIdx))
                        {
                            DebugPrint($"[INF] Set zero at: {(kvStorageShop.storage, kvStorageShop.shop)}!");
                            _resourceAllocation[kvStorageShop.storage][kvStorageShop.shop] = 0;
                        }

                        transportTaskSave.ExcludeIdx++;
                    }
                    else
                    {
                        DebugPrint("[INF] Out of combinations of resource zero filling to solve degeneracy... Pop save!");
                        transportTaskSaves.Pop();

                        if (transportTaskSaves.Count == 0)
                        {
                            DebugPrint("[INF] Out of saves... Shutdown!");
                            break;
                        }

                        doDegeneracyFix = true;
                        continue;
                    }
                }

                //Recalculate coefficients of providers & consumers
                UpdateCoefficients();

                //Get current function value
                var functionValue = GetFunctionValue();
                DebugPrint($"[INF] Function value: {functionValue}");

                //Check if there is resource reallocation loop, if one found, pop saves to continue with the next combination
                var saveHashes = transportTaskSaves.Select(_ => _.GetSha256()).ToList();
                if (saveHashes.Count != saveHashes.Distinct().Count())
                {
                    var duplicate = saveHashes.GroupBy(_ => _).Where(_ => _.Count() > 1).Select(_ => _.Key).First();

                    var firstIndexOf = saveHashes.IndexOf(duplicate);
                    var lastIndexOf = saveHashes.LastIndexOf(duplicate);

                    var range = lastIndexOf - firstIndexOf + 1;

                    DebugPrint($"[WRN] Resource reallocation loop found! Pop [{range}] saves");

                    for (var i = 0; i < range; i++) transportTaskSaves.Pop();

                    doDegeneracyFix = true;
                    continue;
                }

                //Get unset resource reallocation origins by finding potentials greater than 0
                var resourceReallocationOrigins = GetResourceReallocationOrigins();
                if (resourceReallocationOrigins.Count == 0)
                {
                    DebugPrint("[INF] Solve complete! There are no more potentials greater than 0");

                    solutions.Enqueue((_resourceAllocation, functionValue), functionValue);

                    if (transportTaskSaves.Count == 0)
                        break;

                    DebugPrint(Environment.NewLine + "[INF] There are transport task saves remaining, continue...");
                    doDegeneracyFix = true;
                    continue;
                }

                List<(TNodeKey Storages, TNodeKey shop)> resourceReallocationCycle = null;

                //Attempt all resource reallocation origins to find valid resource reallocation cycle
                while (resourceReallocationOrigins.Count > 0)
                {
                    var resourceReallocationOriginTemp = resourceReallocationOrigins.Dequeue();

                    DebugPrint($"[INF] Trying resource reallocation origin: {resourceReallocationOriginTemp}");
                    resourceReallocationCycle = GetResourceReallocationCycle(resourceReallocationOriginTemp);
                    if (resourceReallocationCycle != null)
                        break;

                    DebugPrint($"[WRN] No resource reallocation cycle found for resource reallocation origin: {resourceReallocationOriginTemp}");
                }

                if (resourceReallocationCycle == null)
                {
                    if (transportTaskSaves.Count == 0) throw new TransportTaskException("No resource reallocation cycle was found and no more transport task saves remaining!");

                    DebugPrint("[WRN] No resource reallocation cycle was found, continue...");
                    doDegeneracyFix = true;
                    continue;
                }

                DebugPrint($"[INF] Resource reallocation cycle: {string.Join(",", resourceReallocationCycle)}");

                //Find minimum value across resource reallocation cycle nodes that are going to have their resources subtracted
                var resourceReallocationValue = GetResourceReallocationValue(resourceReallocationCycle);
                DebugPrint($"[INF] Resource reallocation value: {resourceReallocationValue}");

                //Reallocate resources in resource reallocation cycle, if there are more than 1 depleted reallocation resources, then plan has degenerated
                var depletedReallocationResources = ReallocateResources(resourceReallocationCycle, resourceReallocationValue);
                if (depletedReallocationResources.Count > 1)
                {
                    DebugPrint("[WRN] Plan degeneracy detected! Save transport task...");
                    doDegeneracyFix = true;

                    var resourceAllocationSerialized = MemoryPackSerializer.Serialize(_resourceAllocation);
                    var depletedReallocationResourcesSerialized = MemoryPackSerializer.Serialize(depletedReallocationResources);

                    transportTaskSaves.Push(new TransportTaskSave
                    {
                        ResourceAllocation = resourceAllocationSerialized,
                        DepletedReallocationResources = depletedReallocationResourcesSerialized,
                        ExcludeIdx = 0
                    });
                }

                DebugPrint();
            }

            var bestSolution = solutions.Dequeue();

            return (bestSolution.resourceAllocation, bestSolution.functionValue);
        }
    }

    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var transportTask = new TransportTask<string>(
                new Dictionary<string, int>
                {
                    {"Storage #1", 7},
                    {"Storage #2", 8},
                    {"Storage #3", 15},
                },
                new Dictionary<string, int>
                {
                    {"Shop #1", 6},
                    {"Shop #2", 5},
                    {"Shop #3", 7},
                    {"Shop #4", 4},
                    {"Shop #5", 5},
                },
                new Dictionary<string, Dictionary<string, int>>
                {
                    {
                        "Storage #1", new Dictionary<string, int>
                        {
                            {"Shop #1", 2},
                            {"Shop #2", 4},
                            {"Shop #3", 6},
                            {"Shop #4", 3},
                            {"Shop #5", 1},
                        }
                    },
                    {
                        "Storage #2", new Dictionary<string, int>
                        {
                            {"Shop #1", 3},
                            {"Shop #2", 5},
                            {"Shop #3", 2},
                            {"Shop #4", 7},
                            {"Shop #5", 3},
                        }
                    },
                    {
                        "Storage #3", new Dictionary<string, int>
                        {
                            {"Shop #1", 2},
                            {"Shop #2", 1},
                            {"Shop #3", 3},
                            {"Shop #4", 1},
                            {"Shop #5", 5},
                        }
                    },
                },
                "Dummy"
            );
            
            var transportTask2 = new TransportTask<string>(
                new Dictionary<string, int>
                {
                    {"Storage #1", 12},
                    {"Storage #2", 10},
                    {"Storage #3", 14},
                },
                new Dictionary<string, int>
                {
                    {"Shop #1", 4},
                    {"Shop #2", 18},
                    {"Shop #3", 8},
                    {"Shop #4", 6}
                },
                new Dictionary<string, Dictionary<string, int>>
                {
                    {
                        "Storage #1", new Dictionary<string, int>
                        {
                            {"Shop #1", 2},
                            {"Shop #2", 4},
                            {"Shop #3", 6},
                            {"Shop #4", 3},
                        }
                    },
                    {
                        "Storage #2", new Dictionary<string, int>
                        {
                            {"Shop #1", 3},
                            {"Shop #2", 5},
                            {"Shop #3", 2},
                            {"Shop #4", 7},
                        }
                    },
                    {
                        "Storage #3", new Dictionary<string, int>
                        {
                            {"Shop #1", 2},
                            {"Shop #2", 1},
                            {"Shop #3", 3},
                            {"Shop #4", 1},
                        }
                    },
                },
                "Dummy"
            );
            
            var transportTask3 = new TransportTask<string>(
                new Dictionary<string, int>
                {
                    {"Storage #1", 6},
                    {"Storage #2", 3},
                    {"Storage #3", 4},
                },
                new Dictionary<string, int>
                {
                    {"Shop #1", 4},
                    {"Shop #2", 5},
                    {"Shop #3", 1},
                    {"Shop #4", 3}
                },
                new Dictionary<string, Dictionary<string, int>>
                {
                    {
                        "Storage #1", new Dictionary<string, int>
                        {
                            {"Shop #1", 2},
                            {"Shop #2", 4},
                            {"Shop #3", 6},
                            {"Shop #4", 3},
                        }
                    },
                    {
                        "Storage #2", new Dictionary<string, int>
                        {
                            {"Shop #1", 3},
                            {"Shop #2", 5},
                            {"Shop #3", 2},
                            {"Shop #4", 7},
                        }
                    },
                    {
                        "Storage #3", new Dictionary<string, int>
                        {
                            {"Shop #1", 2},
                            {"Shop #2", 1},
                            {"Shop #3", 3},
                            {"Shop #4", 1},
                        }
                    },
                },
                "Dummy"
            );

            var transportTask4 = new TransportTask<string>(
                new Dictionary<string, int>
                {
                    {"Storage #1", 80},
                    {"Storage #2", 60},
                    {"Storage #3", 30},
                    {"Storage #4", 60},
                },
                new Dictionary<string, int>
                {
                    {"Shop #1", 10},
                    {"Shop #2", 30},
                    {"Shop #3", 40},
                    {"Shop #4", 50},
                    {"Shop #5", 70},
                    {"Shop #6", 30}
                },
                new Dictionary<string, Dictionary<string, int>>
                {
                    {
                        "Storage #1", new Dictionary<string, int>
                        {
                            {"Shop #1", 3},
                            {"Shop #2", 20},
                            {"Shop #3", 8},
                            {"Shop #4", 13},
                            {"Shop #5", 4},
                            {"Shop #6", 100},
                        }
                    },
                    {
                        "Storage #2", new Dictionary<string, int>
                        {
                            {"Shop #1", 4},
                            {"Shop #2", 4},
                            {"Shop #3", 18},
                            {"Shop #4", 14},
                            {"Shop #5", 3},
                            {"Shop #6", 0},
                        }
                    },
                    {
                        "Storage #3", new Dictionary<string, int>
                        {
                            {"Shop #1", 10},
                            {"Shop #2", 4},
                            {"Shop #3", 18},
                            {"Shop #4", 8},
                            {"Shop #5", 6},
                            {"Shop #6", 0},
                        }
                    },
                    {
                        "Storage #4", new Dictionary<string, int>
                        {
                            {"Shop #1", 7},
                            {"Shop #2", 19},
                            {"Shop #3", 17},
                            {"Shop #4", 10},
                            {"Shop #5", 1},
                            {"Shop #6", 100},
                        }
                    },
                },
                "Dummy"
            );

            var transportTask5 = new TransportTask<string>(
                new Dictionary<string, int>
                {
                    {"Storage #1", 140},
                    {"Storage #2", 160},
                    {"Storage #3", 100},
                },
                new Dictionary<string, int>
                {
                    {"Shop #1", 80},
                    {"Shop #2", 40},
                    {"Shop #3", 150},
                    {"Shop #4", 130},
                },
                new Dictionary<string, Dictionary<string, int>>
                {
                    {
                        "Storage #1", new Dictionary<string, int>
                        {
                            {"Shop #1", 1},
                            {"Shop #2", 11},
                            {"Shop #3", 3},
                            {"Shop #4", 13},
                        }
                    },
                    {
                        "Storage #2", new Dictionary<string, int>
                        {
                            {"Shop #1", 12},
                            {"Shop #2", 4},
                            {"Shop #3", 8},
                            {"Shop #4", 2},
                        }
                    },
                    {
                        "Storage #3", new Dictionary<string, int>
                        {
                            {"Shop #1", 3},
                            {"Shop #2", 5},
                            {"Shop #3", 14},
                            {"Shop #4", 6},
                        }
                    }
                },
                "Dummy"
            );

            var result = transportTask.Solve();
            Console.WriteLine($"{nameof(transportTask)} = {result.functionValue}");
            result = transportTask2.Solve();
            Console.WriteLine($"{nameof(transportTask2)} = {result.functionValue}");
            result = transportTask3.Solve();
            Console.WriteLine($"{nameof(transportTask3)} = {result.functionValue}");
            result = transportTask4.Solve();
            Console.WriteLine($"{nameof(transportTask4)} = {result.functionValue}");
            result = transportTask5.Solve();
            Console.WriteLine($"{nameof(transportTask5)} = {result.functionValue}");
        }
    }
}
