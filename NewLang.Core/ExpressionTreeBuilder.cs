﻿using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public static class ExpressionTreeBuilder
{
    public static IEnumerable<Expression> Build(IEnumerable<Token> tokens)
    {
        using var tokensEnumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());
        while (tokensEnumerator.TryPeek(out _))
        {
            var expression = PopExpression(tokensEnumerator);
            if (expression.HasValue)
            {
                yield return expression.Value;
            }
        }
    }

    private static Expression? MatchTokenToExpression(Token token, Expression? previousExpression, PeekableEnumerator<Token> tokens)
    {
        return token.Type switch
        {
            // value accessors
            TokenType.Identifier => new Expression(new ValueAccessor(ValueAccessType.Variable, token)),
            TokenType.StringLiteral or TokenType.IntLiteral or TokenType.True or TokenType.False => new Expression(
                new ValueAccessor(ValueAccessType.Literal, token)),
            // unary operators
            TokenType.QuestionMark => GetUnaryOperatorExpression(previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token, UnaryOperatorType.FallOut),
            // binary operator tokens
            TokenType.LeftAngleBracket => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token,
                BinaryOperatorType.LessThan),
            TokenType.RightAngleBracket => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token,
                BinaryOperatorType.GreaterThan),
            TokenType.Plus => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token, BinaryOperatorType.Plus),
            TokenType.Dash => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token, BinaryOperatorType.Minus),
            TokenType.Star => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token, BinaryOperatorType.Multiply),
            TokenType.ForwardSlash => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token,
                BinaryOperatorType.Divide),
            TokenType.Var => GetVariableDeclaration(tokens),
            TokenType.Semicolon => null,
            TokenType.LeftBrace => GetBlockExpression(tokens),
            TokenType.If => GetIfExpression(tokens),
            _ => throw new InvalidOperationException($"Token type {token.Type} not supported")
        };
    }

    private static Expression? PopExpression(PeekableEnumerator<Token> tokens, uint? currentBindingStrength = null)
    {
        Expression? previousExpression = null;
        while (true)
        {
            if (!tokens.MoveNext())
            {
                return null;
            }

            var expression = MatchTokenToExpression(tokens.Current, previousExpression, tokens);
            if (expression is null)
            {
                return null;
            }

            // if we cannot bind to the next expression, return our current expression
            if (!tokens.TryPeek(out var peeked)
                || !TryGetBindingStrength(peeked, out var bindingStrength)
                || bindingStrength <= currentBindingStrength)
            {
                return expression;
            }
            
            // bind to the next expression
            previousExpression = expression.Value;
            currentBindingStrength = null;
        }
    }

    private static Expression GetIfExpression(PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected left parenthesis, found nothing");
        }

        if (tokens.Current.Type != TokenType.LeftParenthesis)
        {
            throw new InvalidOperationException($"Expected left parenthesis, found {tokens.Current.Type}");
        }

        var checkExpression = PopExpression(tokens);
        if (checkExpression is null)
        {
            throw new InvalidOperationException("Expected check expression, found no expression");
        }

        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected right parenthesis, found nothing");
        }

        if (tokens.Current.Type != TokenType.RightParenthesis)
        {
            throw new InvalidOperationException($"Expected right parenthesis, found {tokens.Current.Type}");
        }

        var body = PopExpression(tokens);
        if (body is null)
        {
            throw new InvalidCastException("Expected if body, found nothing");
        }

        return new Expression(new IfExpression(checkExpression.Value, body.Value));
    }

    private static Expression GetBlockExpression(PeekableEnumerator<Token> tokens)
    {
        var blockExpressions = new List<Expression>();
        
        while (tokens.TryPeek(out var peeked))
        {
            if (peeked.Type == TokenType.RightBrace)
            {
                // pop the peeked right brace off
                tokens.MoveNext();
                return new Expression(new Block(blockExpressions));
            }

            var expression = PopExpression(tokens);
            if (expression.HasValue)
            {
                blockExpressions.Add(expression.Value);
            }
        }

        throw new InvalidOperationException("Expected Right brace, got nothing");
    }

    private static Expression GetVariableDeclaration(PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected variable identifier, got nothing");
        }

        var identifier = tokens.Current;
        if (identifier.Type != TokenType.Identifier)
        {
            throw new InvalidOperationException($"Expected variable identifier, got {identifier}");
        }

        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected equals tokens, got nothing");
        }

        if (tokens.Current.Type != TokenType.Equals)
        {
            throw new InvalidOperationException($"Expected equals token, got {tokens.Current}");
        }

        var valueExpression = PopExpression(tokens);
        if (valueExpression is null)
        {
            throw new InvalidOperationException("Expected value expression, got nothing");
        }

        return new Expression(new VariableDeclaration(identifier, valueExpression.Value));
    }
    
    private static Expression GetUnaryOperatorExpression(
        Expression operand,
        Token operatorToken,
        UnaryOperatorType operatorType)
    {
        return new Expression(new UnaryOperator(operatorType, operand, operatorToken));
    }
    
    private static Expression GetBinaryOperatorExpression(
        PeekableEnumerator<Token> tokens,
        Expression leftOperand,
        Token operatorToken,
        BinaryOperatorType operatorType)
    {
        if (!TryGetBindingStrength(operatorToken, out var bindingStrength))
        {
            throw new UnreachableException("All operators have a binding strength");
        }
        var right = PopExpression(tokens, bindingStrength.Value);
        if (right is null)
        {
            throw new Exception("Expected expression but got null");
        }

        return new Expression(new BinaryOperator(operatorType, leftOperand, right.Value, operatorToken));
    }

    private static bool TryGetBindingStrength(Token token, [NotNullWhen(true)] out uint? bindingStrength)
    {
        (UnaryOperatorType? unaryOperatorType, BinaryOperatorType? binaryOperatorType) operators = token.Type switch
        {
            // unary operators
            TokenType.QuestionMark => (UnaryOperatorType.FallOut, null),
            // binary operators
            TokenType.RightAngleBracket => (null, BinaryOperatorType.GreaterThan),
            TokenType.LeftAngleBracket => (null, BinaryOperatorType.LessThan),
            TokenType.Star => (null, BinaryOperatorType.Multiply),
            TokenType.ForwardSlash => (null, BinaryOperatorType.Divide),
            TokenType.Plus => (null, BinaryOperatorType.Plus),
            TokenType.Dash => (null, BinaryOperatorType.Minus),
            _ => (null, null)
        };

        if (operators.unaryOperatorType.HasValue)
        {
            bindingStrength = UnaryOperatorBindingStrengths[operators.unaryOperatorType.Value];
            return true;
        }

        if (operators.binaryOperatorType.HasValue)
        {
            bindingStrength = BinaryOperatorBindingStrengths[operators.binaryOperatorType.Value];
            return true;
        }
        
        bindingStrength = null;
        return false;
    }
    
    private static readonly FrozenDictionary<BinaryOperatorType, uint> BinaryOperatorBindingStrengths =
        new Dictionary<BinaryOperatorType, uint>
        {
            { BinaryOperatorType.Multiply, 3 },
            { BinaryOperatorType.Divide, 3 },
            { BinaryOperatorType.Plus, 2 },
            { BinaryOperatorType.Minus, 2 },
            { BinaryOperatorType.GreaterThan, 1 },
            { BinaryOperatorType.LessThan, 1 },
        }.ToFrozenDictionary();
    
    private static readonly FrozenDictionary<UnaryOperatorType, uint> UnaryOperatorBindingStrengths =
        new Dictionary<UnaryOperatorType, uint>
        {
            { UnaryOperatorType.FallOut, 4 },
        }.ToFrozenDictionary();
}