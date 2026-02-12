namespace DracoSharp.Core;

public class DracoException : Exception
{
    public DracoException(string message) : base(message) { }
    public DracoException(string message, Exception innerException) : base(message, innerException) { }
}
