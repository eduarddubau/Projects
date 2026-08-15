namespace Backend.Exceptions;

/// <summary>The message is returned to the client verbatim, so it must never
/// distinguish a missing resource from one the caller may not see.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message) { }
}
