namespace MediaDeviceCopier
{
    public class FileComparisonInfo
    {
        public ulong Length { get; set; }
        public DateTime ModifiedDate { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is not FileComparisonInfo other)
            {
                return false;
            }

            return Length == other.Length && ModifiedDate == other.ModifiedDate;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Length, ModifiedDate);
        }
    }
}
