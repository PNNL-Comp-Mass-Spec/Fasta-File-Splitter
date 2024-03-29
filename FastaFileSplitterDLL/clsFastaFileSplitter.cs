﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using PRISM;
using PRISM.FileProcessor;
using ProteinFileReader;

namespace FastaFileSplitterLibrary
{
    /// <summary>
    /// This class will read a protein FASTA file and split it apart into the specified number of sections
    /// Although the splitting is random, each section will have a nearly identical number of residues
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Started April 1, 2010
    /// </remarks>
    public class clsFastaFileSplitter : ProcessFilesBase
    {
        /// <summary>
        /// XML section name in the parameter file
        /// </summary>
        public const string XML_SECTION_OPTIONS = "FastaFileSplitterOptions";

        /// <summary>
        /// Error codes specific to this class
        /// </summary>
        public enum FastaFileSplitterErrorCode
        {
            /// <summary>
            /// No error
            /// </summary>
            NoError = 0,

            /// <summary>
            /// Error reading the input file
            /// </summary>
            ErrorReadingInputFile = 1,

            /// <summary>
            /// Error writing the output file
            /// </summary>
            ErrorWritingOutputFile = 2,

            /// <summary>
            /// Unspecified error
            /// </summary>
            UnspecifiedError = -1
        }

        /// <summary>
        /// Path and stats for a given FASTA file
        /// </summary>
        public struct FastaFileInfoType
        {
            /// <summary>
            /// FASTA File Path
            /// </summary>
            public string FilePath;

            /// <summary>
            /// Number of proteins in the file
            /// </summary>
            public int NumProteins;

            /// <summary>
            /// Number of residues in the file
            /// </summary>
            public long NumResidues;
        }

        private readonly Random mRandom;

        /// <summary>
        /// Number of proteins read from the input file
        /// </summary>
        public int InputFileProteinsProcessed { get; private set; }

        /// <summary>
        /// Number of lines read from the input file
        /// </summary>
        public int InputFileLinesRead { get; private set; }

        /// <summary>
        /// Number of lines skipped due to having an invalid format
        /// </summary>
        public int InputFileLineSkipCount { get; private set; }

        /// <summary>
        /// Local error code
        /// </summary>
        public FastaFileSplitterErrorCode LocalErrorCode { get; private set; }

        /// <summary>
        /// Processing options
        /// </summary>
        public SplitterOptions Options { get; }

