using Xunit;

// These tests start real SQL Server and Kafka containers. Running classes in
// parallel makes them compete for CPU and IO, which showed up as timing-based
// flakiness rather than honest failures, so the assembly runs sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
