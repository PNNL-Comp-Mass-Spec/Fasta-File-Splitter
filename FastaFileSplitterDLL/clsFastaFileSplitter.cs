using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using PRISM; // This class will read a protein FASTA file and split it apart into the specified number of sections
// Although the splitting is random, each section will have a nearly identical number of residues
//
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
//
// Started April 1, 2010

namespace FastaFileSplitterLibrary
{
    public class clsFastaFileSplitter : PRISM.FileProcessor.ProcessFilesBase
    {
        public const string XML_SECTION_OPTIONS = "FastaFileSplitterOptions";
        public const int DEFAULT_SPLIT_COUNT = 10;

        // Error codes specialized for this class
        public enum FastaFileSplitterErrorCode
        {
            NoError = 0,
            ErrorReadingInputFile = 1,
            ErrorWritingOutputFile = 2,
            InvalidMotif = 4,
            UnspecifiedError = -1
        }

        public struct FastaFileInfoType
        {
            public string FilePath;
            public int NumProteins;
            public long NumResidues;
        }

        private int mSplitCount;
        private int mInputFileProteinsProcessed;
        private int mInputFileLinesRead;
        private int mInputFileLineSkipCount;
        private readonly Random mRandom;

        private List<FastaFileInfoType> mSplitFastaFileInfo;
        public FastaFileOptionsClass FastaFileOptions;
        private FastaFileSplitterErrorCode mLocalErrorCode;

        public int InputFileProteinsProcessed
        {
            get
            {
                return mInputFileProteinsProcessed;
            }
        }

        public int InputFileLinesRead
        {
            get
            {
                return mInputFileLinesRead;
            }
        }

        public int InputFileLineSkipCount
        {
            get
            {
                return mInputFileLineSkipCount;
            }
        }

        public FastaFileSplitterErrorCode LocalErrorCode
        {
            get
            {
                return mLocalErrorCode;
            }
        }

        public int FastaFileSplitCount
        {
            get
            {
                return mSplitCount;
            }

            private set
            {
                if (value < 0)
                    value = 0;
                mSplitCount = value;
            }
        }

