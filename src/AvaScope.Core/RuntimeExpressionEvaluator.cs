using System.Globalization;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

/// <summary>The same closed evaluator serves scalar queries, assertions and workflow waits.</summary>
public static class RuntimeExpressionEvaluator
{
    public static RuntimeExpressionResult Evaluate(RuntimeExpressionDefinition definition,
        IReadOnlyList<RuntimeExpressionSourceObservation> sources) => EvaluateNode(definition.Expression, "$", sources);

    private static RuntimeExpressionResult EvaluateNode(RuntimeExpression expression, string path,
        IReadOnlyList<RuntimeExpressionSourceObservation>? sources = null)
    {
        var operands = expression.Operands.Select((node, index) => EvaluateNode(node, path + ".operands[" + index + "]", sources)).ToArray();
        RuntimeExpressionResult Unknown(string reason) => new(path, expression.Kind, "indeterminate", "unavailable", null, expression.Source, reason, operands);
        RuntimeExpressionResult Value(object value)
        {
            var json = value is JsonElement element ? element.Clone() : JsonSerializer.SerializeToElement(value);
            if (json.ValueKind == JsonValueKind.Number && !json.TryGetDecimal(out _)) return Unknown("decimal_out_of_range");
            var type = json.ValueKind is JsonValueKind.True or JsonValueKind.False ? "boolean" : json.ValueKind == JsonValueKind.Number ? "number" : "string";
            return new(path, expression.Kind, "available", type, json, expression.Source, null, operands);
        }
        if (operands.Any(operand => operand.Status != "available")) return Unknown("operand_indeterminate");
        try
        {
            if (expression.Kind == "literal") return Value(expression.Literal!.Value);
            if (expression.Source is { } id)
            {
                var source = sources?.SingleOrDefault(candidate => candidate.Id == id);
                if (source is null) return Unknown("source_missing");
                if (!source.Coverage.Complete) return Unknown("source_incomplete:" + string.Join(",", source.Coverage.Reasons));
                if (source.Values.Any(value => value.Observation.Status != "present" || value.Observation.Value is null)) return Unknown("source_value_unavailable");
                var values = source.Values.Select(value => value.Observation.Value!.Value).ToArray();
                if (expression.Kind == "count") return Value(values.Length);
                if (expression.Kind == "value") return values.Length == 1 ? Value(values[0]) : Unknown(values.Length == 0 ? "source_empty" : "source_ambiguous");
                if (expression.Kind == "count_true")
                    return values.All(IsBoolean) ? Value(values.Count(value => value.GetBoolean())) : Unknown("boolean_required");
                if (values.Any(value => value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out _))) return Unknown("number_required");
                var numbers = values.Select(value => value.GetDecimal()).ToArray();
                return expression.Kind switch
                {
                    "sum" => Value(numbers.Aggregate(0m, (sum, value) => checked(sum + value))),
                    "minimum" when numbers.Length > 0 => Value(numbers.Min()),
                    "maximum" when numbers.Length > 0 => Value(numbers.Max()),
                    _ => Unknown("source_empty")
                };
            }
            var args = operands.Select(operand => operand.Value!.Value).ToArray();
            if (expression.Kind == "number")
                return args[0].ValueKind == JsonValueKind.Number ? Value(args[0])
                    : args[0].ValueKind == JsonValueKind.String && decimal.TryParse(args[0].GetString(),
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                        CultureInfo.InvariantCulture, out var parsed) ? Value(parsed) : Unknown("invariant_decimal_required");
            if (expression.Kind is "all" or "any" or "not")
            {
                if (!args.All(IsBoolean)) return Unknown("boolean_required");
                return Value(expression.Kind == "all" ? args.All(value => value.GetBoolean())
                    : expression.Kind == "any" ? args.Any(value => value.GetBoolean()) : !args[0].GetBoolean());
            }
            var numeric = args.All(value => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _));
            if (expression.Kind is "add" or "subtract") return numeric
                ? Value(expression.Kind == "add" ? checked(args[0].GetDecimal() + args[1].GetDecimal()) : checked(args[0].GetDecimal() - args[1].GetDecimal()))
                : Unknown("number_required");
            int comparison;
            if (numeric) comparison = args[0].GetDecimal().CompareTo(args[1].GetDecimal());
            else if (args.All(value => value.ValueKind == JsonValueKind.String)) comparison = StringComparer.Ordinal.Compare(args[0].GetString(), args[1].GetString());
            else if (args.All(IsBoolean) && expression.Kind is "eq" or "ne") comparison = args[0].GetBoolean().CompareTo(args[1].GetBoolean());
            else return Unknown("incompatible_operand_types");
            return Value(expression.Kind switch { "eq" => comparison == 0, "ne" => comparison != 0, "gt" => comparison > 0,
                "ge" => comparison >= 0, "lt" => comparison < 0, "le" => comparison <= 0, _ => throw new InvalidOperationException() });
        }
        catch (OverflowException) { return Unknown("decimal_overflow"); }
        static bool IsBoolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }
}
