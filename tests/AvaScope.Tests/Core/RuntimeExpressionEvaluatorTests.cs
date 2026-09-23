using System.Globalization;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class RuntimeExpressionEvaluatorTests
{
    [Theory]
    [InlineData("hu-HU")]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void DecimalConversionComparisonAndArithmeticAreCultureIndependent(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var expression = Op("eq", Op("add", Op("number", Literal("1.25")), Literal(2.5m)), Literal(3.75m));
            Assert.True(Evaluate(expression).Value!.Value.GetBoolean());
            Assert.Equal("invariant_decimal_required", Evaluate(Op("number", Literal("1,25"))).Reason);
            Assert.Equal("invariant_decimal_required", Evaluate(Op("number", Literal("1,000"))).Reason);
            Assert.Equal("decimal_overflow", Evaluate(Op("add", Literal(decimal.MaxValue), Literal(1))).Reason);
            Assert.Equal("incompatible_operand_types", Evaluate(Op("eq", Literal("1"), Literal(1))).Reason);
            Assert.True(Evaluate(Op("lt", Literal("Z"), Literal("a"))).Value!.Value.GetBoolean());
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void MissingOperandsNeverPassAnyAllOrNegationAndRetainExactPaths()
    {
        var missing = new RuntimeExpression("value", "values");
        foreach (var expression in new[] { Op("any", Literal(true), missing), Op("all", Literal(false), missing), Op("not", missing) })
        {
            var actual = Evaluate(expression, []);
            Assert.Equal("indeterminate", actual.Status); Assert.Null(actual.Value);
            var operand = actual.Operands.Single(item => item.Source == "values");
            Assert.StartsWith("$.operands[", operand.Path); Assert.Equal("source_empty", operand.Reason);
        }
    }

    [Fact]
    public void CountsSumsExtremaAndExplicitEmptySemanticsUseOneTypedValueModel()
    {
        Assert.Equal(0, Evaluate(new("count", "values"), []).Value!.Value.GetInt32());
        Assert.Equal(0m, Evaluate(new("sum", "values"), []).Value!.Value.GetDecimal());
        Assert.Equal("indeterminate", Evaluate(new("minimum", "values"), []).Status);
        Assert.Equal("source_ambiguous", Evaluate(new("value", "values"), [1, 2]).Reason);
        Assert.Equal(2, Evaluate(new("count_true", "values"), [true, false, true]).Value!.Value.GetInt32());
        Assert.Equal(3.75m, Evaluate(new("sum", "values"), [1.25m, 2.5m]).Value!.Value.GetDecimal());
        Assert.Equal(1.25m, Evaluate(new("minimum", "values"), [1.25m, 2.5m]).Value!.Value.GetDecimal());
        Assert.Equal(2.5m, Evaluate(new("maximum", "values"), [1.25m, 2.5m]).Value!.Value.GetDecimal());
        Assert.True(Evaluate(Op("eq", new("sum", "values"), Literal(3.75m)), [1.25m, 2.5m]).Value!.Value.GetBoolean());
        Assert.Equal("number_required", Evaluate(new("sum", "values"), ["1.25"]).Reason);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("redacted")]
    [InlineData("truncated")]
    [InlineData("unavailable")]
    public void NoAggregateTreatsUnavailableValuesOrIncompleteCoverageAsComplete(string status)
    {
        foreach (var op in new[] { "count", "sum", "value", "minimum", "maximum", "count_true" })
        {
            var source = Observation([1], true, status);
            var definition = new RuntimeExpressionDefinition(new(op, "values"), [new("values", new(name: "Value"), "value")]);
            Assert.Equal("indeterminate", RuntimeExpressionEvaluator.Evaluate(definition, [source]).Status);
            Assert.Equal("indeterminate", RuntimeExpressionEvaluator.Evaluate(definition, [Observation([], false)]).Status);
        }
    }

    [Fact]
    public void ExpressionValidationRejectsCodeUnknownSourcesExcessiveDepthAndUnusedOperands()
    {
        Assert.Throws<ArgumentException>(() => new RuntimeExpression("eval", literal: JsonSerializer.SerializeToElement("process.Exit()")));
        Assert.Throws<ArgumentException>(() => new RuntimeExpression("not", operands: [Literal(true), Literal(false)]));
        Assert.Throws<ArgumentException>(() => new RuntimeExpressionDefinition(new("value", "undefined")));
        Assert.Throws<ArgumentException>(() => new RuntimeExpressionDefinition(Literal(true), [new("unused", new(name: "Value"))]));
        var deep = Literal(true);
        for (var i = 0; i < 9; i++) deep = Op("not", deep);
        Assert.Throws<ArgumentException>(() => new RuntimeExpressionDefinition(deep));
        Assert.Throws<ArgumentException>(() => new SemanticWaitCondition("expression"));
        Assert.Throws<ArgumentException>(() => new SemanticWaitCondition("expression", expected: "true", expression: new(Literal(true))));
    }

    private static RuntimeExpressionResult Evaluate(RuntimeExpression expression, object[]? values = null)
    {
        var definition = new RuntimeExpressionDefinition(expression, values is null ? [] : [new("values", new(name: "Value"), "value")]);
        return RuntimeExpressionEvaluator.Evaluate(definition, values is null ? [] : [Observation(values)]);
    }
    private static RuntimeExpressionSourceObservation Observation(object[] values, bool complete = true, string status = "present") =>
        new("values", new(name: "Value"), "value", new(complete, values.Length, values.Length, complete ? [] : ["result_limit"]),
            values.Select((value, index) => new RuntimeExpressionValue(new(new("s"), "top", "visual", "node" + index),
                new("value", "number", status, JsonSerializer.SerializeToElement(value), "test"))).ToArray());
    internal static RuntimeExpression Literal(object value) => new("literal", literal: JsonSerializer.SerializeToElement(value));
    internal static RuntimeExpression Op(string kind, params RuntimeExpression[] operands) => new(kind, operands: operands);
}
