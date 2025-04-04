﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit.Abstractions;

// ReSharper disable PossibleMultipleEnumeration

namespace NewLang.Core.Tests;

public class ExpressionTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [MemberData(nameof(FailTestCases))]
    public void FailTests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens)
    {
        var act = () => ExpressionTreeBuilder.Build(tokens).Count();

        act.Should().Throw<InvalidOperationException>();
    }
    
    [Theory]
    [MemberData(nameof(TestCases))]
    public void Tests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        Expression expectedExpression)
    {
        var result = ExpressionTreeBuilder.PopExpression(tokens);
        result.Should().NotBeNull();
        
        // clear out the source spans, we don't actually care about them
        var expression = RemoveSourceSpan(result.Value);

        try
        {
            expression.Should().BeEquivalentTo(expectedExpression, opts => opts.AllowingInfiniteRecursion());
        }
        catch
        {
            testOutputHelper.WriteLine("Expected {0}, found {1}", expectedExpression, expression);
            throw;
        }
    }
    
    [Theory]
    [MemberData(nameof(SingleTestCase))]
    public void SingleTest(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        Expression expectedExpression)
    {
        var result = ExpressionTreeBuilder.PopExpression(tokens);
        result.Should().NotBeNull();
        
        // clear out the source spans, we don't actually care about them
        var expression = RemoveSourceSpan(result.Value);

        try
        {
            expression.Should().BeEquivalentTo(expectedExpression, opts => opts.AllowingInfiniteRecursion());
        }
        catch
        {
            testOutputHelper.WriteLine("Expected {0}, found {1}", expectedExpression, expression);
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(MultiStatementTestCases))]
    public void MultiStatementTests([SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        IEnumerable<Expression> expectedExpressions)
    {
         var expressions = ExpressionTreeBuilder.Build(tokens)
             .Select(RemoveSourceSpan);
 
         try
         {
             expressions.Should().BeEquivalentTo(expectedExpressions, opts => opts.AllowingInfiniteRecursion());
         }
         catch
         {
             testOutputHelper.WriteLine("Expected [{0}], found [{1}]", string.Join(",", expectedExpressions), string.Join(",", expressions));
             throw;
         }       
    }

    public static IEnumerable<object[]> MultiStatementTestCases()
    {
        return new (string Source, IEnumerable<Expression> ExpectedExpression)[]
        {
            ("var a = 1;var b = 2;", [
                new Expression(new VariableDeclaration(Token.Identifier("a", default), new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new Expression(new VariableDeclaration(Token.Identifier("b", default), new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                ]),
        }.Select(x => new object[] { x.Source, new Parser().Parse(x.Source), x.ExpectedExpression });
    }
    
    public static IEnumerable<object[]> FailTestCases()
    {
        return new[]
        {
            // missing variable declaration value 
            "var a = ",
            // missing variable declaration equals
            "var a ",
            // missing variable declaration name
            "var",
            // missing if pieces
            "if {}",
            "if () {}",
            "if (a {}",
            "if (a)",
            "if",
            // else without else body
            "if (a) {} else",
            // else if without check expression
            "if (a) {} else if",
            "if (a) {} else if (",
            "if (a) {} else if ()",
            // else if without body
            "if (a) {} else if (a)",
            // else without body
            "if (a) {} else if (a) {} else",
            // else before else if
            "if (a) {} else {} else if (a) {}",
            // expression after if else expression
            "if (a) {} else {} var a = 1",
            "{} var a = 1",
            "if (a;) {}",
            // body has tail expression, but else doesn't
            
            /*
             todo: these are type checking errors, because {} is a valid expression that returns unit
            "if (a) {a} else {}",
            "if (a) {} else {a}",
            "if (a) {var b = 1;} else {a}",
            "if (a) {if (b) {var c = 1;}} else {1}",
            "if (a) {if (b) {1} else {2}} else {}",
            */
            // missing semicolon,
            "{var a = 1 var b = 2;}",
            "{",
            "}",
            "var a = 2",
            "var a = 2; var b = 2",
            "?",
            "+",
            ">",
            "<",
            "*",
            "/",
            "-",
            // invalid statement
            "a;",
            "{a;}"
        }.Select(x => new object[] { x, new Parser().Parse(x) });
    }

    public static IEnumerable<object[]> SingleTestCase()
    {
        return new (string Source, Expression ExpectedExpression)[]
        {
            ("if (a) {b} else {c}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([VariableAccessor("b")])),
                [],
                new Expression(new Block([VariableAccessor("c")]))))),
        }.Select(x => new object[] { x.Source, new Parser().Parse(x.Source), x.ExpectedExpression });
    }

    public static IEnumerable<object[]> TestCases()
    {
        return new (string Source, Expression ExpectedExpression)[]
        {
            // value access expressions
            ("a", new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default)))),
            ("1", new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))),
            ("\"my string\"", new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("my string", default)))),
            ("true", new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))),
            ("false", new Expression(new ValueAccessor(ValueAccessType.Literal, Token.False(default)))),
            // postfix unary operator
            ("a?", new Expression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                Token.QuestionMark(default)))),
            ("a??",
                new Expression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        Token.QuestionMark(default))),
                    Token.QuestionMark(default)))
            ),
            // binary operator expressions
            ("a < 5", new Expression(new BinaryOperator(
                BinaryOperatorType.LessThan,
                new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(5, default))),
                Token.LeftAngleBracket(default)))),
            ("\"thing\" > true", new Expression(new BinaryOperator(
                BinaryOperatorType.GreaterThan,
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default))),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default))),
                Token.RightAngleBracket(default)))),
            ("a + b", new Expression(new BinaryOperator(
                BinaryOperatorType.Plus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Plus(default)))),
            ("a - b", new Expression(new BinaryOperator(
                BinaryOperatorType.Minus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Dash(default)))),
            ("a * b", new Expression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Star(default)))),
            ("a / b", new Expression(new BinaryOperator(
                BinaryOperatorType.Divide,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.ForwardSlash(default)))),
            ("var a = b", new Expression(new VariableDeclaration(Token.Identifier("a", default), VariableAccessor("b")))),
            ("var a = 1", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))),
            ("var a = true", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))))),
            ("var a = \"thing\"", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default)))))),
            ("{}", new Expression(new Block([]))),
            ("{var a = 1;}", new Expression(new Block([
                new Expression(new VariableDeclaration(Token.Identifier("a", default), new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                ]))),
            // tail expression
            ("{var a = 1}", new Expression(new Block(
                [
                new Expression(new VariableDeclaration(Token.Identifier("a", default), new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))]))),
            // tail expression
            ("{var a = 1;var b = 2}", new Expression(new Block(
                [new Expression(new VariableDeclaration(Token.Identifier("a", default), new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new Expression(new VariableDeclaration(Token.Identifier("b", default), new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))]))),
            ("{var a = 1; var b = 2;}", new Expression(new Block([
                new Expression(new VariableDeclaration(Token.Identifier("a", default), new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))))),
                new Expression(new VariableDeclaration(Token.Identifier("b", default), new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                ]))),
            ("if (a) var c = 2;", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new VariableDeclaration(
                    Token.Identifier("c", default),
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))), [], null))),
            ("if (a > b) {var c = \"value\";}", new Expression(new IfExpression(
                new Expression(new BinaryOperator(
                    BinaryOperatorType.GreaterThan,
                    VariableAccessor("a"),
                    VariableAccessor("b"),
                    Token.RightAngleBracket(default))),
                new Expression(new Block([
                    new Expression(new VariableDeclaration(
                        Token.Identifier("c", default),
                        new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("value", default)))))
                ])), [], null))),
            ("if (a) {} else {var b = 2;}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([])),
                [],
                new Expression(new Block([
                    new Expression(new VariableDeclaration(
                        Token.Identifier("b", default),
                        new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))
                ]))))),
            ("if (a) {} else if (b) {}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([])),
                [new ElseIf(VariableAccessor("b"), new Expression(new Block([])))],
                null))),
            ("if (a) {} else if (b) {} else {}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([])),
                [
                    new ElseIf(VariableAccessor("b"), new Expression(new Block([]))),
                ],
                new Expression(new Block([]))))),
            ("if (a) {} else if (b) {} else if (c) {} else {}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([])),
                [
                    new ElseIf(VariableAccessor("b"), new Expression(new Block([]))),
                    new ElseIf(VariableAccessor("c"), new Expression(new Block([]))),
                ],
                new Expression(new Block([]))))),
            ("if (a) {b} else {c}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([VariableAccessor("b")])),
                [],
                new Expression(new Block([VariableAccessor("c")]))))),
            ("if (a) b else c", new Expression(new IfExpression(
                VariableAccessor("a"),
                VariableAccessor("b"),
                [],
                VariableAccessor("c")))),
            ("if (a) {if (b) {1} else {2}} else {3}", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new Block([new Expression(new IfExpression(
                    VariableAccessor("b"),
                    new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))])),
                    [],
                    new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))]))))])),
                [],
                new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, default)))]))))),
            ("if (a) if (b) 1 else 2 else 3", new Expression(new IfExpression(
                VariableAccessor("a"),
                new Expression(new IfExpression(
                    VariableAccessor("b"),
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))),
                    [],
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default))))),
                [],
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(3, default)))))),
            // todo: how to deal with 2 having a semicolon. the semicolon is actually for the whole statement
            ("var a = if (b) 1 else 2;", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new Expression(new IfExpression(
                    VariableAccessor("b"),
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default))),
                    [],
                    new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))))))),
            ("var a = if (b) {1} else {2};", new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new Expression(new IfExpression(
                    VariableAccessor("b"),
                    new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))])),
                    [],
                    new Expression(new Block([new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(2, default)))]))))))),
            // ____binding strength tests
            // __greater than
            ( // greater than
                "a > b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.RightAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a > b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.RightAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a > b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // divide
                "a > b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // plus
                "a > b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // minus
                "a > b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // fallOut
                "a > b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.RightAngleBracket(default)))
            ),
            // __Less than
            ( // greater than
                "a < b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.LeftAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a < b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.LeftAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a < b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // divide
                "a < b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // plus
                "a < b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // minus
                "a < b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // fallOut
                "a < b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.LeftAngleBracket(default)))
            ),
            // __multiply
            ( // greater than
                "a * b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c"),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a * b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c"),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a * b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Star(default)))
            ),
            ( // divide
                "a * b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.ForwardSlash(default)))
            ),
            ( // plus
                "a * b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a * b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a * b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("b"),
                            Token.QuestionMark(default))),
                        Token.Star(default)))
            ),
            // __divide
            ( // greater than
                "a / b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a / b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a / b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Star(default)))
            ),
            ( // divide
                "a / b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.ForwardSlash(default)))
            ),
            ( // plus
                "a / b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a / b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a / b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.ForwardSlash(default)))
            ),
            // __plus
            ( // greater than
                "a + b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a + b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a + b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.Plus(default)))
            ),
            ( // divide
                "a + b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.Plus(default)))
            ),
            ( // plus
                "a + b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a + b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a + b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.Plus(default)))
            ),
            // __minus
            ( // greater than
                "a - b > c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
            ),
            ( // less than
                "a - b < c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
            ),
            ( // multiply
                "a - b * c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.Dash(default)))
            ),
            ( // divide
                "a - b / c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.Dash(default)))
            ),
            ( // plus
                "a - b + c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
            ),
            ( // minus
                "a - b - c",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
            ),
            ( // fallOut
                "a - b?",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.Dash(default)))
            ),
            // __FallOut
            ( // fallout
                "a??",
                    new Expression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    Token.QuestionMark(default)))
                ),
            ( // less than
                "a? < b",
                    new Expression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    VariableAccessor("b"),
                    Token.LeftAngleBracket(default)))
            ),
            ( // greater than
                "a? > b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(default)))
            ),
            (// plus
                "a? + b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Plus(default)))
            ),
            ( // minus
                "a? - b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Dash(default)))
            ),
            ( // multiply
                "a? * b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Star(default)))
            ),
            ( // divide
                "a? / b",
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.ForwardSlash(default)))
            ),
        }.Select(x => new object[] { x.Source, new Parser().Parse(x.Source), x.ExpectedExpression });
    }
    
    private static Expression VariableAccessor(string name) =>
        new (new ValueAccessor(ValueAccessType.Variable, Token.Identifier(name, default)));
    
    private static Expression? RemoveSourceSpan(Expression? expression)
    {
        if (expression is null)
        {
            return null;
        }
        
        return expression.Value with
        {
            ValueAccessor = RemoveSourceSpan(expression.Value.ValueAccessor),
            UnaryOperator = RemoveSourceSpan(expression.Value.UnaryOperator),
            BinaryOperator = RemoveSourceSpan(expression.Value.BinaryOperator),
            VariableDeclaration = RemoveSourceSpan(expression.Value.VariableDeclaration),
            Block = RemoveSourceSpan(expression.Value.Block),
            IfExpression = RemoveSourceSpan(expression.Value.IfExpression)
        };
    }
    
    private static Expression RemoveSourceSpan(Expression expression)
    {
        return expression with
        {
            ValueAccessor = RemoveSourceSpan(expression.ValueAccessor),
            UnaryOperator = RemoveSourceSpan(expression.UnaryOperator),
            BinaryOperator = RemoveSourceSpan(expression.BinaryOperator),
            VariableDeclaration = RemoveSourceSpan(expression.VariableDeclaration),
            Block = RemoveSourceSpan(expression.Block),
            IfExpression = RemoveSourceSpan(expression.IfExpression)
        };
    }

    private static StrongBox<Block>? RemoveSourceSpan(StrongBox<Block>? block)
    {
        return block is null ? null : new StrongBox<Block>(new Block(block.Value.Expressions.Select(RemoveSourceSpan).ToArray()));
    }

    private static StrongBox<IfExpression>? RemoveSourceSpan(StrongBox<IfExpression>? ifExpression)
    {
        return ifExpression is null ? null : new StrongBox<IfExpression>(new IfExpression(
            CheckExpression: RemoveSourceSpan(ifExpression.Value.CheckExpression),
            Body: RemoveSourceSpan(ifExpression.Value.Body),
            ElseIfs: ifExpression.Value.ElseIfs.Select(RemoveSourceSpan).ToArray(),
            ElseBody: RemoveSourceSpan(ifExpression.Value.ElseBody)));
    }

    private static ElseIf RemoveSourceSpan(ElseIf elseIf)
    {
        return new ElseIf(RemoveSourceSpan(elseIf.CheckExpression), RemoveSourceSpan(elseIf.Body));
    }
    
    private static StrongBox<VariableDeclaration>? RemoveSourceSpan(StrongBox<VariableDeclaration>? variableDeclaration)
    {
        return variableDeclaration is null ? null
            : new StrongBox<VariableDeclaration>(
                new VariableDeclaration(
                    VariableNameToken: RemoveSourceSpan(variableDeclaration.Value.VariableNameToken),
                    Value: RemoveSourceSpan(variableDeclaration.Value.Value)));
    }

    private static StrongBox<BinaryOperator>? RemoveSourceSpan(StrongBox<BinaryOperator>? binaryOperator)
    {
        return binaryOperator is not null
            ? new StrongBox<BinaryOperator>(binaryOperator.Value with
            {
                Left = RemoveSourceSpan(binaryOperator.Value.Left),
                Right = RemoveSourceSpan(binaryOperator.Value.Right),
                OperatorToken = RemoveSourceSpan(binaryOperator.Value.OperatorToken)
            })
            : null;
    }

    private static StrongBox<UnaryOperator>? RemoveSourceSpan(StrongBox<UnaryOperator>? unaryOperator)
    {
        return unaryOperator is not null
            ? new StrongBox<UnaryOperator>(unaryOperator.Value with
            {
                OperatorToken = RemoveSourceSpan(unaryOperator.Value.OperatorToken),
                Operand = RemoveSourceSpan(unaryOperator.Value.Operand)
            })
            : null;
    }

    private static ValueAccessor? RemoveSourceSpan(ValueAccessor? valueAccessor)
    {
        return valueAccessor.HasValue
            ? valueAccessor.Value with { Token = RemoveSourceSpan(valueAccessor.Value.Token) }
            : null;
    }

    private static Token RemoveSourceSpan(Token token)
    {
        return token with { SourceSpan = default };
    }
}