        public List<FastaFileInfoType> SplitFastaFileInfo
        {
            get
            {
                return mSplitFastaFileInfo;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsFastaFileSplitter(int splitCount = 5)
        {
            mFileDate = "August 4, 2021";

            // Note: intentionally using a seed here
            mRandom = new Random(314159);

            InitializeLocalVariables();
            FastaFileSplitCount = splitCount;
        }

        private bool CreateOutputFiles(int splitCount, string outputFilePathBase, ref clsFastaOutputFile[] outputFiles)
        {
            var fileNum = 0;
            try
            {
                outputFiles = new clsFastaOutputFile[splitCount];
                var formatCode = "0";
                if (splitCount >= 10)
                {
                    var zeroCount = (int)Math.Round(Math.Floor(Math.Log10(splitCount) + 1d));
                    for (int index = 2, loopTo = zeroCount; index <= loopTo; index++)
                        formatCode += "0";
                }

                // Create each of the output files
                var loopTo1 = splitCount;
                for (fileNum = 1; fileNum <= loopTo1; fileNum++)
                {
                    var outputFilePath = outputFilePathBase + "_" + splitCount + "x_" + fileNum.ToString(formatCode) + ".fasta";
                    outputFiles[fileNum - 1] = new clsFastaOutputFile(outputFilePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error creating output file " + fileNum, ex);
                SetLocalErrorCode(FastaFileSplitterErrorCode.ErrorWritingOutputFile);
                return false;
            }
        }

        public override IList<string> GetDefaultExtensionsToParse()
        {
            var extensionsToParse = new List<string>() { ".fasta", ".faa" };
            return extensionsToParse;
        }

        /// <summary>
        /// Get the error message, or an empty string if no error
        /// </summary>
        /// <returns></returns>
        public override string GetErrorMessage()
        {
            string errorMessage;
            if (ErrorCode == ProcessFilesErrorCodes.LocalizedError | ErrorCode == ProcessFilesErrorCodes.NoError)
            {
                switch (mLocalErrorCode)
                {
                    case FastaFileSplitterErrorCode.NoError:
                        {
                            errorMessage = "";
                            break;
                        }

                    case FastaFileSplitterErrorCode.ErrorReadingInputFile:
                        {
                            errorMessage = "Error reading input file";
                            break;
                        }

                    case FastaFileSplitterErrorCode.ErrorWritingOutputFile:
                        {
                            errorMessage = "Error writing to the output file";
                            break;
                        }

                    case FastaFileSplitterErrorCode.UnspecifiedError:
                        {
                            errorMessage = "Unspecified localized error";
                            break;
                        }

                    default:
                        {
                            // This shouldn't happen
                            errorMessage = "Unknown error state";
                            break;
                        }
                }
            }
            else
            {
                errorMessage = GetBaseClassErrorMessage();
            }

            return errorMessage;
        }

        /// <summary>
        /// Examines the Residue counts in the files in outputFiles()
        /// Will randomly choose one of the files whose residue count is less than the average residue count
        /// </summary>
        /// <param name="splitCount">Number of files the source .Fasta file is being split into</param>
        /// <param name="outputFiles">Array of clsFastaOutputFile objects</param>
        /// <returns>Randomly selected target file number (ranging from 1 to splitCount)</returns>
        /// <remarks></remarks>
        private int GetTargetFileNum(int splitCount, ref clsFastaOutputFile[] outputFiles)
        {
            // The strategy:
            // 1) Compute the average residue count already stored to the files
            // 2) Populate an array with the file numbers that have residue counts less than the average
            // 3) Randomly choose one of those files

            if (splitCount <= 1)
            {
                // Nothing to do; just return 1
                return 1;
            }

            // Compute the average number of residues stored in each file
            var sum = 0L;
            for (int index = 0, loopTo = splitCount - 1; index <= loopTo; index++)
                sum += outputFiles[index].TotalResiduesInFile;
            if (sum == 0L)
            {
                // We haven't stored any proteins yet
                // Just return a random number between 1 and splitCount
                return mRandom.Next(1, splitCount);
            }

            var averageCount = sum / (double)splitCount;

            // Populate candidates with the file numbers that have residue counts less than averageCount

            var candidates = new List<int>();
            for (int index = 0, loopTo1 = splitCount - 1; index <= loopTo1; index++)
            {
                if (outputFiles[index].TotalResiduesInFile < averageCount)
                {
                    candidates.Add(index + 1);
                }
            }

            if (candidates.Count > 0)
            {
                // Now randomly choose an entry in candidates
                // Note that rand.Next(x,y) returns an integer in the range x <= i < y
                // In other words, the range of random numbers returned is x through y-1
                //
                // Thus, we pass candidateCount to the upper bound of rand.Next() to get a
                // range of values from 0 to candidateCount-1

                var randomIndex = mRandom.Next(0, candidates.Count);

                // Return the file number at index randomIndex in candidates
                return candidates[randomIndex];
            }
            else
            {
                // Pick a file at random
                return mRandom.Next(1, splitCount);
            }
        }

        private void InitializeLocalVariables()
        {
            mLocalErrorCode = FastaFileSplitterErrorCode.NoError;
            FastaFileSplitCount = DEFAULT_SPLIT_COUNT;
            mInputFileProteinsProcessed = 0;
            mInputFileLinesRead = 0;
            mInputFileLineSkipCount = 0;
            mSplitFastaFileInfo = new List<FastaFileInfoType>();
            FastaFileOptions = new FastaFileOptionsClass();
        }

        /// <summary>
        /// Examines the file's extension and true if it ends in .fasta or .faa
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static bool IsFastaFile(string filePath)
        {
            var fileExtension = Path.GetExtension(filePath);
            if (fileExtension.Equals(".fasta", StringComparison.OrdinalIgnoreCase) || fileExtension.Equals(".faa", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool LoadParameterFileSettings(string parameterFilePath)
        {
            var settingsFile = new XmlSettingsFileAccessor();
            try
            {
                if (parameterFilePath is null || parameterFilePath.Length == 0)
                {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(parameterFilePath))
                {
                    // See if parameterFilePath points to a file in the same directory as the application
                    parameterFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Path.GetFileName(parameterFilePath));
                    if (!File.Exists(parameterFilePath))
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                if (settingsFile.LoadSettings(parameterFilePath))
                {
                    if (!settingsFile.SectionPresent(XML_SECTION_OPTIONS))
                    {
                        ShowErrorMessage("The node '<section name=\"" + XML_SECTION_OPTIONS + "\"> was not found in the parameter file: " + parameterFilePath);
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile);
                        return false;
                    }
                    else
                    {
                        FastaFileSplitCount = settingsFile.GetParam(XML_SECTION_OPTIONS, "SplitCount", FastaFileSplitCount);
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadParameterFileSettings", ex);
                return false;
            }

            return true;
        }

        private bool OpenInputFile(string inputFilePath, string outputDirectoryPath, string outputFileNameBaseBaseOverride, out ProteinFileReader.FastaFileReader fastaFileReader, out string outputFilePathBase)
        {
            fastaFileReader = null;
            outputFilePathBase = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(inputFilePath))
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath);
                    return false;
                }

                // Verify that the input file exists
                if (!File.Exists(inputFilePath))
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath);
                    return false;
                }

                // Instantiate the protein file reader object (assuming inputFilePath is a .Fasta file)
                fastaFileReader = new ProteinFileReader.FastaFileReader();

                // Define the output file name
                var outputFileNameBase = string.Empty;
                if (!string.IsNullOrWhiteSpace(outputFileNameBaseBaseOverride))
                {
                    if (Path.HasExtension(outputFileNameBaseBaseOverride))
                    {
                        outputFileNameBase = string.Copy(outputFileNameBaseBaseOverride);

                        // Remove the extension
                        outputFileNameBase = Path.GetFileNameWithoutExtension(outputFileNameBase);
                    }
                    else
                    {
                        outputFileNameBase = string.Copy(outputFileNameBaseBaseOverride);
                    }
                }

                if (outputFileNameBase.Length == 0)
                {
                    // Output file name is not defined; auto-define it
                    outputFileNameBase = Path.GetFileNameWithoutExtension(inputFilePath);
                }

                if (outputDirectoryPath is null || outputDirectoryPath.Length == 0)
                {
                    // This code likely won't be reached since CleanupFilePaths() should have already initialized outputDirectoryPath
                    FileInfo inputFile;
                    inputFile = new FileInfo(inputFilePath);
                    outputDirectoryPath = inputFile.Directory.FullName;
                }

                // Define the full path to output file base name
                outputFilePathBase = Path.Combine(outputDirectoryPath, outputFileNameBase);
                return true;
            }
            catch (Exception ex)
            {
                HandleException("OpenInputFile", ex);
                SetLocalErrorCode(FastaFileSplitterErrorCode.ErrorReadingInputFile);
                outputFilePathBase = string.Empty;
                return true;
            }
        }

        public bool SplitFastaFile(string inputFastaFilePath, string outputDirectoryPath, int splitCount)
        {
            return SplitFastaFile(inputFastaFilePath, outputDirectoryPath, string.Empty, splitCount);
        }

        /// <summary>
        /// Split inputFastaFilePath into splitCount parts
        /// The output file will be created in outputDirectoryPath (or the same directory as inputFastaFilePath if outputDirectoryPath is empty)
        /// </summary>
        /// <param name="inputFastaFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="outputFileNameBaseOverride">When defined, use this name for the protein output filename rather than auto-defining the name</param>
        /// <param name="splitCount"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool SplitFastaFile(string inputFastaFilePath, string outputDirectoryPath, string outputFileNameBaseOverride, int splitCount)
        {
            ProteinFileReader.FastaFileReader fastaFileReader = null;

            // The following is a zero-based array that tracks the output file handles, along with the number of residues written to each file
            clsFastaOutputFile[] outputFiles = null;
            var outputFilePathBase = string.Empty;
            bool inputProteinFound;
            int outputFileIndex;
            try
            {
                mSplitFastaFileInfo.Clear();

                // Open the input file and define the output file path
                var openSuccess = OpenInputFile(inputFastaFilePath, outputDirectoryPath, outputFileNameBaseOverride, out fastaFileReader, out outputFilePathBase);

                // Abort processing if we couldn't successfully open the input file
                if (!openSuccess)
                    return false;
                if (splitCount < 1)
                    splitCount = 1;

                // Create the output files
                var success = CreateOutputFiles(splitCount, outputFilePathBase, ref outputFiles);
                if (!success)
                    return false;

                // Attempt to open the input file
                if (!fastaFileReader.OpenFile(inputFastaFilePath))
                {
                    SetLocalErrorCode(FastaFileSplitterErrorCode.ErrorReadingInputFile);
                    return false;
                }

                UpdateProgress("Splitting FASTA file: " + Path.GetFileName(inputFastaFilePath), 0f);

                // Read each protein in the input file and process appropriately
                mInputFileProteinsProcessed = 0;
                mInputFileLineSkipCount = 0;
                mInputFileLinesRead = 0;
                do
                {
                    inputProteinFound = fastaFileReader.ReadNextProteinEntry();
                    mInputFileLineSkipCount += fastaFileReader.LineSkipCount;
                    if (inputProteinFound)
                    {
                        mInputFileProteinsProcessed += 1;
                        mInputFileLinesRead = fastaFileReader.LinesRead;
                        outputFileIndex = GetTargetFileNum(splitCount, ref outputFiles) - 1;
                        if (outputFileIndex < 0 || outputFileIndex >= outputFiles.Count())
                        {
                            Console.WriteLine("Programming bug: index is outside the expected range.  Defaulting to use OutputFileIndex=0");
                            outputFileIndex = 0;
                        }

                        // Append the current protein to the file at index outputFileIndex
                        var description = fastaFileReader.ProteinDescription;
                        var sequence = fastaFileReader.ProteinSequence;
                        outputFiles[outputFileIndex].StoreProtein(fastaFileReader.ProteinName, description, sequence);

                        UpdateProgress(fastaFileReader.PercentFileProcessed());
                    }
                }
                while (inputProteinFound);

                // Close the input file
                fastaFileReader.CloseFile();

                // Close the output files
                // Store the info on the newly created files in mSplitFastaFileInfo
                for (int index = 0, loopTo = splitCount - 1; index <= loopTo; index++)
                {
                    outputFiles[index].CloseFile();
                    var udtFileInfo = new FastaFileInfoType()
                    {
                        FilePath = outputFiles[index].OutputFilePath,
                        NumProteins = outputFiles[index].TotalProteinsInFile,
                        NumResidues = outputFiles[index].TotalResiduesInFile
                    };
                    mSplitFastaFileInfo.Add(udtFileInfo);
                }

                // Create the stats file
                WriteStatsFile(outputFilePathBase + "_SplitStats.txt", splitCount, ref outputFiles);
                UpdateProgress("Done: Processed " + mInputFileProteinsProcessed.ToString("###,##0") + " proteins (" + mInputFileLinesRead.ToString("###,###,##0") + " lines)", 100f);
                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error in SplitFastaFile", ex);
                return false;
            }
        }

        /// <summary>
        /// Main processing function -- Calls SplitFastaFile
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="parameterFilePath"></param>
        /// <param name="resetErrorCode"></param>
        /// <returns>True if success, False if failure</returns>
        public override bool ProcessFile(string inputFilePath, string outputDirectoryPath, string parameterFilePath, bool resetErrorCode)
        {
            FileInfo file;
            string inputFilePathFull;
            var success = default(bool);
            if (resetErrorCode)
            {
                SetLocalErrorCode(FastaFileSplitterErrorCode.NoError);
            }

            if (!LoadParameterFileSettings(parameterFilePath))
            {
                ShowErrorMessage("Parameter file load error: " + parameterFilePath);
                if (ErrorCode == ProcessFilesErrorCodes.NoError)
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile);
                }

                return false;
            }

            try
            {
                if (inputFilePath is null || inputFilePath.Length == 0)
                {
                    ShowMessage("Input file name is empty");
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath);
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Parsing " + Path.GetFileName(inputFilePath));

                    // Note that CleanupFilePaths() will update mOutputDirectoryPath, which is used by LogMessage()
                    if (!CleanupFilePaths(ref inputFilePath, ref outputDirectoryPath))
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.FilePathError);
                    }
                    else
                    {
                        ResetProgress();
                        try
                        {
                            // Obtain the full path to the input file
                            file = new FileInfo(inputFilePath);
                            inputFilePathFull = file.FullName;
                            success = SplitFastaFile(inputFilePathFull, outputDirectoryPath, FastaFileSplitCount);
                            if (success)
                            {
                                ShowMessage(string.Empty, false);
                            }
                            else
                            {
                                SetLocalErrorCode(FastaFileSplitterErrorCode.UnspecifiedError);
                                ShowErrorMessage("Error");
                            }
                        }
                        catch (Exception ex)
                        {
                            HandleException("Error calling SplitFastaFile", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFile", ex);
            }

            return success;
        }

        private void SetLocalErrorCode(FastaFileSplitterErrorCode eNewErrorCode)
        {
            SetLocalErrorCode(eNewErrorCode, false);
        }

        private void SetLocalErrorCode(FastaFileSplitterErrorCode eNewErrorCode, bool leaveExistingErrorCodeUnchanged)
        {
            if (leaveExistingErrorCodeUnchanged && mLocalErrorCode != FastaFileSplitterErrorCode.NoError)
            {
            }
            // An error code is already defined; do not change it
            else
            {
                mLocalErrorCode = eNewErrorCode;
                if (eNewErrorCode == FastaFileSplitterErrorCode.NoError)
                {
                    if (ErrorCode == ProcessFilesErrorCodes.LocalizedError)
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.NoError);
                    }
                }
                else
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.LocalizedError);
                }
            }
        }

        private void WriteStatsFile(string statsFilePath, int splitCount, ref clsFastaOutputFile[] outputFiles)
        {
            try
            {

                // Sleep 250 milliseconds to give the system time to close all of the file handles
                Thread.Sleep(250);
                using (var statsFileWriter = new StreamWriter(new FileStream(statsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    statsFileWriter.WriteLine("Section" + '\t' + "Proteins" + '\t' + "Residues" + '\t' + "FileSize_MB" + '\t' + "FileName");
                    for (int fileIndex = 0, loopTo = splitCount - 1; fileIndex <= loopTo; fileIndex++)
                    {
                        statsFileWriter.Write((fileIndex + 1).ToString() + '\t' + outputFiles[fileIndex].TotalProteinsInFile + '\t' + outputFiles[fileIndex].TotalResiduesInFile);
                        try
                        {
                            var outputFileInfo = new FileInfo(outputFiles[fileIndex].OutputFilePath);
                            statsFileWriter.Write('\t' + (outputFileInfo.Length / 1024.0d / 1024.0d).ToString("0.000"));
                        }
                        catch (Exception ex)
                        {
                            // Error obtaining a FileInfo object; that's odd
                            statsFileWriter.Write('\t' + "??");
                        }

                        statsFileWriter.WriteLine('\t' + Path.GetFileName(outputFiles[fileIndex].OutputFilePath));
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in WriteStatsFile", ex);
            }
        }

        /// <summary>
        /// Options class
        /// </summary>
        public class FastaFileOptionsClass
        {
            public char ProteinLineStartChar { get; private set; } = '>';
            public char ProteinLineAccessionEndChar { get; set; } = ' ';
        }
    }
}