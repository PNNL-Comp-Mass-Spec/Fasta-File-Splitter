using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FastaFileSplitterLibrary;
using PRISM;
using PRISM.FileProcessor;
using PRISM.Logging;

// This program can be used to split apart a protein FASTA file into a number of sections
// Although the splitting is random, each section will have a nearly identical number of residues
//
// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Program started April 1, 2010

// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
// Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
// -------------------------------------------------------------------------------
//
// Licensed under the 2-Clause BSD License; you may not use this file except
// in compliance with the License.  You may obtain a copy of the License at
// https://opensource.org/licenses/BSD-2-Clause

namespace FastaFileSplitter
{
    /// <summary>
    /// FASTA file splitter program
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Program date
        /// </summary>
        public const string PROGRAM_DATE = "August 5, 2021";

        private static string mInputFilePath;
        private static int mSplitCount;
        private static string mOutputDirectoryName;              // Optional
        private static string mParameterFilePath;                // Optional
        private static string mOutputDirectoryAlternatePath;                // Optional
        private static bool mRecreateDirectoryHierarchyInAlternatePath;  // Optional
        private static bool mRecurseDirectories;
        private static int mMaxLevelsToRecurse;
        private static bool mLogMessagesToFile;

        private static DateTime mLastProgressReportTime;
        private static int mLastProgressReportValue;

