namespace MediaDeviceCopier
{
	public enum FileCopyStatus
	{
		Copied,
		CopiedBecauseDateOrSizeMismatch,
		SkippedBecauseAlreadyExists
	}
}
