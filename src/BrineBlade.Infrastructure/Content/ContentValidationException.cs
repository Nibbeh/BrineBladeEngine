namespace BrineBlade.Infrastructure.Content;

public sealed class ContentValidationException : Exception
{
    public List<string> Errors { get; }

    public ContentValidationException(IEnumerable<string> errors)
        : base("Content validation failed. See Errors for details.")
    {
        Errors = errors.ToList();
    }
}