        /// <summary>
        /// Information on each output file
        /// </summary>
        public List<FastaFileInfoType> SplitFastaFileInfo { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="splitCount"></param>
        /// <param name="targetFastaFileSizeMB"></param>
        /// <param name="useTargetFileSize"></param>
        [Obsolete("Use the constructor with the SplitterOptions argument ")]
        public clsFastaFileSplitter(int splitCount = 5, int targetFastaFileSizeMB = 100, bool useTargetFileSize = false)
            : this (new SplitterOptions(splitCount, targetFastaFileSizeMB, useTargetFileSize))
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public clsFastaFileSplitter(SplitterOptions options)
        {
            mFileDate = "August 5, 2021";

            // Note: intentionally using a seed here
            mRandom = new Random(314159);

            Options = options ?? new SplitterOptions();
            SplitFastaFileInfo = new List<FastaFileInfoType>();

            InitializeLocalVariables();
        }

        /// <summary>
        /// Create the output files
        /// </summary>
        /// <param name="splitCount"></param>
        /// <param name="outputFilePathBase"></param>
        /// <param name="outputFiles">Output: zero-based array that tracks the output file handles, along with the number of residues written to each file</param>
        /// <returns>True if successful, false if an error</returns>
        private bool CreateOutputFiles(int splitCount, string outputFilePathBase, out List<clsFastaOutputFile> outputFiles)
        {
            var fileNum = 0;
            outputFiles = new List<clsFastaOutputFile>();

            try
            {
                var formatCode = "0";
                if (splitCount >= 10)
                {
                    var zeroCount = (int)Math.Round(Math.Floor(Math.Log10(splitCount) + 1d));
                    for (var index = 2; index <= zeroCount; index++)
                    {
                        formatCode += "0";
                    }
                }

                // Create each of the output files
                for (fileNum = 1; fileNum <= splitCount; fileNum++)
                {
                    var outputFilePath = outputFilePathBase + "_" + splitCount + "x_" + fileNum.ToString(formatCode) + ".fasta";
                    var outputFile = new clsFastaOutputFile(outputFilePath);
                    RegisterEvents(outputFile);

                    outputFiles.Add(outputFile);
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

        /// <summary>
        /// Default extensions to parse
        /// </summary>
        public override IList<string> GetDefaultExtensionsToParse()
        {
            return new List<string> { ".fasta", ".faa" };
        }

        /// <summary>
        /// Get the error message, or an empty string if no error
        /// </summary>
        public override string GetErrorMessage()
        {
            string errorMessage;

            if (ErrorCode is ProcessFilesErrorCodes.LocalizedError or ProcessFilesErrorCodes.NoError)
            {
                errorMessage = LocalErrorCode switch
                {
                    FastaFileSplitterErrorCode.NoError => "",
                    FastaFileSplitterErrorCode.ErrorReadingInputFile => "Error reading input file",
                    FastaFileSplitterErrorCode.ErrorWritingOutputFile => "Error writing to the output file",
                    FastaFileSplitterErrorCode.UnspecifiedError => "Unspecified localized error",
                    _ => "Unknown error state"
                };
            }
            else
            {
                errorMessage = GetBaseClassErrorMessage();
            }

            return errorMessage;
        }

        /// <summary>
        /// Examines the residue counts in the output files
        /// Returns the index of one of the files whose residue count is less than the average residue count
        /// The file is chosen at random, though the random number generator is always instantiated with the same seed
        /// </summary>
        /// <param name="splitCount">Number of files the source .Fasta file is being split into</param>
        /// <param name="outputFiles">List of clsFastaOutputFile objects</param>
        /// <returns>Randomly selected target file index (ranging from 0 to splitCount - 1)</returns>
        private int GetTargetFileIndex(int splitCount, IReadOnlyList<clsFastaOutputFile> outputFiles)
        {
            // The strategy:
            // 1) Compute the average residue count already stored to the files
            // 2) Populate an array with the file numbers that have residue counts less than the average
            // 3) Randomly choose one of those files

            if (splitCount <= 1)
            {
                // Not actually splitting; just return 0
                return 0;
            }

            // Compute the average number of residues stored in each file
            var sum = outputFiles.Sum(outputFile => outputFile.TotalResiduesInFile);

            if (sum == 0)
            {
                // We haven't stored any proteins yet
                // Just return a random file index
                return mRandom.Next(0, splitCount - 1);
            }

            var averageCount = sum / (double)splitCount;

            // Populate candidates with the file numbers that have residue counts less than averageCount

            var candidates = new List<int>();

            var fileIndex = 0;

            foreach (var outputFile in outputFiles)
            {
                if (outputFile.TotalResiduesInFile < averageCount)
                {
                    candidates.Add(fileIndex);
                }
                fileIndex++;
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

                // Return the file index at index randomIndex in candidates
                return candidates[randomIndex];
            }

            // Pick a file index at random
            return mRandom.Next(0, splitCount - 1);
        }

        private void InitializeLocalVariables()
        {
            LocalErrorCode = FastaFileSplitterErrorCode.NoError;
            InputFileProteinsProcessed = 0;
            InputFileLinesRead = 0;
            InputFileLineSkipCount = 0;
        }

        /// <summary>
        /// Read settings from a parameter file
        /// </summary>
        /// <param name="parameterFilePath"></param>
        public bool LoadParameterFileSettings(string parameterFilePath)
        {
            var settingsFile = new XmlSettingsFileAccessor();
            try
            {
                if (string.IsNullOrWhiteSpace(parameterFilePath))
                {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(parameterFilePath))
                {
                    // See if parameterFilePath points to a file in the same directory as the application
                    parameterFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, Path.GetFileName(parameterFilePath));
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

                    Options.SplitCount = settingsFile.GetParam(XML_SECTION_OPTIONS, "SplitCount", Options.SplitCount);
                    Options.TargetFastaFileSizeMB = settingsFile.GetParam(XML_SECTION_OPTIONS, "TargetFastaFileSizeMB", Options.TargetFastaFileSizeMB);
                    Options.UseTargetFileSize = settingsFile.GetParam(XML_SECTION_OPTIONS, "UseTargetFileSize", Options.UseTargetFileSize);
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadParameterFileSettings", ex);
                return false;
            }

            return true;
        }

        private bool OpenInputFile(string inputFilePath, string outputDirectoryPath, string outputFileNameBaseBaseOverride, out FastaFileReader fastaFileReader, out string outputFilePathBase)
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
                fastaFileReader = new FastaFileReader();

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

                if (string.IsNullOrWhiteSpace(outputDirectoryPath))
                {
                    // This code likely won't be reached since CleanupFilePaths() should have already initialized outputDirectoryPath
                    var inputFile = new FileInfo(inputFilePath);

                    if (inputFile.Directory == null)
                    {
                        outputDirectoryPath = string.Empty;
                    }
                    else
                    {
                        outputDirectoryPath = inputFile.Directory.FullName;
                    }
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

        /// <summary>
        /// Split the FASTA file into the given number of files
        /// </summary>
        /// <param name="inputFastaFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="splitCount"></param>
        /// <returns>True if success, false if an error</returns>
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
        /// <returns>True if success, false if an error</returns>
        public bool SplitFastaFile(string inputFastaFilePath, string outputDirectoryPath, string outputFileNameBaseOverride, int splitCount)
        {
            try
            {
                SplitFastaFileInfo.Clear();

                // Open the input file and define the output file path
                var openSuccess = OpenInputFile(
                    inputFastaFilePath,
                    outputDirectoryPath,
                    outputFileNameBaseOverride,
                    out var fastaFileReader,
                    out var outputFilePathBase);

                // Abort processing if we couldn't successfully open the input file
                if (!openSuccess)
                    return false;

                if (splitCount < 1)
                    splitCount = 1;

                // Create the output files
                var success = CreateOutputFiles(splitCount, outputFilePathBase, out var outputFiles);

                if (!success)
                    return false;

                // Attempt to open the input file
                if (!fastaFileReader.OpenFile(inputFastaFilePath))
                {
                    SetLocalErrorCode(FastaFileSplitterErrorCode.ErrorReadingInputFile);
                    return false;
                }

                var progressMessage = string.Format("Splitting {0} file into {1} parts", Path.GetFileName(inputFastaFilePath), splitCount);
                UpdateProgress(progressMessage, 0f);

                // Read each protein in the input file and process appropriately
                InputFileProteinsProcessed = 0;
                InputFileLineSkipCount = 0;
                InputFileLinesRead = 0;

                while (true)
                {
                    var inputProteinFound = fastaFileReader.ReadNextProteinEntry();
                    InputFileLineSkipCount += fastaFileReader.LineSkipCount;

                    if (!inputProteinFound)
                    {
                        break;
                    }

                    InputFileProteinsProcessed++;
                    InputFileLinesRead = fastaFileReader.LinesRead;

                    var outputFileIndex = GetTargetFileIndex(splitCount, outputFiles);

                    if (outputFileIndex < 0 || outputFileIndex >= outputFiles.Count)
                    {
                        OnWarningEvent("Programming bug: index is outside the expected range. Defaulting to use outputFileIndex=0");
                        outputFileIndex = 0;
                    }

                    // Append the current protein to the file at index outputFileIndex
                    var description = fastaFileReader.ProteinDescription;
                    var sequence = fastaFileReader.ProteinSequence;
                    outputFiles[outputFileIndex].StoreProtein(fastaFileReader.ProteinName, description, sequence);

                    UpdateProgress(fastaFileReader.PercentFileProcessed());
                }

                // Close the input file
                fastaFileReader.CloseFile();

                // Close the output files
                // Store the info on the newly created files in mSplitFastaFileInfo
                foreach (var outputFile in outputFiles)
                {
                    outputFile.CloseFile();

                    var udtFileInfo = new FastaFileInfoType
                    {
                        FilePath = outputFile.OutputFilePath,
                        NumProteins = outputFile.TotalProteinsInFile,
                        NumResidues = outputFile.TotalResiduesInFile
                    };

                    SplitFastaFileInfo.Add(udtFileInfo);
                }

                // Create the stats file
                WriteStatsFile(outputFilePathBase + "_SplitStats.txt", outputFiles);
                Console.WriteLine();

                UpdateProgress("Done: Processed " + InputFileProteinsProcessed.ToString("###,##0") + " proteins (" + InputFileLinesRead.ToString("###,###,##0") + " lines)", 100f);
                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error in SplitFastaFile", ex);
                return false;
            }
        }

        /// <summary>
        /// Split inputFastaFilePath into multiple FASTA files, with each new FASTA file being roughly the same size
        /// The output file will be created in outputDirectoryPath (or the same directory as inputFastaFilePath if outputDirectoryPath is empty)
        /// </summary>
        /// <param name="inputFastaFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="targetFileSizeMB">Size, in MB, that each of the split FASTA files should have once splitting is complete</param>
        /// <param name="outputFileNameBaseOverride">When defined, use this name for the protein output filename rather than auto-defining the name</param>
        /// <returns>True if success, false if an error</returns>
        public bool SplitFastaFileBySize(string inputFastaFilePath, string outputDirectoryPath, int targetFileSizeMB, string outputFileNameBaseOverride = "")
        {
            try
            {
                var inputFile = new FileInfo(inputFastaFilePath);
                if (!inputFile.Exists)
                {
                    ShowErrorMessage("Input file not found: " + inputFastaFilePath);

                    if (ErrorCode == ProcessFilesErrorCodes.NoError)
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath);
                    }

                    return false;
                }

                var fileSizeMB = inputFile.Length / 1024.0 / 1024;

                // ReSharper disable once ConvertIfStatementToSwitchExpression
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (targetFileSizeMB == 0)
                {
                    targetFileSizeMB = SplitterOptions.DEFAULT_TARGET_FILE_SIZE_MB;
                }
                else if (targetFileSizeMB < SplitterOptions.MINIMUM_TARGET_FILE_SIZE_MB)
                {
                    targetFileSizeMB = SplitterOptions.MINIMUM_TARGET_FILE_SIZE_MB;
                }

                int splitCount;

                if (fileSizeMB <= targetFileSizeMB)
                {
                    ShowWarning(string.Format(
                        "Input file is {0:F0} MB, which is less than the target size of {1} MB; only one FASTA file will be created for {2}",
                        fileSizeMB, targetFileSizeMB, PathUtils.CompactPathString(inputFile.FullName)));

                    Console.WriteLine();
                    splitCount = 1;
                }
                else
                {
                    splitCount = (int)Math.Round(fileSizeMB / targetFileSizeMB);

                    if (splitCount <= 1)
                    {
                        ShowWarning(string.Format(
                            "Input file is {0:F0} MB, which is close to the target size of {1} MB; only one FASTA file will be created for {2}",
                            fileSizeMB, targetFileSizeMB, PathUtils.CompactPathString(inputFile.FullName)));

                        Console.WriteLine();
                    }
                    else
                    {
                        ShowMessage(string.Format(
                            "Will create {0} FASTA files, each ~{1} MB in size", splitCount, targetFileSizeMB));

                        Console.WriteLine();
                    }
                }

                return SplitFastaFile(inputFile.FullName, outputDirectoryPath, splitCount);
            }
            catch (Exception ex)
            {
                HandleException("Error in SplitFastaFileBySize", ex);
                return false;
            }
        }

        /// <summary>
        /// Main processing function
        /// Calls SplitFastaFile or SplitFastaFileBySize
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="parameterFilePath"></param>
        /// <param name="resetErrorCode"></param>
        /// <returns>True if success, False if failure</returns>
        public override bool ProcessFile(string inputFilePath, string outputDirectoryPath, string parameterFilePath, bool resetErrorCode)
        {
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
                if (string.IsNullOrWhiteSpace(inputFilePath))
                {
                    ShowMessage("Input file name is empty");
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath);
                    return false;
                }

                Console.WriteLine();
                ShowMessage("Parsing " + Path.GetFileName(inputFilePath));

                // Note that CleanupFilePaths() will update mOutputDirectoryPath, which is used by LogMessage()
                if (!CleanupFilePaths(ref inputFilePath, ref outputDirectoryPath))
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.FilePathError);
                    return false;
                }

                ResetProgress();

                try
                {
                    // Obtain the full path to the input file
                    var inputFile = new FileInfo(inputFilePath);
                    var inputFilePathFull = inputFile.FullName;

                    var success = Options.UseTargetFileSize ?
                        SplitFastaFileBySize(inputFilePathFull, outputDirectoryPath, Options.TargetFastaFileSizeMB) :
                        SplitFastaFile(inputFilePathFull, outputDirectoryPath, Options.SplitCount);

                    if (success)
                    {
                        ShowMessage(string.Empty, false);
                        return true;
                    }

                    SetLocalErrorCode(FastaFileSplitterErrorCode.UnspecifiedError);
                    ShowErrorMessage("Error");
                    return false;
                }
                catch (Exception ex)
                {
                    HandleException("Error calling SplitFastaFile", ex);
                    return false;
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFile", ex);
                return false;
            }
        }

