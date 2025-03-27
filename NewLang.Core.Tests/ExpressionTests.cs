﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit.Abstractions;

// ReSharper disable PossibleMultipleEnumeration

namespace NewLang.Core.Tests;

public class ExpressionTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [MemberData(nameof(TestCases))]
    public void Tests(
        [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        string source,
        IEnumerable<Token> tokens,
        IEnumerable<Expression> expectedExpressions)
    {
        var expressions = ExpressionTreeBuilder.Build(tokens)
            // clear out the source spans, we don't actually care about them
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

    public static IEnumerable<object[]> TestCases()
    {
        return new (string Source, IEnumerable<Expression> ExpectedExpression)[]
        {
            // value access expressions
            ("a", [new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default)))]),
            ("1", [new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))]),
            ("\"my string\"", [new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("my string", default)))]),
            ("true", [new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))]),
            ("false", [new Expression(new ValueAccessor(ValueAccessType.Literal, Token.False(default)))]),
            // postfix unary operator
            ("a?", [new Expression(new UnaryOperator(
                UnaryOperatorType.FallOut,
                new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                Token.QuestionMark(default)))]),
            ("a??",
            [
                new Expression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        Token.QuestionMark(default))),
                    Token.QuestionMark(default))),
            ]),
            // binary operator expressions
            ("a < 5", [new Expression(new BinaryOperator(
                BinaryOperatorType.LessThan,
                new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(5, default))),
                Token.LeftAngleBracket(default)))]),
            ("\"thing\" > true", [new Expression(new BinaryOperator(
                BinaryOperatorType.GreaterThan,
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default))),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default))),
                Token.RightAngleBracket(default)))]),
            ("a + b", [new Expression(new BinaryOperator(
                BinaryOperatorType.Plus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Plus(default)))]),
            ("a - b", [new Expression(new BinaryOperator(
                BinaryOperatorType.Minus,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Dash(default)))]),
            ("a * b", [new Expression(new BinaryOperator(
                BinaryOperatorType.Multiply,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.Star(default)))]),
            ("a / b", [new Expression(new BinaryOperator(
                BinaryOperatorType.Divide,
                VariableAccessor("a"),
                VariableAccessor("b"),
                Token.ForwardSlash(default)))]),
            ("var a = b", [new Expression(new VariableDeclaration(Token.Identifier("a", default), VariableAccessor("b")))]),
            ("var a = 1", [new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.IntLiteral(1, default)))))]),
            ("var a = true", [new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.True(default)))))]),
            ("var a = \"thing\"", [new Expression(new VariableDeclaration(
                Token.Identifier("a", default),
                new Expression(new ValueAccessor(ValueAccessType.Literal, Token.StringLiteral("thing", default)))))]),
            // ____binding strength tests
            // __greater than
            ( // greater than
                "a > b > c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.RightAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // less than
                "a > b < c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.GreaterThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.RightAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // multiply
                "a > b * c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // divide
                "a > b / c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // plus
                "a > b + c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // minus
                "a > b - c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // fallOut
                "a > b?",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            // __Less than
            ( // greater than
                "a < b > c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.LeftAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // less than
                "a < b < c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.LessThan,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.LeftAngleBracket(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // multiply
                "a < b * c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // divide
                "a < b / c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // plus
                "a < b + c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Plus(default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // minus
                "a < b - c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Dash(default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // fallOut
                "a < b?",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            // __multiply
            ( // greater than
                "a * b > c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c"),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // less than
                "a * b < c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            VariableAccessor("a"),
                            VariableAccessor("b"),
                            Token.Star(default))),
                        VariableAccessor("c"),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // multiply
                "a * b * c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Star(default)))
                ]
            ),
            ( // divide
                "a * b / c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.ForwardSlash(default)))
                ]
            ),
            ( // plus
                "a * b + c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
                ]
            ),
            ( // minus
                "a * b - c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Star(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
                ]
            ),
            ( // fallOut
                "a * b?",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        VariableAccessor("a"),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("b"),
                            Token.QuestionMark(default))),
                        Token.Star(default)))
                ]
            ),
            // __divide
            ( // greater than
                "a / b > c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // less than
                "a / b < c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // multiply
                "a / b * c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Star(default)))
                ]
            ),
            ( // divide
                "a / b / c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.ForwardSlash(default)))
                ]
            ),
            ( // plus
                "a / b + c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
                ]
            ),
            ( // minus
                "a / b - c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.ForwardSlash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
                ]
            ),
            ( // fallOut
                "a / b?",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.ForwardSlash(default)))
                ]
            ),
            // __plus
            ( // greater than
                "a + b > c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // less than
                "a + b < c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // multiply
                "a + b * c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.Plus(default)))
                ]
            ),
            ( // divide
                "a + b / c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.Plus(default)))
                ]
            ),
            ( // plus
                "a + b + c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
                ]
            ),
            ( // minus
                "a + b - c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Plus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Plus(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
                ]
            ),
            ( // fallOut
                "a + b?",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.Plus(default)))
                ]
            ),
            // __minus
            ( // greater than
                "a - b > c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.RightAngleBracket(default)))
                ]
            ),
            ( // less than
                "a - b < c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.LessThan,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.LeftAngleBracket(default)))
                ]
            ),
            ( // multiply
                "a - b * c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Multiply,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.Star(default))),
                        Token.Dash(default)))
                ]
            ),
            ( // divide
                "a - b / c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Divide,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                            Token.ForwardSlash(default))),
                        Token.Dash(default)))
                ]
            ),
            ( // plus
                "a - b + c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Plus(default)))
                ]
            ),
            ( // minus
                "a - b - c",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new BinaryOperator(
                            BinaryOperatorType.Minus,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.Dash(default))),
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("c", default))),
                        Token.Dash(default)))
                ]
            ),
            ( // fallOut
                "a - b?",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("a", default))),
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            new Expression(new ValueAccessor(ValueAccessType.Variable, Token.Identifier("b", default))),
                            Token.QuestionMark(default))),
                        Token.Dash(default)))
                ]
            ),
            // __FallOut
            ( // fallout
                "a??",
                [new Expression(new UnaryOperator(
                    UnaryOperatorType.FallOut,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    Token.QuestionMark(default)))]),
            ( // less than
                "a? < b",
                [new Expression(new BinaryOperator(
                    BinaryOperatorType.LessThan,
                    new Expression(new UnaryOperator(
                        UnaryOperatorType.FallOut,
                        VariableAccessor("a"),
                        Token.QuestionMark(default))),
                    VariableAccessor("b"),
                    Token.LeftAngleBracket(default)))]
            ),
            ( // greater than
                "a? > b",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.GreaterThan,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.RightAngleBracket(default)))
                ]
            ),
            (// plus
                "a? + b",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Plus,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Plus(default)))
                ]
            ),
            ( // minus
                "a? - b",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Minus,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Dash(default)))
                ]
            ),
            ( // multiply
                "a? * b",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Multiply,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.Star(default)))
                ]
            ),
            ( // divide
                "a? / b",
                [
                    new Expression(new BinaryOperator(
                        BinaryOperatorType.Divide,
                        new Expression(new UnaryOperator(
                            UnaryOperatorType.FallOut,
                            VariableAccessor("a"),
                            Token.QuestionMark(default))),
                        VariableAccessor("b"),
                        Token.ForwardSlash(default)))
                ]
            ),
        }.Select(x => new object[] { x.Source, new Parser().Parse(x.Source), x.ExpectedExpression });
    }
    
    private static Expression VariableAccessor(string name) =>
        new (new ValueAccessor(ValueAccessType.Variable, Token.Identifier(name, default)));
    
    private static Expression RemoveSourceSpan(Expression expression)
    {
        return expression with
        {
            ValueAccessor = RemoveSourceSpan(expression.ValueAccessor),
            UnaryOperator = RemoveSourceSpan(expression.UnaryOperator),
            BinaryOperator = RemoveSourceSpan(expression.BinaryOperator),
            VariableDeclaration = RemoveSourceSpan(expression.VariableDeclaration)
        };
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