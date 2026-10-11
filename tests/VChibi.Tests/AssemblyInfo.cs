using Xunit;

// Disable parallel test execution within the test assembly because multiple tests
// instantiate WPF Windows, Dispatcher timers, and Pack URI stream resources which
// share static process-wide state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
