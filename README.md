# algorithms
Performance may be a bit worse than possible, Dictionary are used instead of Arrays (enables to use generic key)

## BFS/DFS
Finds shortest path from any vertex to any vertex
Implementation done with relaxation, making those methods fastest at finding shortest path

## Dijkstra
Finds shortest path from any vertex to any vertex
Additionaly has implementation with Binary Heap (PriorityQueue) to find closest vertex faster

## Frog
typical dynamic programming task

## Kraskal
Finds acyclic fully-connected minimal length graph from the given one

## Max Flow
Finds maximum flow for each path found between source and drain via BFS/DFS

## Transport Task
Finds minimal expences paths from providers to customers refilling required resource amount

Solves tree of transport task plan degeneracy fixes to find the best possible solution

Has transport task plan loop guard, skips solutions when reallocating resources eventually does a loop
