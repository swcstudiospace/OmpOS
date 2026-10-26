// The fake executables are written and started by the tests. Running them one at a time keeps a concurrent fork from
// holding a script open for writing, which would make its exec fail with "text file busy".
[assembly: Xunit.v3.ParallelizationAttribute(Mode = Xunit.Sdk.ParallelMode.None)]
