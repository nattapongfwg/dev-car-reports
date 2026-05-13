namespace CarReports.Web;

public sealed class InvalidUploadException : Exception
{
    public InvalidUploadException(string message) : base(message) { }
    public InvalidUploadException(string message, Exception inner) : base(message, inner) { }
}
