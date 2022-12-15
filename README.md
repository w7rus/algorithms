# algorithms
Performance may be a bit worse than possible, Dictionary are used instead of Arrays (enables to use generic key)

## BFS/DFS
Those are done w/ relaxation, making those methods fastest at finding shortest path on very large graphs

## Dijkstra
Additionaly has implementation with Binary Heap (PriorityQueue) to find closest vertex faster

## Frog
typical dynamic programming task

## Kraskal
Finds acyclic fully-connected minimal length graph from the given one

## Max Flow
Finds maximum flow for each path found via BFS/DFS

## Transport Task
Solves tree (all possible N-1 combinations to set 0's on depleted resource allocations) of transport task plan degeneracy to find the best possible solution

Has transport task plan loop guard, skips solutions when reallocating resources eventually does a loop
