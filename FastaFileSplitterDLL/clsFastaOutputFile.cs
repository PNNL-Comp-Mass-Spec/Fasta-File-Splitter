using System;
using System.IO;
using PRISM;

namespace FastaFileSplitterLibrary
{
    /// <summary>
    /// This class is used to create the split FASTA files
    /// </summary>
    public class clsFastaOutputFile : EventNotifier
    {
        /// <summary>
        /// Protein line start character
        /// </summary>
        public const char DEFAULT_PROTEIN_LINE_START_CHAR = '>';

        /// <summary>
        /// Protein name / description separator character
        /// </summary>
        public const char DEFAULT_PROTEIN_LINE_ACCESSION_END_CHAR = ' ';

        /// <summary>
        /// Number of residues per line for the protein sequence
        /// </summary>
        public const int DEFAULT_RESIDUES_PER_LINE = 60;

        protected bool mOutputFileIsOpen;

        protected string mOutputFilePath;
        protected StreamWriter mOutputFile;
        protected string mProteinLineStartChar;
        protected string mProteinLineAccessionEndChar;
        protected int mResiduesPerLine;
        protected int mTotalProteinsInFile;
        protected long mTotalResiduesInFile;

        /// <summary>
        /// True if the output file is open for writing
        /// </summary>
        public bool OutputFileIsOpen
        {
            get
            {
                return mOutputFileIsOpen;
            }
        }

        /// <summary>
        /// Output file path
        /// </summary>
        public string OutputFilePath
        {
            get
            {
                return mOutputFilePath;
            }
        }

        /// <summary>
        /// Residues per line to write to the output file
        /// </summary>
        public int ResiduesPerLine
        {
            get
            {
                return mResiduesPerLine;
            }

            set
            {
                if (value < 1)
                    value = 1;
                mResiduesPerLine = value;
            }
        }

        /// <summary>
        /// Total proteins in the file
        /// </summary>
        public int TotalProteinsInFile
        {
            get
            {
                return mTotalProteinsInFile;
            }
        }

        /// <summary>
        /// Total residues in the file
        /// </summary>
        public long TotalResiduesInFile
        {
            get
            {
                return mTotalResiduesInFile;
            }
        }

        public clsFastaOutputFile(string outputFilePath) : this(outputFilePath, DEFAULT_PROTEIN_LINE_START_CHAR, DEFAULT_PROTEIN_LINE_ACCESSION_END_CHAR)
        {
        }

        public clsFastaOutputFile(string outputFilePath, char proteinLineStartChar, char proteinLineAccessionEndChar)
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="outputFilePath"></param>
        /// <param name="proteinLineStartChar"></param>
        /// <param name="proteinLineAccessionEndChar"></param>
        {
            if (outputFilePath is null || outputFilePath.Length == 0)
            {
                throw new Exception("OutputFilePath is empty; cannot instantiate class");
            }

            mOutputFilePath = outputFilePath;
            mOutputFile = new StreamWriter(new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
            mOutputFileIsOpen = true;
            mProteinLineStartChar = proteinLineStartChar.ToString();
            mProteinLineAccessionEndChar = proteinLineAccessionEndChar.ToString();
            mResiduesPerLine = DEFAULT_RESIDUES_PER_LINE;
            mTotalProteinsInFile = 0;
            mTotalResiduesInFile = 0L;
        }

        /// <summary>
        /// Close the output file
        /// </summary>
        public void CloseFile()
        {
            try
            {
                if (mOutputFileIsOpen && mOutputFile is object)
                {
                    mOutputFile.Close();
                    mOutputFileIsOpen = false;
                }
            }
            catch (Exception ex)
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Append a protein to the output file
        /// </summary>
        /// <param name="proteinName"></param>
        /// <param name="description"></param>
        /// <param name="sequence"></param>
        public void StoreProtein(string proteinName, string description, string sequence)
        {
            if (mOutputFileIsOpen)
            {
                try
                {
                    // Write out the protein header and description line
                    mOutputFile.WriteLine(mProteinLineStartChar + proteinName + mProteinLineAccessionEndChar + description);

                    // Now write out the residues, storing mResiduesPerLine residues per line
                    var startIndex = 0;
                    while (startIndex < sequence.Length)
                    {
                        var charCount = mResiduesPerLine;
                        if (startIndex + charCount > sequence.Length)
                        {
                            charCount = sequence.Length - startIndex;
                        }

                        mOutputFile.WriteLine(sequence.Substring(startIndex, charCount));
                        startIndex += charCount;
                    }

                    mTotalProteinsInFile += 1;
                    mTotalResiduesInFile += sequence.Length;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in StoreProtein: " + ex.Message);
                    throw;
                }
            }
        }
    }
}