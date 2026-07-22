namespace Unshrill.Dsp;

[Flags]
public enum AudioCandidateReason
{
	None = 0,
	Bright = 1,
	Tonal = 2,
	Transient = 4,
	SuddenEmergence = 8
}
