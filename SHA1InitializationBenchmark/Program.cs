using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using SHA1InitializationBenchmark;

IConfig config = new DebugInProcessConfig();

BenchmarkRunner.Run<SHA1InitializationBenchmarks>(config);