namespace Backend.Exceptions;

public class BusinessRuleException : Exception
{
    /// <summary>Stable identifier the client maps to a translated string. The message is
    /// the English fallback for consumers with no translation table.</summary>
    public string? Code { get; }

    public BusinessRuleException(string message) : base(message) { }

    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }
}
