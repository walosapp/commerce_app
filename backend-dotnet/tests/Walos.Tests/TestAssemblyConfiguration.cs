using Xunit;

// Integration test classes share WALOS_TEST_CONNECTION and mutate the same schemas.
// Serializing test classes prevents cross-class lock/cleanup interference. Explicit
// Task.WhenAll concurrency scenarios inside an individual test remain concurrent.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