        private void SetLocalErrorCode(FastaFileSplitterErrorCode newErrorCode, bool leaveExistingErrorCodeUnchanged = false)
        {
            if (leaveExistingErrorCodeUnchanged && LocalErrorCode != FastaFileSplitterErrorCode.NoError)
            {
                // An error code is already defined; do not change it
                return;
            }

            LocalErrorCode = newErrorCode;

            if (newErrorCode == FastaFileSplitterErrorCode.NoError)
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

        private void WriteStatsFile(string statsFilePath, IEnumerable<clsFastaOutputFile> outputFiles)
        {
            try
            {
                // Sleep 250 milliseconds to give the system time to close all of the file handles
                Thread.Sleep(250);

                using var statsFileWriter = new StreamWriter(new FileStream(statsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

                statsFileWriter.WriteLine("Section" + '\t' + "Proteins" + '\t' + "Residues" + '\t' + "FileSize_MB" + '\t' + "FileName");

                var fileNumber = 0;
                foreach (var outputFile in outputFiles)
                {
                    fileNumber++;

                    statsFileWriter.Write(fileNumber.ToString() + '\t' + outputFile.TotalProteinsInFile + '\t' + outputFile.TotalResiduesInFile);
                    try
                    {
                        var outputFileInfo = new FileInfo(outputFile.OutputFilePath);
                        statsFileWriter.Write('\t' + (outputFileInfo.Length / 1024.0d / 1024.0d).ToString("0.000"));
                    }
                    catch (Exception ex)
                    {
                        // Error creating a FileInfo object
                        statsFileWriter.Write('\t' + "Error: " + ex.Message);
                    }

                    statsFileWriter.WriteLine('\t' + Path.GetFileName(outputFile.OutputFilePath));
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in WriteStatsFile", ex);
            }
        }
    }
}