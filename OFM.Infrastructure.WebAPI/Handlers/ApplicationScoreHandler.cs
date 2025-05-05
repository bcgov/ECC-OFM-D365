namespace OFM.Infrastructure.WebAPI.Handlers;


// Comparison Handler Interface (Chain of Responsibility)
public interface IComparisonHandler
{
    IComparisonHandler SetNext(IComparisonHandler handler);
    bool Handle(string operatorStr, object value, string comparisonValue);
}

// Abstract Base Handler
public abstract class BaseComparisonHandler : IComparisonHandler
{
    private IComparisonHandler _nextHandler;

    public IComparisonHandler SetNext(IComparisonHandler handler)
    {
        _nextHandler = handler;
        return handler;
    }

    public virtual bool Handle(string operatorStr, object value, string comparisonValue)
    {
        return _nextHandler?.Handle(operatorStr, value, comparisonValue) ?? false;
    }
}

// Concrete Comparison Handlers
public class EqualHandler : BaseComparisonHandler
{
    public override bool Handle(string operatorStr, object value, string comparisonValue)
    {
        if (operatorStr == "Equal")
        {
            return value?.ToString() == comparisonValue;
        }
        return base.Handle(operatorStr, value, comparisonValue);
    }
}

public class GreaterThanHandler : BaseComparisonHandler
{
    public override bool Handle(string operatorStr, object value, string comparisonValue)
    {
        if (operatorStr == "GreaterThan" && value != null && decimal.TryParse(value.ToString(), out var numValue) && decimal.TryParse(comparisonValue, out var compValue))
        {
            return numValue > compValue;
        }
        return base.Handle(operatorStr, value, comparisonValue);
    }
}

public class LessThanHandler : BaseComparisonHandler
{
    public override bool Handle(string operatorStr, object value, string comparisonValue)
    {
        if (operatorStr == "LessThan" && value != null && decimal.TryParse(value.ToString(), out var numValue) && decimal.TryParse(comparisonValue, out var compValue))
        {
            return numValue < compValue;
        }
        return base.Handle(operatorStr, value, comparisonValue);
    }
}

public class GreaterThanOrEqualHandler : BaseComparisonHandler
{
    public override bool Handle(string operatorStr, object value, string comparisonValue)
    {
        if (operatorStr == "GreaterThanOrEqual" && value != null && decimal.TryParse(value.ToString(), out var numValue) && decimal.TryParse(comparisonValue, out var compValue))
        {
            return numValue >= compValue;
        }
        return base.Handle(operatorStr, value, comparisonValue);
    }
}

public class LessThanOrEqualHandler : BaseComparisonHandler
{
    public override bool Handle(string operatorStr, object value, string comparisonValue)
    {
        if (operatorStr == "LessThanOrEqual" && value != null && decimal.TryParse(value.ToString(), out var numValue) && decimal.TryParse(comparisonValue, out var compValue))
        {
            return numValue <= compValue;
        }
        return base.Handle(operatorStr, value, comparisonValue);
    }
}

public class BetweenHandler : BaseComparisonHandler
{
    public override bool Handle(string operatorStr, object value, string comparisonValue)
    {
        if (operatorStr == "Between" && value != null && decimal.TryParse(value.ToString(), out var numValue))
        {
            var range = comparisonValue.Split(',');
            if (range.Length == 2 && decimal.TryParse(range[0], out var lower) && decimal.TryParse(range[1], out var upper))
            {
                return numValue >= lower && numValue <= upper;
            }
        }
        return base.Handle(operatorStr, value, comparisonValue);
    }
}

// Operator Mapping for Choice Field
public static class OperatorMapper
{
    private static readonly Dictionary<int, string> OperatorMap = new()
        {
            { 1, "Equal" },
            { 2, "LessThan" },
            { 3, "LessThanOrEqual" },
            { 4, "GreaterThan" },
            { 5, "GreaterThanOrEqual" },
            { 6, "Contains" },
            { 7, "Between" }
            
        };

    public static string MapOperator(int choiceValue)
    {
        return OperatorMap.TryGetValue(choiceValue, out var operatorStr) ? operatorStr : throw new ArgumentException($"Unknown operator value: {choiceValue}");
    }
}

