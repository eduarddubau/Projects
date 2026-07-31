namespace Backend.Exceptions;

public class BusinessRuleException : Exception
{
    public string? Code { get; }

    public IReadOnlyDictionary<string, string>? Params { get; }

    public BusinessRuleException(string message) : base(message) { }

    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }

    public BusinessRuleException(string code, string message, IReadOnlyDictionary<string, string> parameters) : base(message)
    {
        Code = code;
        Params = parameters;
    }
}
