﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace NewLang.Core.Benchmarks;

[MemoryDiagnoser]
public class ParserBenchmarks
{
    [Params(SmallSource, MediumSource, LargeSource)]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    // ReSharper disable once UnassignedField.Global
    public string Source;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private readonly Consumer _consumer = new();
    private readonly Parser _parser = new();

    [Benchmark]
    public void BenchmarkParser()
    {
        _parser.Parse(Source).Consume(_consumer);
    }
    

    private const string SmallSource = "var a = 2;";

    private const string MediumSource = """
                                        pub fn DoSomething(a: int): result<int, string> {
                                            var b = 2;
                                            
                                            if (a > b) {
                                                return ok(a);
                                            }
                                            else if (a == b) {
                                                return ok(b);
                                            }
                                            
                                            return error("something wrong");
                                        }

                                        pub fn SomethingElse(a: int): result<int, string> {
                                            b = DoSomething(a)?;
                                            
                                            return b;
                                        }

                                        Println(DoSomething(5));
                                        Println(DoSomething(1));
                                        Println(SomethingElse(1));

                                        """;

    private const string LargeSource = $"""
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       {MediumSource}
                                       """;
}