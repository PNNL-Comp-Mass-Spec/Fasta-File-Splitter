namespace FastaFileSplitterLibrary
{
    /// <summary>
    /// FASTA file splitter options
    /// </summary>
    public class SplitterOptions
    {
        /// <summary>
        /// Default number of files to create
        /// </summary>
        public const int DEFAULT_SPLIT_COUNT = 10;

        /// <summary>
        /// Default size to use when <see cref="UseTargetFileSize"/> is true
        /// </summary>
        public const int DEFAULT_TARGET_FILE_SIZE_MB = 100;

        /// <summary>
        /// Minimum allowed value for <see cref="TargetFastaFileSizeMB"/>
        /// </summary>
        public const int MINIMUM_TARGET_FILE_SIZE_MB = 5;

        /// <summary>
        /// Number of parts to split the input FASTA file into
        /// </summary>
        public int SplitCount { get; set; }

        /// <summary>
        /// Size, in MB, that each of the split FASTA files should have once splitting is complete
        /// </summary>
        public int TargetFastaFileSizeMB { get; set; }

        /// <summary>
        /// When true, split the input FASTA file into the necessary number of parts such that each part has size TargetFastaFileSizeMB
        /// When false, split the input FASTA file into the number of parts specified by FastaFileSplitCount
        /// </summary>
        public bool UseTargetFileSize { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public SplitterOptions()
        {
            SplitCount = DEFAULT_SPLIT_COUNT;
            TargetFastaFileSizeMB = DEFAULT_TARGET_FILE_SIZE_MB;
            UseTargetFileSize = false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public SplitterOptions(int splitCount, int targetFastaFileSizeMB, bool useTargetFileSize = false)
        {
            SplitCount = splitCount;
            TargetFastaFileSizeMB = targetFastaFileSizeMB;
            UseTargetFileSize = useTargetFileSize;
        }
    }
}
