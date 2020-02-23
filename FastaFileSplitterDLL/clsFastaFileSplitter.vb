Option Strict On

' This class will read a protein fasta file and split it apart into the specified number of sections
' Although the splitting is random, each section will have a nearly identical number of residues
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Started April 1, 2010

Imports System.IO
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Threading
Imports PRISM

Public Class clsFastaFileSplitter
    Inherits FileProcessor.ProcessFilesBase

#Region "Constants and Enums"

    Public Const XML_SECTION_OPTIONS As String = "FastaFileSplitterOptions"

    Public Const DEFAULT_SPLIT_COUNT As Integer = 10

    ' Error codes specialized for this class
    Public Enum eFastaFileSplitterErrorCodes
        NoError = 0
        ErrorReadingInputFile = 1
        ErrorWritingOutputFile = 2
        InvalidMotif = 4
        UnspecifiedError = -1
    End Enum

#End Region

#Region "Structures"

    Public Structure FastaFileInfoType
        Public FilePath As String
        Public NumProteins As Integer
        Public NumResidues As Long
    End Structure

#End Region

#Region "Classwide Variables"
    Private mSplitCount As Integer

    Private mInputFileProteinsProcessed As Integer
    Private mInputFileLinesRead As Integer
    Private mInputFileLineSkipCount As Integer

    Private mSplitFastaFileInfo As List(Of FastaFileInfoType)

    Public FastaFileOptions As FastaFileOptionsClass

    Private mLocalErrorCode As eFastaFileSplitterErrorCodes
#End Region

#Region "Processing Options Interface Functions"

    Public ReadOnly Property InputFileProteinsProcessed As Integer
        Get
            Return mInputFileProteinsProcessed
        End Get
    End Property

    Public ReadOnly Property InputFileLinesRead As Integer
        Get
            Return mInputFileLinesRead
        End Get
    End Property

    Public ReadOnly Property InputFileLineSkipCount As Integer
        Get
            Return mInputFileLineSkipCount
        End Get
    End Property

    Public ReadOnly Property LocalErrorCode As eFastaFileSplitterErrorCodes
        Get
            Return mLocalErrorCode
        End Get
    End Property

    Public Property FastaFileSplitCount As Integer
        Get
            Return mSplitCount
        End Get
        Private Set
            If Value < 0 Then Value = 0
            mSplitCount = Value
        End Set
    End Property

    Public ReadOnly Property SplitFastaFileInfo As List(Of FastaFileInfoType)
        Get
            Return mSplitFastaFileInfo
        End Get
    End Property

