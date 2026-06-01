using Xunit;

// Integration tests spin real worker threads and several inspect process-wide resources
// (thread counts, the global worker counter, managed memory). Running the assembly's tests
// sequentially keeps those measurements reliable and avoids oversubscribing the CPU with many
// concurrent multi-worker runs. The fast unit-test assembly stays fully parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
