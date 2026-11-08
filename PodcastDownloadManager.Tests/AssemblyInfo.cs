using Xunit;

// ProgramConfiguration holds the pdlm paths in static state, so test classes must not run in
// parallel: each test points them at its own temporary directory.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
