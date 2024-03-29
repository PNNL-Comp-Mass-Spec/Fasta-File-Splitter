﻿using System;
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

        private readonly StreamWriter mOutputFile;
        private readonly string mProteinLineStartChar;
        private readonly string mProteinLineAccessionEndChar;
        private int mResiduesPerLine;
        private int mTotalProteinsInFile;
        private long mTotalResiduesInFile;

        /// <summary>
        /// True if the output file is open for writing
        /// </summary>
        public bool OutputFileIsOpen { get; private set; }

        /// <summary>
        /// Output file path
        /// </summary>
        public string OutputFilePath { get; }

        /// <summary>
        /// Residues per line to write to the output file
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public int ResiduesPerLine
        {
            get => mResiduesPerLine;
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
        public int TotalProteinsInFile => mTotalProteinsInFile;

        /// <summary>
        /// Total residues in the file
        /// </summary>
        public long TotalResiduesInFile => mTotalResiduesInFile;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="outputFilePath"></param>
        /// <param name="proteinLineStartChar"></param>
        /// <param name="proteinLineAccessionEndChar"></param>
        public clsFastaOutputFile(
            string outputFilePath,
            char proteinLineStartChar = DEFAULT_PROTEIN_LINE_START_CHAR,
            char proteinLineAccessionEndChar = DEFAULT_PROTEIN_LINE_ACCESSION_END_CHAR)
        {
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                throw new Exception("OutputFilePath is empty; cannot instantiate class");
            }

            OutputFilePath = outputFilePath;

            mOutputFile = new StreamWriter(new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

            OutputFileIsOpen = true;

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
                if (!OutputFileIsOpen)
                {
                    return;
                }

                mOutputFile.Close();
                OutputFileIsOpen = false;
            }
            catch (Exception ex)
            {
                OnWarningEvent("Error closing the output file: " + ex.Message);
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
            if (!OutputFileIsOpen)
            {
                return;
            }

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

                mTotalProteinsInFile++;
                mTotalResiduesInFile += sequence.Length;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in StoreProtein", ex);
                throw;
            }
        }
    }
}