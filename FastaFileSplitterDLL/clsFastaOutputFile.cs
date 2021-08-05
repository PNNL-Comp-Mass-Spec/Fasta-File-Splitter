using System;
using System.IO;

namespace FastaFileSplitterLibrary
{
    public class clsFastaOutputFile
    {
        public const char DEFAULT_PROTEIN_LINE_START_CHAR = '>';
        public const char DEFAULT_PROTEIN_LINE_ACCESSION_END_CHAR = ' ';
        public const int DEFAULT_RESIDUES_PER_LINE = 60;

        protected bool mOutputFileIsOpen;
        protected string mOutputFilePath;
        protected StreamWriter mOutputFile;
        protected string mProteinLineStartChar;
        protected string mProteinLineAccessionEndChar;
        protected int mResiduesPerLine;
        protected int mTotalProteinsInFile;
        protected long mTotalResiduesInFile;

        public bool OutputFileIsOpen
        {
            get
            {
                return mOutputFileIsOpen;
            }
        }

        public string OutputFilePath
        {
            get
            {
                return mOutputFilePath;
            }
        }

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

        public int TotalProteinsInFile
        {
            get
            {
                return mTotalProteinsInFile;
            }
        }

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

        public void StoreProtein(string proteinName, string description, string sequence)
        {
            if (mOutputFileIsOpen)
            {
                try
                {
                    // Write out the protein header and description line
                    mOutputFile.WriteLine(mProteinLineStartChar + proteinName + mProteinLineAccessionEndChar + description);

                    // Now write out the residues, storing mResiduesPerLine residues per line
                    int startIndex = 0;
                    while (startIndex < sequence.Length)
                    {
                        int charCount = mResiduesPerLine;
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