#End Region

    ''' <summary>
    ''' Constructor
    ''' </summary>
    Public Sub New(Optional splitCount As Integer = 5)
        mFileDate = "February 23, 2020"
        InitializeLocalVariables()

        FastaFileSplitCount = splitCount
    End Sub

    Private Function CreateOutputFiles(
      splitCount As Integer,
      outputFilePathBase As String,
      ByRef outputFiles() As clsFastaOutputFile) As Boolean

        Dim fileNum = 0

        Try

            ReDim outputFiles(splitCount - 1)

            Dim formatCode = "0"
            If splitCount >= 10 Then
                Dim zeroCount = CInt(Math.Floor(Math.Log10(splitCount) + 1))
                For index = 2 To zeroCount
                    formatCode &= "0"
                Next
            End If

            ' Create each of the output files
            For fileNum = 1 To splitCount
                Dim outputFilePath = outputFilePathBase & "_" & splitCount & "x_" & fileNum.ToString(formatCode) & ".fasta"
                outputFiles(fileNum - 1) = New clsFastaOutputFile(outputFilePath)
            Next

            Return True
        Catch ex As Exception
            HandleException("Error creating output file " & fileNum, ex)
            SetLocalErrorCode(eFastaFileSplitterErrorCodes.ErrorWritingOutputFile)
            Return False
        End Try

    End Function

    Public Overrides Function GetDefaultExtensionsToParse() As IList(Of String)
        Dim extensionsToParse = New List(Of String) From {
            ".fasta",
            ".faa"
        }

        Return extensionsToParse

    End Function

    ''' <summary>
    ''' Get the error message, or an empty string oif no error
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Function GetErrorMessage() As String

        Dim errorMessage As String

        If ErrorCode = ProcessFilesErrorCodes.LocalizedError Or
           ErrorCode = ProcessFilesErrorCodes.NoError Then
            Select Case mLocalErrorCode
                Case eFastaFileSplitterErrorCodes.NoError
                    errorMessage = ""

                Case eFastaFileSplitterErrorCodes.ErrorReadingInputFile
                    errorMessage = "Error reading input file"

                Case eFastaFileSplitterErrorCodes.ErrorWritingOutputFile
                    errorMessage = "Error writing to the output file"

                Case eFastaFileSplitterErrorCodes.UnspecifiedError
                    errorMessage = "Unspecified localized error"

                Case Else
                    ' This shouldn't happen
                    errorMessage = "Unknown error state"
            End Select
        Else
            errorMessage = GetBaseClassErrorMessage()
        End If

        Return errorMessage

    End Function

    ''' <summary>
    ''' Examines the Residue counts in the files in outputFiles()
    ''' Will randomly choose one of the files whose residue count is less than the average residue count
    ''' </summary>
    ''' <param name="splitCount">Number of files the source .Fasta file is being split into</param>
    ''' <param name="outputFiles">Array of clsFastaOutputFile objects</param>
    ''' <returns>Randomly selected target file number (ranging from 1 to splitCount)</returns>
    ''' <remarks></remarks>
    Private Function GetTargetFileNum(splitCount As Integer, ByRef outputFiles() As clsFastaOutputFile) As Integer

        ' The strategy:
        ' 1) Compute the average residue count already stored to the files
        ' 2) Populate an array with the file numbers that have residue counts less than the average
        ' 3) Randomly choose one of those files

        ' Note: intentionally using a seed here
        Static rand As New Random(314159)

        If splitCount <= 1 Then
            ' Nothing to do; just return 1
            Return 1
        End If

        ' Compute the average number of residues stored in each file
        Dim sum As Long = 0
        For index = 0 To splitCount - 1
            sum += outputFiles(index).TotalResiduesInFile
        Next

        If sum = 0 Then
            ' We haven't stored any proteins yet
            ' Just return a random number between 1 and splitCount
            Return rand.Next(1, splitCount)
        End If

        Dim averageCount = sum / splitCount

        ' Populate candidates with the file numbers that have residue counts less than averageCount

        Dim candidates = New List(Of Integer)

        For index = 0 To splitCount - 1

            If outputFiles(index).TotalResiduesInFile < (averageCount) Then
                candidates.Add(index + 1)
            End If

        Next

        If candidates.Count > 0 Then
            ' Now randomly choose an entry in candidates
            ' Note that rand.Next(x,y) returns an integer in the range x <= i < y
            ' In other words, the range of random numbers returned is x through y-1
            '
            ' Thus, we pass candidateCount to the upper bound of rand.Next() to get a
            ' range of values from 0 to candidateCount-1

            Dim randomIndex = rand.Next(0, candidates.Count)

            ' Return the file number at index randomIndex in candidates
            Return candidates.Item(randomIndex)
        Else
            ' Pick a file at random
            Return rand.Next(1, splitCount)
        End If


    End Function

    Private Sub InitializeLocalVariables()
        mLocalErrorCode = eFastaFileSplitterErrorCodes.NoError

        FastaFileSplitCount = DEFAULT_SPLIT_COUNT

        mInputFileProteinsProcessed = 0
        mInputFileLinesRead = 0
        mInputFileLineSkipCount = 0

        mSplitFastaFileInfo = New List(Of FastaFileInfoType)

        FastaFileOptions = New FastaFileOptionsClass
    End Sub

    ''' <summary>
    ''' Examines the file's extension and true if it ends in .fasta or .faa
    ''' </summary>
    ''' <param name="filePath"></param>
    ''' <returns></returns>
    Public Shared Function IsFastaFile(filePath As String) As Boolean

        Dim fileExtension = Path.GetExtension(filePath)

        If fileExtension.Equals(".fasta", StringComparison.OrdinalIgnoreCase) OrElse
           fileExtension.Equals(".faa", StringComparison.OrdinalIgnoreCase) Then
            Return True
        Else
            Return False
        End If

    End Function

    Public Function LoadParameterFileSettings(parameterFilePath As String) As Boolean

        Dim settingsFile As New XmlSettingsFileAccessor

        Try

            If parameterFilePath Is Nothing OrElse parameterFilePath.Length = 0 Then
                ' No parameter file specified; nothing to load
                Return True
            End If

            If Not File.Exists(parameterFilePath) Then
                ' See if parameterFilePath points to a file in the same directory as the application
                parameterFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Path.GetFileName(parameterFilePath))
                If Not File.Exists(parameterFilePath) Then
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.ParameterFileNotFound)
                    Return False
                End If
            End If

            If settingsFile.LoadSettings(parameterFilePath) Then
                If Not settingsFile.SectionPresent(XML_SECTION_OPTIONS) Then
                    ShowErrorMessage("The node '<section name=""" & XML_SECTION_OPTIONS & """> was not found in the parameter file: " & parameterFilePath)
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile)
                    Return False
                Else

                    FastaFileSplitCount = settingsFile.GetParam(XML_SECTION_OPTIONS, "SplitCount", FastaFileSplitCount)
                End If
            End If

        Catch ex As Exception
            HandleException("Error in LoadParameterFileSettings", ex)
            Return False
        End Try

        Return True

    End Function

    Private Function OpenInputFile(
      inputFilePath As String,
      outputDirectoryPath As String,
      outputFileNameBaseBaseOverride As String,
      <Out> ByRef fastaFileReader As ProteinFileReader.FastaFileReader,
      <Out> ByRef outputFilePathBase As String) As Boolean

        fastaFileReader = Nothing
        outputFilePathBase = String.Empty

        Try

            If inputFilePath Is Nothing OrElse inputFilePath.Length = 0 Then
                SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath)
                Return False
            End If

            ' Verify that the input file exists
            If Not File.Exists(inputFilePath) Then
                SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath)
                Return False
            End If

            ' Instantiate the protein file reader object (assuming inputFilePath is a .Fasta file)
            fastaFileReader = New ProteinFileReader.FastaFileReader
            With fastaFileReader
                .ProteinLineStartChar = FastaFileOptions.ProteinLineStartChar
                .ProteinLineAccessionEndChar = FastaFileOptions.ProteinLineAccessionEndChar
            End With

            ' Define the output file name
            Dim outputFileNameBase = String.Empty
            If Not outputFileNameBaseBaseOverride Is Nothing AndAlso outputFileNameBaseBaseOverride.Length > 0 Then
                If Path.HasExtension(outputFileNameBaseBaseOverride) Then
                    outputFileNameBase = String.Copy(outputFileNameBaseBaseOverride)

                    ' Remove the extension
                    outputFileNameBase = Path.GetFileNameWithoutExtension(outputFileNameBase)
                Else
                    outputFileNameBase = String.Copy(outputFileNameBaseBaseOverride)
                End If
            End If

            If outputFileNameBase.Length = 0 Then
                ' Output file name is not defined; auto-define it
                outputFileNameBase = Path.GetFileNameWithoutExtension(inputFilePath)
            End If

            If outputDirectoryPath Is Nothing OrElse outputDirectoryPath.Length = 0 Then
                ' This code likely won't be reached since CleanupFilePaths() should have already initialized outputDirectoryPath
                Dim inputFile As FileInfo
                inputFile = New FileInfo(inputFilePath)

                outputDirectoryPath = inputFile.Directory.FullName
            End If

            ' Define the full path to output file base name
            outputFilePathBase = Path.Combine(outputDirectoryPath, outputFileNameBase)

            Return True

        Catch ex As Exception
            HandleException("OpenInputFile", ex)
            SetLocalErrorCode(eFastaFileSplitterErrorCodes.ErrorReadingInputFile)
            outputFilePathBase = String.Empty
            Return True
        End Try

    End Function

    Public Function SplitFastaFile(inputFastaFilePath As String, outputDirectoryPath As String, splitCount As Integer) As Boolean
        Return SplitFastaFile(inputFastaFilePath, outputDirectoryPath, String.Empty, splitCount)
    End Function

    ''' <summary>
    ''' Split inputFastaFilePath into splitCount parts
    ''' The output file will be created in outputDirectoryPath (or the same directory as inputFastaFilePath if outputDirectoryPath is empty)
    ''' </summary>
    ''' <param name="inputFastaFilePath"></param>
    ''' <param name="outputDirectoryPath"></param>
    ''' <param name="outputFileNameBaseOverride">When defined, use this name for the protein output filename rather than auto-defining the name</param>
    ''' <param name="splitCount"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function SplitFastaFile(
      inputFastaFilePath As String,
      outputDirectoryPath As String,
      outputFileNameBaseOverride As String,
      splitCount As Integer) As Boolean

        Dim fastaFileReader As ProteinFileReader.FastaFileReader = Nothing

        ' The following is a zero-based array that tracks the output file handles, along with the number of residues written to each file
        Dim outputFiles() As clsFastaOutputFile = Nothing
        Dim outputFilePathBase As String = String.Empty

        Dim inputProteinFound As Boolean

        Dim outputFileIndex As Integer

        Try
            mSplitFastaFileInfo.Clear()

            ' Open the input file and define the output file path
            Dim openSuccess = OpenInputFile(inputFastaFilePath,
               outputDirectoryPath,
               outputFileNameBaseOverride,
               fastaFileReader,
               outputFilePathBase)

            ' Abort processing if we couldn't successfully open the input file
            If Not openSuccess Then Return False

            If splitCount < 1 Then splitCount = 1

            ' Create the output files
            Dim success = CreateOutputFiles(splitCount, outputFilePathBase, outputFiles)
            If Not success Then Return False

            ' Attempt to open the input file
            If Not fastaFileReader.OpenFile(inputFastaFilePath) Then
                SetLocalErrorCode(eFastaFileSplitterErrorCodes.ErrorReadingInputFile)
                Return False
            End If

            UpdateProgress("Splitting fasta file: " & Path.GetFileName(inputFastaFilePath), 0)

            ' Read each protein in the input file and process appropriately
            mInputFileProteinsProcessed = 0
            mInputFileLineSkipCount = 0
            mInputFileLinesRead = 0
            Do
                inputProteinFound = fastaFileReader.ReadNextProteinEntry()
                mInputFileLineSkipCount += fastaFileReader.LineSkipCount

                If inputProteinFound Then
                    mInputFileProteinsProcessed += 1
                    mInputFileLinesRead = fastaFileReader.LinesRead

                    outputFileIndex = GetTargetFileNum(splitCount, outputFiles) - 1

                    If outputFileIndex < 0 OrElse outputFileIndex >= outputFiles.Count Then
                        Console.WriteLine("Programming bug: index is outside the expected range.  Defaulting to use OutputFileIndex=0")
                        outputFileIndex = 0
                    End If

                    ' Append the current protein to the file at index outputFileIndex
                    outputFiles(outputFileIndex).StoreProtein(fastaFileReader.ProteinName, fastaFileReader.ProteinDescription, fastaFileReader.ProteinSequence)

                    UpdateProgress(fastaFileReader.PercentFileProcessed())
                End If
            Loop While inputProteinFound

            ' Close the input file
            fastaFileReader.CloseFile()

            ' Close the output files
            ' Store the info on the newly created files in mSplitFastaFileInfo
            For index = 0 To splitCount - 1
                outputFiles(index).CloseFile()

                Dim udtFileInfo = New FastaFileInfoType With {
                    .FilePath = outputFiles(index).OutputFilePath,
                    .NumProteins = outputFiles(index).TotalProteinsInFile,
                    .NumResidues = outputFiles(index).TotalResiduesInFile
                }

                mSplitFastaFileInfo.Add(udtFileInfo)
            Next

            ' Create the stats file
            WriteStatsFile(outputFilePathBase & "_SplitStats.txt", splitCount, outputFiles)

            UpdateProgress("Done: Processed " & mInputFileProteinsProcessed.ToString("###,##0") & " proteins (" & mInputFileLinesRead.ToString("###,###,##0") & " lines)", 100)

            Return True

        Catch ex As Exception
            HandleException("Error in SplitFastaFile", ex)
            Return False
        End Try

    End Function

    ''' <summary>
    ''' Main processing function -- Calls SplitFastaFile
    ''' </summary>
    ''' <param name="inputFilePath"></param>
    ''' <param name="outputDirectoryPath"></param>
    ''' <param name="parameterFilePath"></param>
    ''' <param name="resetErrorCode"></param>
    ''' <returns>True if success, False if failure</returns>
    Public Overloads Overrides Function ProcessFile(
      inputFilePath As String,
      outputDirectoryPath As String,
      parameterFilePath As String,
      resetErrorCode As Boolean) As Boolean

        Dim file As FileInfo
        Dim inputFilePathFull As String

        Dim success As Boolean

        If resetErrorCode Then
            SetLocalErrorCode(eFastaFileSplitterErrorCodes.NoError)
        End If

        If Not LoadParameterFileSettings(parameterFilePath) Then
            ShowErrorMessage("Parameter file load error: " & parameterFilePath)

            If ErrorCode = ProcessFilesErrorCodes.NoError Then
                SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile)
            End If
            Return False
        End If

        Try
            If inputFilePath Is Nothing OrElse inputFilePath.Length = 0 Then
                ShowMessage("Input file name is empty")
                SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath)
            Else

                Console.WriteLine()
                Console.WriteLine("Parsing " & Path.GetFileName(inputFilePath))

                ' Note that CleanupFilePaths() will update mOutputDirectoryPath, which is used by LogMessage()
                If Not CleanupFilePaths(inputFilePath, outputDirectoryPath) Then
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.FilePathError)
                Else

                    ResetProgress()

                    Try
                        ' Obtain the full path to the input file
                        file = New FileInfo(inputFilePath)
                        inputFilePathFull = file.FullName

                        success = SplitFastaFile(inputFilePathFull, outputDirectoryPath, FastaFileSplitCount)

                        If success Then
                            ShowMessage(String.Empty, False)
                        Else
                            SetLocalErrorCode(eFastaFileSplitterErrorCodes.UnspecifiedError)
                            ShowErrorMessage("Error")
                        End If

                    Catch ex As Exception
                        HandleException("Error calling SplitFastaFile", ex)
                    End Try
                End If
            End If
        Catch ex As Exception
            HandleException("Error in ProcessFile", ex)
        End Try

        Return success

    End Function

    Private Sub SetLocalErrorCode(eNewErrorCode As eFastaFileSplitterErrorCodes)
        SetLocalErrorCode(eNewErrorCode, False)
    End Sub

    Private Sub SetLocalErrorCode(eNewErrorCode As eFastaFileSplitterErrorCodes, leaveExistingErrorCodeUnchanged As Boolean)

        If leaveExistingErrorCodeUnchanged AndAlso mLocalErrorCode <> eFastaFileSplitterErrorCodes.NoError Then
            ' An error code is already defined; do not change it
        Else
            mLocalErrorCode = eNewErrorCode

            If eNewErrorCode = eFastaFileSplitterErrorCodes.NoError Then
                If ErrorCode = ProcessFilesErrorCodes.LocalizedError Then
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.NoError)
                End If
            Else
                SetBaseClassErrorCode(ProcessFilesErrorCodes.LocalizedError)
            End If
        End If

    End Sub

    Private Sub WriteStatsFile(statsFilePath As String, splitCount As Integer, ByRef outputFiles() As clsFastaOutputFile)

        Try

            ' Sleep 250 milliseconds to give the system time to close all of the file handles
            Thread.Sleep(250)

            Using statsFileWriter = New StreamWriter(New FileStream(statsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

                statsFileWriter.WriteLine("Section" & ControlChars.Tab &
                     "Proteins" & ControlChars.Tab &
                     "Residues" & ControlChars.Tab &
                     "FileSize_MB" & ControlChars.Tab &
                     "FileName")

                For fileIndex = 0 To splitCount - 1

                    statsFileWriter.Write((fileIndex + 1).ToString & ControlChars.Tab &
                     outputFiles(fileIndex).TotalProteinsInFile & ControlChars.Tab &
                     outputFiles(fileIndex).TotalResiduesInFile)

                    Try
                        Dim outputFileInfo = New FileInfo(outputFiles(fileIndex).OutputFilePath)

                        statsFileWriter.Write(ControlChars.Tab & (outputFileInfo.Length / 1024.0 / 1024.0).ToString("0.000"))
                    Catch ex As Exception
                        ' Error obtaining a FileInfo object; that's odd
                        statsFileWriter.Write(ControlChars.Tab & "??")
                    End Try

                    statsFileWriter.WriteLine(ControlChars.Tab & Path.GetFileName(outputFiles(fileIndex).OutputFilePath))
                Next
            End Using

        Catch ex As Exception
            HandleException("Error in WriteStatsFile", ex)
        End Try

    End Sub

    ''' <summary>
    ''' Options class
    ''' </summary>
    Public Class FastaFileOptionsClass

        Public Sub New()
            mProteinLineStartChar = ">"c
            mProteinLineAccessionEndChar = " "c
        End Sub

#Region "Classwide Variables"

        Private mProteinLineStartChar As Char
        Private mProteinLineAccessionEndChar As Char
        Private mAddnlRefAccessionSepChar As Char

#End Region

#Region "Processing Options Interface Functions"

        Public Property ProteinLineStartChar As Char
            Get
                Return mProteinLineStartChar
            End Get
            Set
                If Not Value = Nothing Then
                    mProteinLineStartChar = Value
                End If
            End Set
        End Property

        Public Property ProteinLineAccessionEndChar As Char
            Get
                Return mProteinLineAccessionEndChar
            End Get
            Set
                If Not Value = Nothing Then
                    mProteinLineAccessionEndChar = Value
                End If
            End Set
        End Property

        ' ReSharper disable once UnusedMember.Global
        Public Property AddnlRefAccessionSepChar As Char
            Get
                Return mAddnlRefAccessionSepChar
            End Get
            Set
                If Not Value = Nothing Then
                    mAddnlRefAccessionSepChar = Value
                End If
            End Set
        End Property
#End Region

    End Class

End Class
