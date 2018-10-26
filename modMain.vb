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

    Public Const PROGRAM_DATE As String = "October 24, 2018"

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

    Private Sub DisplayProgressPercent(intPercentComplete As Integer, blnAddCarriageReturn As Boolean)
        If blnAddCarriageReturn Then
            Console.WriteLine()
        End If
        If intPercentComplete > 100 Then intPercentComplete = 100
        Console.Write("Processing: " & intPercentComplete.ToString & "% ")
        If blnAddCarriageReturn Then
            Console.WriteLine()
        End If
    End Sub

    Public Function Main() As Integer
        ' Returns 0 if no error, error code if an error

        Dim intReturnCode As Integer
        Dim objParseCommandLine As New clsParseCommandLine
        Dim blnProceed As Boolean

        ' Initialize the options
        mInputFilePath = String.Empty
        mSplitCount = clsFastaFileSplitter.DEFAULT_SPLIT_COUNT

        mOutputDirectoryName = String.Empty
        mParameterFilePath = String.Empty

        mRecurseDirectories = False
        mMaxLevelsToRecurse = 0

        mLogMessagesToFile = False

        Try
            blnProceed = False
            If objParseCommandLine.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
            End If

            If Not blnProceed OrElse
               objParseCommandLine.NeedToShowHelp OrElse
               objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount = 0 OrElse
               mInputFilePath.Length = 0 Then
                ShowProgramHelp()
                intReturnCode = -1
            Else

                mFastaFileSplitter = New clsFastaFileSplitter

                ' Note: the following settings will be overridden if mParameterFilePath points to a valid parameter file that has these settings defined
                With mFastaFileSplitter
                    .LogMessagesToFile = mLogMessagesToFile
                    .SplitCount = mSplitCount
                End With

                If mRecurseDirectories Then
                    If mFastaFileSplitter.ProcessFilesAndRecurseDirectories(
                        mInputFilePath, mOutputDirectoryName,
                        mOutputDirectoryAlternatePath, mRecreateDirectoryHierarchyInAlternatePath,
                        mParameterFilePath, mMaxLevelsToRecurse) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = mFastaFileSplitter.ErrorCode
                    End If
                Else
                    If mFastaFileSplitter.ProcessFilesWildcard(mInputFilePath, mOutputDirectoryName, mParameterFilePath) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = mFastaFileSplitter.ErrorCode
                        If intReturnCode <> 0 Then
                            Console.WriteLine("Error while processing: " & mFastaFileSplitter.GetErrorMessage())
                        End If
                    End If
                End If

                DisplayProgressPercent(mLastProgressReportValue, True)
            End If

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error occurred in modMain->Main", ex)
            intReturnCode = -1
        End Try

        Return intReturnCode

    End Function

    Private Function GetAppVersion() As String
        Return FileProcessor.ProcessFilesOrDirectoriesBase.GetAppVersion(PROGRAM_DATE)
    End Function

    Private Function SetOptionsUsingCommandLineParameters(objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
        Dim lstValidParameters = New List(Of String) From {"I", "N", "O", "P", "S", "A", "R", "L"}

        Try
            ' Make sure no invalid parameters are present
            If objParseCommandLine.InvalidParametersPresent(lstValidParameters) Then
                ConsoleMsgUtils.ShowErrors("Invalid command line parameters",
                  (From item In objParseCommandLine.InvalidParameters(lstValidParameters) Select "/" + item).ToList())
                Return False
            Else
                With objParseCommandLine
                    ' Query objParseCommandLine to see if various parameters are present
                    If .RetrieveValueForParameter("I", strValue) Then
                        mInputFilePath = strValue
                    ElseIf .NonSwitchParameterCount > 0 Then
                        mInputFilePath = .RetrieveNonSwitchParameter(0)
                    End If

                    If .RetrieveValueForParameter("N", strValue) Then
                        If Not Integer.TryParse(strValue, mSplitCount) Then
                            ConsoleMsgUtils.ShowError("Error parsing number from the /N parameter; use /N:25 to specify the file be split into " &
                                                      clsFastaFileSplitter.DEFAULT_SPLIT_COUNT & " parts")

                            mSplitCount = clsFastaFileSplitter.DEFAULT_SPLIT_COUNT
                        End If
                    End If


                    If .RetrieveValueForParameter("O", strValue) Then mOutputDirectoryName = strValue

                    If .RetrieveValueForParameter("P", strValue) Then mParameterFilePath = strValue

                    If .RetrieveValueForParameter("S", strValue) Then
                        mRecurseDirectories = True
                        If Not Integer.TryParse(strValue, mMaxLevelsToRecurse) Then
                            mMaxLevelsToRecurse = 0
                        End If
                    End If
                    If .RetrieveValueForParameter("A", strValue) Then mOutputDirectoryAlternatePath = strValue
                    If .IsParameterPresent("R") Then mRecreateDirectoryHierarchyInAlternatePath = True

                    If .IsParameterPresent("L") Then mLogMessagesToFile = True

                End With

                Return True
            End If

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error parsing the command line parameters", ex)
        End Try

        Return False

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
