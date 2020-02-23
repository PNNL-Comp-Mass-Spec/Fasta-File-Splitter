Option Strict On

' This program can be used to split apart a protein Fasta file into a number of sections
' Although the splitting is random, each section will have a nearly identical number of residues
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started April 1, 2010

' E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
' Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
' -------------------------------------------------------------------------------
'
' Licensed under the 2-Clause BSD License; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at
' https://opensource.org/licenses/BSD-2-Clause

Imports System.IO
Imports FastaFileSplitterDLL
Imports PRISM

Module modMain

    Public Const PROGRAM_DATE As String = "February 23, 2020"

    Private mInputFilePath As String

    Private mSplitCount As Integer

    Private mOutputDirectoryName As String              ' Optional
    Private mParameterFilePath As String                ' Optional

    Private mOutputDirectoryAlternatePath As String                ' Optional
    Private mRecreateDirectoryHierarchyInAlternatePath As Boolean  ' Optional

    Private mRecurseDirectories As Boolean
    Private mMaxLevelsToRecurse As Integer

    Private mLogMessagesToFile As Boolean

    Private WithEvents mFastaFileSplitter As clsFastaFileSplitter
    Private mLastProgressReportTime As DateTime
    Private mLastProgressReportValue As Integer

    Private Sub DisplayProgressPercent(percentComplete As Integer, addCarriageReturn As Boolean)
        If addCarriageReturn Then
            Console.WriteLine()
        End If
        If percentComplete > 100 Then percentComplete = 100
        Console.Write("Processing: " & percentComplete.ToString & "% ")
        If addCarriageReturn Then
            Console.WriteLine()
        End If
    End Sub

    Public Function Main() As Integer
        ' Returns 0 if no error, error code if an error

        Dim returnCode As Integer
        Dim commandLineParser As New clsParseCommandLine
        Dim proceed As Boolean

        ' Initialize the options
        mInputFilePath = String.Empty
        mSplitCount = clsFastaFileSplitter.DEFAULT_SPLIT_COUNT

        mOutputDirectoryName = String.Empty
        mParameterFilePath = String.Empty

        mRecurseDirectories = False
        mMaxLevelsToRecurse = 0

        mLogMessagesToFile = False

        Try
            proceed = False
            If commandLineParser.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(commandLineParser) Then proceed = True
            End If

            If Not proceed OrElse
               commandLineParser.NeedToShowHelp OrElse
               commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount = 0 OrElse
               mInputFilePath.Length = 0 Then
                ShowProgramHelp()
                returnCode = -1
            Else

                ' Note: mSplitCount and mSplitCount will be overridden if mParameterFilePath points to a valid parameter file that has these settings defined
                mFastaFileSplitter = New clsFastaFileSplitter(mSplitCount) With {
                    .LogMessagesToFile = mLogMessagesToFile
                }

                If mRecurseDirectories Then
                    If mFastaFileSplitter.ProcessFilesAndRecurseDirectories(
                        mInputFilePath, mOutputDirectoryName,
                        mOutputDirectoryAlternatePath, mRecreateDirectoryHierarchyInAlternatePath,
                        mParameterFilePath, mMaxLevelsToRecurse) Then
                        returnCode = 0
                    Else
                        returnCode = mFastaFileSplitter.ErrorCode
                    End If
                Else
                    If mFastaFileSplitter.ProcessFilesWildcard(mInputFilePath, mOutputDirectoryName, mParameterFilePath) Then
                        returnCode = 0
                    Else
                        returnCode = mFastaFileSplitter.ErrorCode
                        If returnCode <> 0 Then
                            Console.WriteLine("Error while processing: " & mFastaFileSplitter.GetErrorMessage())
                        End If
                    End If
                End If

                DisplayProgressPercent(mLastProgressReportValue, True)
            End If

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error occurred in modMain->Main", ex)
            returnCode = -1
        End Try

        Return returnCode

    End Function

    Private Function GetAppVersion() As String
        Return FileProcessor.ProcessFilesOrDirectoriesBase.GetAppVersion(PROGRAM_DATE)
    End Function

    Private Function SetOptionsUsingCommandLineParameters(commandLineParser As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim value As String = String.Empty
        Dim validParameters = New List(Of String) From {"I", "N", "O", "P", "S", "A", "R", "L"}

        Try
            ' Make sure no invalid parameters are present
            If commandLineParser.InvalidParametersPresent(validParameters) Then
                ConsoleMsgUtils.ShowErrors("Invalid command line parameters",
                  (From item In commandLineParser.InvalidParameters(validParameters) Select "/" + item).ToList())
                Return False
            End If


            ' Query commandLineParser to see if various parameters are present
            If commandLineParser.RetrieveValueForParameter("I", value) Then
                mInputFilePath = value
            ElseIf commandLineParser.NonSwitchParameterCount > 0 Then
                mInputFilePath = commandLineParser.RetrieveNonSwitchParameter(0)
            End If

            If commandLineParser.RetrieveValueForParameter("N", value) Then
                If Not Integer.TryParse(value, mSplitCount) Then
                    ConsoleMsgUtils.ShowError("Error parsing number from the /N parameter; use /N:25 to specify the file be split into " &
                                              clsFastaFileSplitter.DEFAULT_SPLIT_COUNT & " parts")

                    mSplitCount = clsFastaFileSplitter.DEFAULT_SPLIT_COUNT
                End If
            End If


            If commandLineParser.RetrieveValueForParameter("O", value) Then mOutputDirectoryName = value

            If commandLineParser.RetrieveValueForParameter("P", value) Then mParameterFilePath = value

            If commandLineParser.RetrieveValueForParameter("S", value) Then
                mRecurseDirectories = True
                If Not Integer.TryParse(value, mMaxLevelsToRecurse) Then
                    mMaxLevelsToRecurse = 0
                End If
            End If

            If commandLineParser.RetrieveValueForParameter("A", value) Then mOutputDirectoryAlternatePath = value
            If commandLineParser.IsParameterPresent("R") Then mRecreateDirectoryHierarchyInAlternatePath = True

            If commandLineParser.IsParameterPresent("L") Then mLogMessagesToFile = True

            Return True

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error parsing the command line parameters", ex)
            Return False
        End Try

    End Function

    Private Sub ShowProgramHelp()

        Try

            Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                "This program can be used to split apart a protein fasta file into a number of sections. " &
                "Although the splitting is random, each section will have a nearly identical number of residues."))
            Console.WriteLine()

            Console.WriteLine("Program syntax:")
            Console.WriteLine(Path.GetFileName(FileProcessor.ProcessFilesOrDirectoriesBase.GetAppPath()) &
                              " /I:SourceFastaFile [/O:OutputFolderPath]")
            Console.WriteLine(" [/N:SplitCount] [/P:ParameterFilePath] ")
            Console.WriteLine(" [/S:[MaxLevel]] [/A:AlternateOutputFolderPath] [/R] [/L]")
            Console.WriteLine()
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                "The input file path can contain the wildcard character * and should point to a fasta file. " &
                "The output folder switch is optional.  If omitted, the output file will be created in the same folder as the input file."))
            Console.WriteLine()

            Console.WriteLine("Use /N to define the number of parts to split the input file into.")
            Console.WriteLine()

            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("The parameter file path is optional. " &
                                                            "If included, it should point to a valid XML parameter file."))
            Console.WriteLine()

            Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                "Use /S to process all valid files in the input folder and subfolders. Include a number after /S (like /S:2) to limit the level of subfolders to examine. " &
                "When using /S, you can redirect the output of the results using /A. " &
                "When using /S, you can use /R to re-create the input folder hierarchy in the alternate output folder (if defined)."))

            Console.WriteLine("Use /L to log messages to a file.")
            Console.WriteLine()

            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2010")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov")
            Console.WriteLine("Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/")
            Console.WriteLine()

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            Threading.Thread.Sleep(750)

        Catch ex As Exception
            Console.WriteLine("Error displaying the program syntax: " & ex.Message)
        End Try

    End Sub

    Private Sub mFastaFileSplitter_ProgressChanged(taskDescription As String, percentComplete As Single) Handles mFastaFileSplitter.ProgressUpdate
        Const PERCENT_REPORT_INTERVAL = 25
        Const PROGRESS_DOT_INTERVAL_MSEC = 250

        If percentComplete >= mLastProgressReportValue Then
            If mLastProgressReportValue > 0 Then
                Console.WriteLine()
            End If
            DisplayProgressPercent(mLastProgressReportValue, False)
            mLastProgressReportValue += PERCENT_REPORT_INTERVAL
            mLastProgressReportTime = DateTime.UtcNow
        Else
            If DateTime.UtcNow.Subtract(mLastProgressReportTime).TotalMilliseconds > PROGRESS_DOT_INTERVAL_MSEC Then
                mLastProgressReportTime = DateTime.UtcNow
                Console.Write(".")
            End If
        End If
    End Sub

    Private Sub mFastaFileSplitter_ProgressReset() Handles mFastaFileSplitter.ProgressReset
        mLastProgressReportTime = DateTime.UtcNow
        mLastProgressReportValue = 0
    End Sub
End Module