        private static void DisplayProgressPercent(int percentComplete, bool addCarriageReturn)
        {
            if (addCarriageReturn)
            {
                Console.WriteLine();
            }

            if (percentComplete > 100)
                percentComplete = 100;

            Console.Write("Processing: " + percentComplete + "% ");
            if (addCarriageReturn)
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Main processing method
        /// </summary>
        /// <returns>0 if no error, error code if an error</returns>
        public static int Main()
        {
            var commandLineParser = new clsParseCommandLine();

            // Initialize the options
            mInputFilePath = string.Empty;
            mSplitCount = clsFastaFileSplitter.DEFAULT_SPLIT_COUNT;
            mOutputDirectoryName = string.Empty;
            mParameterFilePath = string.Empty;
            mRecurseDirectories = false;
            mMaxLevelsToRecurse = 0;
            mLogMessagesToFile = false;

            try
            {
                var proceed = commandLineParser.ParseCommandLine() && SetOptionsUsingCommandLineParameters(commandLineParser);

                if (!proceed || commandLineParser.NeedToShowHelp || commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount == 0 || mInputFilePath.Length == 0)
                {
                    ShowProgramHelp();
                    return -1;
                }

                // Note: mSplitCount and mSplitCount will be overridden if mParameterFilePath points to a valid parameter file that has these settings defined
                var fastaFileSplitter = new clsFastaFileSplitter(mSplitCount)
                {
                    LogMessagesToFile = mLogMessagesToFile
                };

                RegisterEvents(fastaFileSplitter);
                fastaFileSplitter.ProgressReset += ProgressReset;

                int returnCode;
                if (mRecurseDirectories)
                {
                    if (fastaFileSplitter.ProcessFilesAndRecurseDirectories(mInputFilePath, mOutputDirectoryName, mOutputDirectoryAlternatePath, mRecreateDirectoryHierarchyInAlternatePath, mParameterFilePath, mMaxLevelsToRecurse))
                    {
                        returnCode = 0;
                    }
                    else
                    {
                        if (fastaFileSplitter.ErrorCode == ProcessFilesBase.ProcessFilesErrorCodes.NoError)
                            returnCode = - 1;
                        else
                            returnCode = (int)fastaFileSplitter.ErrorCode;
                    }
                }
                else if (fastaFileSplitter.ProcessFilesWildcard(mInputFilePath, mOutputDirectoryName, mParameterFilePath))
                {
                    returnCode = 0;
                }
                else
                {
                    if (fastaFileSplitter.ErrorCode != ProcessFilesBase.ProcessFilesErrorCodes.NoError)
                    {
                        Console.WriteLine("Error while processing: " + fastaFileSplitter.GetErrorMessage());
                    }

                    returnCode = (int)fastaFileSplitter.ErrorCode;
                }

                DisplayProgressPercent(mLastProgressReportValue, true);
                return returnCode;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error occurred in modMain->Main", ex);
                return -1;
            }
        }

        private static string GetAppVersion()
        {
            return ProcessFilesOrDirectoriesBase.GetAppVersion(PROGRAM_DATE);
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine commandLineParser)
        {
            // Returns True if no problems; otherwise, returns false

            var validParameters = new List<string> { "I", "N", "O", "P", "S", "A", "R", "L" };

            try
            {
                // Make sure no invalid parameters are present
                if (commandLineParser.InvalidParametersPresent(validParameters))
                {
                    ConsoleMsgUtils.ShowErrors("Invalid command line parameters", (from item in commandLineParser.InvalidParameters(validParameters)
                                                                                   select ("/" + item)).ToList());
                    return false;
                }

                // Query commandLineParser to see if various parameters are present
                if (commandLineParser.RetrieveValueForParameter("I", out var inputFilePath))
                {
                    mInputFilePath = inputFilePath;
                }
                else if (commandLineParser.NonSwitchParameterCount > 0)
                {
                    mInputFilePath = commandLineParser.RetrieveNonSwitchParameter(0);
                }

                if (commandLineParser.RetrieveValueForParameter("N", out var splitCount))
                {
                    if (!int.TryParse(splitCount, out mSplitCount))
                    {
                        ConsoleMsgUtils.ShowError("Error parsing number from the /N parameter; use /N:25 to specify the file be split into " + clsFastaFileSplitter.DEFAULT_SPLIT_COUNT + " parts");
                        mSplitCount = clsFastaFileSplitter.DEFAULT_SPLIT_COUNT;
                    }
                }

                if (commandLineParser.RetrieveValueForParameter("O", out var outputDirectory))
                    mOutputDirectoryName = outputDirectory;

                if (commandLineParser.RetrieveValueForParameter("P", out var parameterFile))
                    mParameterFilePath = parameterFile;

                if (commandLineParser.RetrieveValueForParameter("S", out var recurseSubdirectories))
                {
                    mRecurseDirectories = true;
                    if (!int.TryParse(recurseSubdirectories, out mMaxLevelsToRecurse))
                    {
                        mMaxLevelsToRecurse = 0;
                    }
                }

                if (commandLineParser.RetrieveValueForParameter("A", out var alternateOutputDirectory))
                    mOutputDirectoryAlternatePath = alternateOutputDirectory;

                if (commandLineParser.IsParameterPresent("R"))
                    mRecreateDirectoryHierarchyInAlternatePath = true;

                if (commandLineParser.IsParameterPresent("L"))
                    mLogMessagesToFile = true;

                return true;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error parsing the command line parameters", ex);
                return false;
            }
        }

        private static void ShowProgramHelp()
        {
            try
            {
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "This program can be used to split apart a protein FASTA file into a number of sections. " +
                    "Although the splitting is random, each section will have a nearly identical number of residues."));
                Console.WriteLine();
                Console.WriteLine("Program syntax:");
                Console.WriteLine(Path.GetFileName(ProcessFilesOrDirectoriesBase.GetAppPath()) +
                                  " /I:SourceFastaFile [/O:OutputDirectoryPath]");
                Console.WriteLine(" [/N:SplitCount] [/P:ParameterFilePath] ");
                Console.WriteLine(" [/S:[MaxLevel]] [/A:AlternateOutputDirectoryPath] [/R] [/L]");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "The input file path can contain the wildcard character * and should point to a FASTA file. " +
                    "The output directory switch is optional. " +
                    "If omitted, the output file will be created in the same directory as the input file."));
                Console.WriteLine();
                Console.WriteLine("Use /N to define the number of parts to split the input file into.");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "The parameter file path is optional. " +
                    "If included, it should point to a valid XML parameter file."));
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /S to process all valid files in the input directory and subdirectories. " +
                    "Include a number after /S (like /S:2) to limit the level of subdirectories to examine. " +
                    "When using /S, you can redirect the output of the results using /A. " +
                    "When using /S, you can use /R to re-create the input directory hierarchy in the alternate output directory (if defined)."));
                Console.WriteLine("Use /L to log messages to a file.");
                Console.WriteLine();
                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2010");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();
                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                Thread.Sleep(750);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error displaying the program syntax: " + ex.Message);
            }
        }

        /// <summary>
        /// Use this method to chain events between classes
        /// </summary>
        /// <param name="sourceClass"></param>
        private static void RegisterEvents(IEventNotifier sourceClass)
        {
            // Ignore: sourceClass.DebugEvent += OnDebugEvent;
            sourceClass.StatusEvent += OnStatusEvent;
            sourceClass.ErrorEvent += OnErrorEvent;
            sourceClass.WarningEvent += OnWarningEvent;
            sourceClass.ProgressUpdate += ProgressChanged;
        }

        private static void OnErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void OnStatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void OnWarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private static void ProgressChanged(string taskDescription, float percentComplete)
        {
            const int PERCENT_REPORT_INTERVAL = 25;
            const int PROGRESS_DOT_INTERVAL_MSEC = 250;
            if (percentComplete >= mLastProgressReportValue)
            {
                if (mLastProgressReportValue > 0)
                {
                    Console.WriteLine();
                }

                DisplayProgressPercent(mLastProgressReportValue, false);
                mLastProgressReportValue += PERCENT_REPORT_INTERVAL;
                mLastProgressReportTime = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow.Subtract(mLastProgressReportTime).TotalMilliseconds > PROGRESS_DOT_INTERVAL_MSEC)
            {
                mLastProgressReportTime = DateTime.UtcNow;
                Console.Write(".");
            }
        }

        private static void ProgressReset()
        {
            mLastProgressReportTime = DateTime.UtcNow;
            mLastProgressReportValue = 0;
        }
    }
}