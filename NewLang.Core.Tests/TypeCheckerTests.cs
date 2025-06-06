﻿using FluentAssertions;

namespace NewLang.Core.Tests;

public class TypeCheckerTests
{
    [Theory]
    [MemberData(nameof(SuccessfulExpressionTestCases))]
    public void Should_SuccessfullyTypeCheckExpressions(string source)
    {
        var program = Parser.Parse(Tokenizer.Tokenize(source));
        var act = () => TypeChecker.TypeCheck(program);
        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(FailedExpressionTestCases))]
    public void Should_FailTypeChecking_When_ExpressionsAreNotValid(string source)
    {
        var program = Parser.Parse(Tokenizer.Tokenize(source));
        var act = () => TypeChecker.TypeCheck(program);

        act.Should().Throw<InvalidOperationException>();
    }

    public static TheoryData<string> SuccessfulExpressionTestCases() =>
        new()
        {
            "var a = 2",
            "var a: int = 2",
            "var b: string = \"somestring\"",
            "var a = 2; var b: int = a",
            "fn MyFn(): int { return 1; }",
            """
            fn MyFn<T>(param: T): T {return param;}
            var a: string = MyFn::<string>("");
            """,
            """
            class MyClass<T> {
                fn MyFn<T2>(param1: T, param2: T2) {
                }
            }
            
            var a = new MyClass::<int>{};
            a.MyFn::<string>(1, "");
            """,
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
            "fn Fn1<T1>(){} Fn1::<string>();",
            "fn Fn1<T1, T2>(){} Fn1::<string, int>();",
            """
            fn Fn1<T1>(param: T1): T1 { return param; }
            var a: string = Fn1::<string>("");
            var b: int = Fn1::<int>(1);
            """,
            "if (true) {}",
            "if (false) {}",
            "var a = true; if (a) {}",
            "if (true) {} else {}",
            "if (true) {var a = 2} else if (true) {var a = 3} else if (true) {var a = 4} else {var a = 5}",
            "if (true) var a = 2",
            "var a: result::<int, string>",
            """
            class Class1 { field someField: Class1;}
            class Class2 { }
            """,
            // binary operators
            // less than
            "var a: bool = 1 < 2;",
            // GreaterThan,
            "var a: bool = 2 > 2",
            // Plus,
            "var a: int = 2 + 2",
            // Minus,
            "var a: int = 2 - 2",
            // Multiply,
            "var a: int = 2 * 2",
            // Divide,
            "var a: int = 2 / 2",
            // EqualityCheck,
            "var a: bool = 2 == 2",
            // ValueAssignment,
            "var a = 2; a = 3",
            // Object Initializers
            """
            class MyClass {field myField: int; field otherField: string;}
            var a = new MyClass { myField = 1, otherField = "" };
            """,
            "class MyClass {} var a: MyClass = new MyClass {};",
            """
            class MyClass {field someField: int;}
            var a = new MyClass { someField = 1 };
            var b: int = a.someField;
            """,
            """
            class MyClass { static field someField: int = 3; }
            var a: int = MyClass::someField;
            """,
            """
            class MyClass<T> {
                fn MyFn(param: T): T {
                    return param;
                }
            }
            
            var a = new MyClass::<string>{};
            
            var b = a.MyFn;
            
            var c = b("");
            """,
            """
            class MyClass<T> { static field someField: int = 1; }
            var a = MyClass::<string>::someField;
            """,
            """
            class MyClass<T> { field someField: T; }
            var a = new MyClass::<int> {someField = 1};
            """,
            """
            class MyClass<T> { field someField: T; }
            var a = new MyClass::<string> {someField = ""};
            var b: string = a.someField;
            """,
            """
            class MyClass<T> {}
            class OtherClass<T> {}
            """,
            """
            class MyClass<T> {
                 fn MyFn<T2>() {
                 }
            }
            
            var a = new MyClass::<string>{};
            var b = a.MyFn::<int>();
            """,
            """
            fn OuterFn() {
               var a = new MyClass{}; 
               a.MyFn();
            }
            class MyClass {
                fn MyFn() {
                    OuterFn();
                }
            }
            """,
            """
            class MyClass {
                fn MyFn() {
                    var a = new MyClass{};
                }
            }
            """,
            """
            fn MyFn(): string {
                fn InnerFn(): int { return 1; }
                return "";
            }
            """,
            Mvp
        };

    public static TheoryData<string> FailedExpressionTestCases() =>
        new()
        {
            """
            class MyClass<T> {
                fn MyFn<T2>(param1: T, param2: T2) {
                }
            }
            
            var a = new MyClass::<int>{};
            a.MyFn::<string>("", 1);
            """,
            """
            class MyClass<T> {
                fn MyFn<T2>(param1: T, param2: T2) {
                }
            }
            
            var a = new MyClass::<int>{};
            a.MyFn::<string>("", "");
            """,
            """
            class MyClass<T> {
                fn MyFn<T2>(param1: T, param2: T2) {
                }
            }
            
            var a = new MyClass::<int>{};
            a.MyFn::<string>(1, 1);
            """,
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
            "var a = new MyClass::<int> {someField = true};",
            "fn MyFn(param1: string, param2: int) {} MyFn(3, \"value\");",
            "fn MyFn(param1: string, param2: int) {} MyFn();",
            "fn MyFn(param1: string, param2: int) {} MyFn(\"value\", 3, 2);",
            "fn MyFn<T1>() {var a = T1.something;}",
            "fn MyFn<T1>() {var a = T1::something;}",
            "fn MyFn<T1>(param: T1): int { return param; }",
            "fn MyFn(){} fn MyFn(){}",
            "fn MyFn<T, T>() {}",
            "if (1) {}",
            "if (true) {} else if (1) {}",
            "if (true) {var a: string = 1;}",
            "if (true) {} else if (true) {var a: string = 1}",
            "if (true) {} else if (true) {} else {var a: string = 1}",
            "if (true) {} else if (true) {} else if (true) {var a: string = 1}",
            "var a: result::<>",
            "var a: result::<string>",
            "var a: result::<string, string, string>",
            "fn Fn1<T1>(){} Fn1::<>();",
            "fn Fn1<T1>(){} Fn1::<string, bool>();",
            """
            class MyClass {
                field someField: string;
            }
            var a = new MyClass { someField = 1 };
            """,
            """
            class MyClass {
                field someField: string;
            }
            var a = new MyClass { someField = "value", someField = "value" };
            """,
            """
            class MyClass {
                field someField: string;
            }
            var a = new MyClass { someField = "value", extraField = 1 };
            """,
            """
            class MyClass {
                field someField: string;
            }
            var a = new MyClass {};
            """,
            "class MyClass { static field someField: string = 1; }",
            """
            class MyClass<T> {
                fn MyFn<T>(){}
            }
            """,
            "class MyClass<T, T>{}",
            """
            class MyClass{}
            class OtherClass<MyClass>{}
            """,
            """
            class MyClass {
                fn MyFn(): int { return ""; }
            }
            """,
            // binary operators
            // less than
            "var a = 1 < true;",
            "var a = true < 1;",
            "var a: int = 1 < 2",
            // GreaterThan,
            "var a = true > 1;",
            "var a = 2 > true",
            "var a: int = 2 > 2",
            // Plus,
            "var a = true + 1;",
            "var a = 2 + true",
            "var a: bool = 2 + 2",
            // Minus,
            "var a = true - 1;",
            "var a = 2 - true",
            "var a: bool = 2 - 2",
            // Multiply,
            "var a = true * 1;",
            "var a = 2 * true",
            "var a: bool = 2 * 2",
            // Divide,
            "var a = true / 1;",
            "var a = 2 / true",
            "var a: bool = 2 / 2",
            // EqualityCheck,
            "var a = true == 1;",
            "var a = 2 == true",
            "var a: int = 2 == 2",
            // ValueAssignment,
            "var a = 2; a = true",
            "true = false",
            // todo:
            // MemberAccess,
            """
            class MyClass { static field someField: int = 3; }
            var a: string = MyClass::someField;
            """,
            // StaticMemberAccess
            """
            class MyClass { field someField: int; }
            var a: MyClass = new MyClass { someField = 3 };
            var b: string = a.someField;
            """
        };

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
            pub static field FieldE: string = "something";
            pub static mut field FieldF: string = "something";
        }

        pub class GenericClass<T> {
            pub fn PublicMethod<T1>() {
            }
        }

        pub class Class2 {
            pub field A: string;
        }
        
        // todo: unions
        
        /*
        pub union MyUnion {
            A,
            B { field MyField: string; },
            
            fn SomeMethod() {
                var foo = switch (this) {
                    A => "",
                    B { MyField } => MyField
                };
                
                var bar = switch (this) {
                    A => 1,
                    B => 2
                }
            }
        }
        
        fn AnotherMethod(param: MyUnion) {
            if (param is MyUnion::A) {
            }
            else if (param is MyUnion::B { MyField }) {
            }
            
            var a = switch (param) {
                MyUnion::A => 1,
                MyUnion::B { MyField } => 2,
            };
        }
        
        var a = MyUnion::A;
        
        c = new MyUnion::B{ MyField = ""};
        
        */
        """;
}