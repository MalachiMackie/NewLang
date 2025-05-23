﻿namespace NewLang.Core;

public class TypeChecker
{
    public static void TypeCheck(LangProgram program)
    {
        var types = LangType.BuiltInTypes.ToDictionary(x => x.Name);

        foreach (var @class in program.Classes)
        {
            var name = GetIdentifierName(@class.Name);
            types.Add(name, new LangType
            {
                Name = name,
                GenericParameters = @class.TypeArguments.Select(GetIdentifierName).ToArray()
            });
        }

        var functionTypes = new Dictionary<string, FunctionType>();
        
        foreach (var fn in program.Functions)
        {
            functionTypes[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, functionTypes, types);
        }

        foreach (var fn in program.Functions)
        {
            TypeCheckFunctionBody(fn, variables: [], functionTypes, types);
        }

        var variables = new Dictionary<string, Variable>();

        foreach (var expression in program.Expressions)
        {
            TypeCheckExpression(expression, types, variables, functionTypes, expectedReturnType: null);
        }
    }

    private static void TypeCheckFunctionBody(LangFunction function,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        Dictionary<string, LangType> types)
    {
        var functionType = functions[function.Name.StringValue];

        var innerVariables = new Dictionary<string, Variable>(variables);
        foreach (var parameter in functionType.Parameters)
        {
            innerVariables[parameter.Name] = new Variable(
                parameter.Name,
                parameter.Type,
                Instantiated: true);
        }
        
        TypeCheckBlock(function.Block, innerVariables, types, functions, function.ReturnType is null ? null : GetType(function.ReturnType, types));
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static FunctionType TypeCheckFunctionSignature(LangFunction function, Dictionary<string, FunctionType> functions, Dictionary<string, LangType> types)
    {
        if (functions.ContainsKey(function.Name.StringValue))
        {
            throw new InvalidOperationException($"Function with name {function.Name.StringValue} already defined");
        }
        
        return new FunctionType
        {
            Name = function.Name.StringValue,
            ReturnType = function.ReturnType is null ? InstantiatedType.Unit : GetType(function.ReturnType, types),
            GenericParameters = function.TypeArguments.Select(x => x.StringValue).ToArray(),
            Parameters = function.Parameters.Select(x => new FunctionType.Parameter(x.Identifier.StringValue, GetType(x.Type, types))).ToArray()
        };
    }

    private static InstantiatedType TypeCheckBlock(
        Block block,
        Dictionary<string, Variable> variables,
        Dictionary<string, LangType> types,
        Dictionary<string, FunctionType> functions,
        InstantiatedType? expectedReturnType)
    {
        var innerVariables = new Dictionary<string, Variable>(variables);
        var innerFunctions = new Dictionary<string, FunctionType>(functions);
        
        foreach (var fn in block.Functions)
        {
            innerFunctions[fn.Name.StringValue] = TypeCheckFunctionSignature(fn, innerFunctions, types);
        }
        
        foreach (var fn in block.Functions)
        {
            TypeCheckFunctionBody(fn, innerVariables, innerFunctions, types);
        }

        foreach (var expression in block.Expressions)
        {
            TypeCheckExpression(expression, types, innerVariables, innerFunctions, expectedReturnType);
        }

        // todo: tail expressions
        return InstantiatedType.Unit;
    }

    private static InstantiatedType TypeCheckExpression(
        IExpression expression,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        InstantiatedType? expectedReturnType)
    {
        return expression switch
        {
            VariableDeclarationExpression variableDeclarationExpression => TypeCheckVariableDeclaration(
                variableDeclarationExpression, types, variables, functions, expectedReturnType),
            ValueAccessorExpression valueAccessorExpression => TypeCheckValueAccessor(valueAccessorExpression, variables, functions),
            MethodReturnExpression methodReturnExpression => TypeCheckMethodReturn(methodReturnExpression, types, variables, functions, expectedReturnType),
            MethodCallExpression methodCallExpression => TypeCheckMethodCall(methodCallExpression.MethodCall, types, variables, functions, expectedReturnType),
            BlockExpression blockExpression => TypeCheckBlock(blockExpression.Block, variables, types, functions, expectedReturnType),
            IfExpressionExpression ifExpressionExpression => TypeCheckIfExpression(ifExpressionExpression.IfExpression, types, variables, functions, expectedReturnType),
            _ => throw new NotImplementedException($"{expression.ExpressionType}")
        };
    }

    private static InstantiatedType TypeCheckIfExpression(IfExpression ifExpression,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        InstantiatedType? expectedReturnType)
    {
        var checkExpressionType =
            TypeCheckExpression(ifExpression.CheckExpression, types, variables, functions, expectedReturnType);
        if (checkExpressionType.Type != LangType.Boolean)
        {
            throw new InvalidOperationException("Expected bool");
        }

        TypeCheckExpression(ifExpression.Body, types, variables, functions, expectedReturnType);

        foreach (var elseIf in ifExpression.ElseIfs)
        {
            var elseIfCheckExpressionType
                = TypeCheckExpression(elseIf.CheckExpression, types, variables, functions, expectedReturnType);
            if (elseIfCheckExpressionType.Type != LangType.Boolean)
            {
                throw new InvalidOperationException("Expected bool");
            }

            TypeCheckExpression(elseIf.Body, types, variables, functions, expectedReturnType);
        }

        if (ifExpression.ElseBody is not null)
        {
            TypeCheckExpression(ifExpression.ElseBody, types, variables, functions, expectedReturnType);
        }
        
        // todo: tail expression
        return InstantiatedType.Unit;
    }

    private static InstantiatedType TypeCheckMethodCall(
        MethodCall methodCall,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        InstantiatedType? expectedReturnType)
    {
        var methodType = TypeCheckExpression(methodCall.Method, types, variables, functions, expectedReturnType);

        if (methodType.Type is not FunctionType functionType)
        {
            throw new InvalidOperationException($"{methodType} is not callable");
        }

        if (methodCall.ParameterList.Count != functionType.Parameters.Count)
        {
            throw new InvalidOperationException($"Expected {functionType.Parameters.Count} parameters, got {methodCall.ParameterList.Count}");
        }

        for (var i = 0; i < functionType.Parameters.Count; i++)
        {
            var expectedParameterType = functionType.Parameters[i].Type;
            var givenParameterType = TypeCheckExpression(methodCall.ParameterList[i], types, variables, functions, expectedParameterType);

            if (expectedParameterType != givenParameterType)
            {
                throw new InvalidOperationException(
                    $"Expected parameter type {expectedParameterType}, got {givenParameterType}");
            }
        }

        return functionType.ReturnType;
    }

    private static InstantiatedType TypeCheckMethodReturn(
        MethodReturnExpression methodReturnExpression,
        Dictionary<string, LangType> types,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        InstantiatedType? expectedReturnType)
    {
        var returnExpressionType = methodReturnExpression.MethodReturn.Expression is null
            ? null
            : TypeCheckExpression(methodReturnExpression.MethodReturn.Expression, types, variables, functions, expectedReturnType);
        
        if (expectedReturnType is null && returnExpressionType is not null)
        {
            throw new InvalidOperationException($"Expected void, got {returnExpressionType}");
        }

        if (expectedReturnType is not null && returnExpressionType is null)
        {
            throw new InvalidOperationException($"Expected {expectedReturnType}, got void");
        }

        if (expectedReturnType != returnExpressionType)
        {
            throw new InvalidOperationException($"Expected {returnExpressionType}, got {expectedReturnType}");
        }
        
        return InstantiatedType.Never;
    }

    private static InstantiatedType TypeCheckValueAccessor(ValueAccessorExpression valueAccessorExpression, Dictionary<string, Variable> variables, Dictionary<string, FunctionType> functions)
    {
        return valueAccessorExpression.ValueAccessor switch
        {
            {AccessType: ValueAccessType.Literal, Token: IntToken {Type: TokenType.IntLiteral}} => InstantiatedType.Int,
            {AccessType: ValueAccessType.Literal, Token: StringToken {Type: TokenType.StringLiteral}} => InstantiatedType.String,
            {AccessType: ValueAccessType.Literal, Token.Type: TokenType.True or TokenType.False } => InstantiatedType.Boolean,
            {AccessType: ValueAccessType.Variable, Token: StringToken {Type: TokenType.Identifier, StringValue: var variableName}} =>
                TypeCheckVariableAccess(variableName, variables, functions),
            _ => throw new NotImplementedException($"{valueAccessorExpression}")
        };
        
    }

    private static InstantiatedType TypeCheckVariableAccess(string variableName, Dictionary<string, Variable> variables, Dictionary<string, FunctionType> functions)
    {
        if (functions.TryGetValue(variableName, out var function))
        {
            return new InstantiatedType { Type = function, TypeArguments = [] };
        }
        
        if (!variables.TryGetValue(variableName, out var value))
        {
            throw new InvalidOperationException($"No symbol found with name {variableName}");
        }

        if (!value.Instantiated)
        {
            throw new InvalidOperationException($"{value.Name} is not instantiated");
        }

        return value.Type;
    }

    private record Variable(string Name, InstantiatedType Type, bool Instantiated);

    private static string GetIdentifierName(Token token)
    {
        return token is StringToken { Type: TokenType.Identifier } stringToken
            ? stringToken.StringValue
            : throw new InvalidOperationException("Expected token name");
    }

    private static InstantiatedType TypeCheckVariableDeclaration(
        VariableDeclarationExpression expression,
        Dictionary<string, LangType> resolvedTypes,
        Dictionary<string, Variable> variables,
        Dictionary<string, FunctionType> functions,
        InstantiatedType? expectedReturnType)
    {
        var varName = expression.VariableDeclaration.VariableNameToken.StringValue;
        if (variables.ContainsKey(varName))
        {
            throw new InvalidOperationException(
                $"Variable with name {varName} already exists");
        }

        switch (expression.VariableDeclaration)
        {
            case {Value: null, Type: null}:
                throw new InvalidOperationException("Variable declaration must have a type specifier or a value");
            case { Value: { } value, Type: var type} :
            {
                var valueType = TypeCheckExpression(value, resolvedTypes, variables, functions, expectedReturnType);
                if (type is not null)
                {
                    var expectedType = GetType(type, resolvedTypes);
                    if (expectedType != valueType)
                    {
                        throw new InvalidOperationException($"Expected type {expectedType}, but found {valueType}");
                    }
                }

                variables[varName] = new Variable(varName, valueType, Instantiated: true);

                break;
            }
            case { Value: null, Type: { } type }:
            {
                var langType = GetType(type, resolvedTypes);
                variables[varName] = new Variable(varName, langType, Instantiated: false);

                break;
            }
        }
        
        // variable declaration return type is always unit, regardless of the variable type
        return InstantiatedType.Unit;
    }

    private static InstantiatedType GetType(TypeIdentifier typeIdentifier, Dictionary<string, LangType> resolvedTypes)
    {
        if (typeIdentifier.Identifier.Type == TokenType.StringKeyword)
        {
            return InstantiatedType.String;
        }

        if (typeIdentifier.Identifier.Type == TokenType.IntKeyword)
        {
            return InstantiatedType.Int;
        }

        if (typeIdentifier.Identifier.Type == TokenType.Result)
        {
            if (typeIdentifier.TypeArguments.Count != 2)
            {
                throw new InvalidOperationException("Result expects 2 arguments");
            }
            
            return InstantiatedType.Result(
                GetType(typeIdentifier.TypeArguments[0], resolvedTypes),
                GetType(typeIdentifier.TypeArguments[1], resolvedTypes));
        }

        if (typeIdentifier.Identifier is StringToken { Type: TokenType.Identifier } stringToken
            && resolvedTypes.TryGetValue(stringToken.StringValue, out var nameMatchingType))
        {
            if (!nameMatchingType.IsGeneric && typeIdentifier.TypeArguments.Count == 0)
            {
                return new InstantiatedType{Type = nameMatchingType, TypeArguments = []};
            }
        }

        throw new InvalidOperationException($"No type found {typeIdentifier}");
    }

    public class InstantiatedType
    {
        public required LangType Type { get; init; }
        
        // todo: be consistent with argument/parameter
        public required Dictionary<string, InstantiatedType> TypeArguments { get; init; }

        public static InstantiatedType String { get; } = new() { Type = LangType.String, TypeArguments = [] };
        public static InstantiatedType Boolean { get; } = new() { Type = LangType.Boolean, TypeArguments = [] };
        
        public static InstantiatedType Int { get; } = new() { Type = LangType.Int, TypeArguments = [] };

        public static InstantiatedType Unit { get; } = new() { Type = LangType.Unit, TypeArguments = [] };
        
        public static InstantiatedType Never { get; } = new() {Type = LangType.Never, TypeArguments = [] };

        public static InstantiatedType Result(InstantiatedType value, InstantiatedType error) =>
            new()
            {
                Type = LangType.Result, TypeArguments = new Dictionary<string, InstantiatedType>
                {
                    {"TValue", value},
                    {"TError", error}
                }
            };
    }
    
    public class LangType
    {
        public required IReadOnlyList<string> GenericParameters { get; init; }
        public bool IsGeneric => GenericParameters.Count > 0;
        public required string Name { get; init; }

        public static LangType Unit { get; } = new() { GenericParameters = [], Name = "Unit" };
        public static LangType String { get; } = new() { GenericParameters = [], Name = "String" };
        public static LangType Int { get; } = new() { GenericParameters = [], Name = "Int" };
        public static LangType Boolean { get; } = new() { GenericParameters = [], Name = "Boolean" };
        public static LangType Never { get; } = new() { GenericParameters = [], Name = "!" };
        public static LangType Result { get; } = new() { GenericParameters = ["TValue", "TError"], Name = "Result" };
        public static IEnumerable<LangType> BuiltInTypes { get; } = [Unit, String, Int, Never, Result, Boolean];
    }

    public class FunctionType : LangType
    {
        public required IReadOnlyList<Parameter> Parameters { get; init; }
        
        // todo: figure this out. This both the class the fn is in and the fn itself could be generic
        public required InstantiatedType ReturnType { get; init; }

        public record Parameter(string Name, InstantiatedType Type);
    }
}