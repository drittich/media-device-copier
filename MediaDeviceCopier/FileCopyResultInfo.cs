namespace MediaDeviceCopier
{
	public class FileCopyResultInfo
	{
		public FileCopyStatus CopyStatus { get; set; }
		public ulong Length { get; set; }
		// True when source file was deleted as part of a move operation
		public bool SourceDeleted { get; set; }
	}
}
