﻿using FluentAssertions;

namespace NewLang.Core.Tests;

public class TypeCheckerTests
{
    [Theory]
    [MemberData(nameof(SuccessfulExpressionTestCases))]
    public void Should_SuccessfullyTypeCheckExpressions(LangProgram program)
    {
        var act = () => TypeChecker.TypeCheck(program);
        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(FailedExpressionTestCases))]
    public void Should_FailTypeChecking_When_ExpressionsAreNotValid(LangProgram program)
    {
        var act = () => TypeChecker.TypeCheck(program);

        act.Should().Throw<InvalidOperationException>();
    }

    public static TheoryData<LangProgram> SuccessfulExpressionTestCases() =>
        ConvertToPrograms(
        [
            "var a = 2",
            "var a: int = 2",
            "var b: string = \"somestring\"",
            "var a = 2; var b: int = a",
            "fn MyFn(): int { return 1; }",
            """
            fn MyFn(){}
            MyFn();
            """,
            "var a = 2;{var b = a;}",
            "fn Fn1(){Fn2();} fn Fn2(){}",
            "fn MyFn() {fn InnerFn() {OuterFn();}} fn OuterFn() {}",
            "fn MyFn() {fn InnerFn() {} InnerFn();}",
            "fn MyFn(param: int) {var a: int = param;}",
            "fn MyFn(param1: string, param2: int) {} MyFn(\"value\", 3);",
            "fn MyFn(param: result::<string, int>) {}",
            "if (true) {}",
            "if (false) {}",
            "var a = true; if (a) {}",
            "if (true) {} else {}",
            "if (true) {var a = 2} else if (true) {var a = 3} else if (true) {var a = 4} else {var a = 5}",
            "if (true) var a = 2",
            Mvp
        ]);

    public static TheoryData<LangProgram> FailedExpressionTestCases() =>
        ConvertToPrograms([
            "var a: string = 2",
            "var a: int = \"somestring\"",
            "var b;",
            "fn MyFn(): int { return \"something\"; }",
            "fn MyFn() { return 1; }",
            "fn MyFn() {} fn MyFn() {}",
            "fn MyFn(): string { return; }",
            "var a = 2; var b: string = a",
            "var a: int; var b = a",
            "fn MyFn(){fn InnerFn() {}} InnerFn();",
            "CallMissingMethod();",
            "fn MyFn(param1: string, param2: int) {} MyFn(3, \"value\");",
            "fn MyFn(param1: string, param2: int) {} MyFn();",
            "fn MyFn(param1: string, param2: int) {} MyFn(\"value\", 3, 2);",
            "if (1) {}",
            "if (true) {} else if (1) {}",
            "if (true) {var a: string = 1}",
            "if (true) {} else if (true) {var a: string = 1}",
            "if (true) {} else if (true) {} else {var a: string = 1}",
            "if (true) {} else if (true) {} else if (true) {var a: string = 1}"
        ]);

    private static TheoryData<LangProgram> ConvertToPrograms(IEnumerable<string> input)
    {
        var theoryData = new TheoryData<LangProgram>();
        foreach (var program in input.Select(Tokenizer.Tokenize).Select(Parser.Parse))
        {
            theoryData.Add(program);
        }

        return theoryData;
    }

    private const string Mvp =
        """
        pub fn DoSomething(a: int): result::<int, string> {
            var b: int = 2;
            
            if (a > b) {
                return ok(a);
            }
            else if (a == b) {
                return ok(b);
            }
        
            b = 3;
        
            var thing = new Class2 {
                A = 3
            };
        
            MyClass::StaticMethod();
        
            PrivateFn::<string>();
        
            return error("something wrong");
        }
        
        fn PrivateFn<T>() {
            Println("Message");
        }
        
        pub fn SomethingElse(a: int): result::<int, string> {
            var b = DoSomething(a)?;
            var mut c = 2;
            
            return b;
        }
        
        Println(DoSomething(5));
        Println(DoSomething(1));
        Println(SomethingElse(1));
        
        pub class MyClass {
            pub fn PublicMethod() {
            }
        
            pub static fn StaticMethod() {
        
            }
            
            field FieldA: string;
            mut field FieldB: string;
            pub mut field FieldC: string;
            pub field FieldD: string;
            pub static field FieldE: string;
        }
        
        pub class GenericClass<T> {
            pub fn PublicMethod<T1>() {
            }
        }
        
        pub class Class2 {
            pub field A: string;
        }
        """;
}