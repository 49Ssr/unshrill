namespace Unshrill.WindowsAudio;

public sealed class AudioSessionServiceFaultedEventArgs(Exception exception) : EventArgs
{
	public Exception Exception { get; } = exception;
}
