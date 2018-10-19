Option Strict On

' This class will read a protein fasta file and split it apart into the specified number of sections
' Although the splitting is random, each section will have a nearly identical number of residues
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Started April 1, 2010

Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports PRISM

Public Class clsFastaFileSplitter
    Inherits FileProcessor.ProcessFilesBase

    Public Sub New()
        mFileDate = "July 23, 2018"
        InitializeLocalVariables()
    End Sub

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
    Public Structure udtProteinInfoType
        Public Name As String
        Public Description As String
        Public Sequence As String
    End Structure

    Public Structure udtFastaFileInfoType
        Public FilePath As String
        Public NumProteins As Integer
        Public NumResidues As Int64
    End Structure

#End Region

#Region "Classwide Variables"
    Private mSplitCount As Integer

    Private mInputFileProteinsProcessed As Integer
    Private mInputFileLinesRead As Integer
    Private mInputFileLineSkipCount As Integer

    Private mSplitFastaFileInfo As List(Of udtFastaFileInfoType)

    Public FastaFileOptions As FastaFileOptionsClass

    Private mLocalErrorCode As eFastaFileSplitterErrorCodes
#End Region

#Region "Processing Options Interface Functions"

    Public ReadOnly Property InputFileProteinsProcessed() As Integer
        Get
            Return mInputFileProteinsProcessed
        End Get
    End Property

    Public ReadOnly Property InputFileLinesRead() As Integer
        Get
            Return mInputFileLinesRead
        End Get
    End Property

    Public ReadOnly Property InputFileLineSkipCount() As Integer
        Get
            Return mInputFileLineSkipCount
        End Get
    End Property

    Public ReadOnly Property LocalErrorCode() As eFastaFileSplitterErrorCodes
        Get
            Return mLocalErrorCode
        End Get
    End Property

    Public Property SplitCount() As Integer
        Get
            Return mSplitCount
        End Get
        Set
            If Value < 0 Then Value = 0
            mSplitCount = Value
        End Set
    End Property

    Public ReadOnly Property SplitFastaFileInfo() As List(Of udtFastaFileInfoType)
        Get
            Return mSplitFastaFileInfo
        End Get
    End Property

#End Region

    Protected Function CreateOutputFiles(intSplitCount As Integer,
                                         strOutputFilePathBase As String,
                                         ByRef objOutputFiles() As clsFastaOutputFile) As Boolean
        Dim strFormatCode As String

        Dim intZeroCount As Integer
        Dim intFileNum As Integer

        Dim strOutputfilePath As String
        Dim blnSuccess As Boolean

        Try

            ReDim objOutputFiles(intSplitCount - 1)

            strFormatCode = "0"
            If intSplitCount >= 10 Then
                intZeroCount = CInt(Math.Floor(Math.Log10(intSplitCount) + 1))
                For intIndex = 2 To intZeroCount
                    strFormatCode &= "0"
                Next
            End If

            ' Create each of the output files
            For intFileNum = 1 To intSplitCount
                strOutputfilePath = strOutputFilePathBase & "_" & intSplitCount & "x_" & intFileNum.ToString(strFormatCode) & ".fasta"
                objOutputFiles(intFileNum - 1) = New clsFastaOutputFile(strOutputfilePath)
            Next

            blnSuccess = True
        Catch ex As Exception
            HandleException("Error creating output file " & intFileNum, ex)
            SetLocalErrorCode(eFastaFileSplitterErrorCodes.ErrorWritingOutputFile)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Overrides Function GetDefaultExtensionsToParse() As IList(Of String)
        Dim strExtensionsToParse = New List(Of String) From {
            ".fasta"
        }

        Return strExtensionsToParse

    End Function

    Public Overrides Function GetErrorMessage() As String
        ' Returns "" if no error

        Dim strErrorMessage As String

        If MyBase.ErrorCode = ProcessFilesErrorCodes.LocalizedError Or
           MyBase.ErrorCode = ProcessFilesErrorCodes.NoError Then
            Select Case mLocalErrorCode
                Case eFastaFileSplitterErrorCodes.NoError
                    strErrorMessage = ""

                Case eFastaFileSplitterErrorCodes.ErrorReadingInputFile
                    strErrorMessage = "Error reading input file"

                Case eFastaFileSplitterErrorCodes.ErrorWritingOutputFile
                    strErrorMessage = "Error writing to the output file"

                Case eFastaFileSplitterErrorCodes.UnspecifiedError
                    strErrorMessage = "Unspecified localized error"

                Case Else
                    ' This shouldn't happen
                    strErrorMessage = "Unknown error state"
            End Select
        Else
            strErrorMessage = MyBase.GetBaseClassErrorMessage()
        End If

        Return strErrorMessage

    End Function

    ''' <summary>
    ''' Examines the Residue counts in the files in objOutputFiles()
    ''' Will randomly choose one of the files whose residue count is less than the average residue count
    ''' </summary>
    ''' <param name="intSplitCount">Number of files the source .Fasta file is being split into</param>
    ''' <param name="objOutputFiles">Array of clsFastaOutputFile objects</param>
    ''' <returns>Randomly selected target file number (ranging from 1 to intSplitCount)</returns>
    ''' <remarks></remarks>
    Protected Function GetTargetFileNum(intSplitCount As Integer, ByRef objOutputFiles() As clsFastaOutputFile) As Integer

        ' The strategy:
        ' 1) Compute the average residue count already stored to the files
        ' 2) Populate an array with the file numbers that have residue counts less than the average
        ' 3) Randomly choose one of those files

        ' Note: intentially using a seed here
        Static objRand As New Random(314159)

        Dim lngSum As Int64
        Dim dblAverageCount As Double

        Dim lstCandidates As List(Of Integer)

        Dim intIndex As Integer
        Dim intRandomIndex As Integer

        If intSplitCount <= 1 Then
            ' Nothing to do; just return 1
            Return 1
        End If

        ' Compute the average number of residues stored in each file
        lngSum = 0
        For intIndex = 0 To intSplitCount - 1
            lngSum += objOutputFiles(intIndex).TotalResiduesInFile
        Next

        If lngSum = 0 Then
            ' We haven't stored any proteins yet
            ' Just return a random number between 1 and intSplitCount
            Return objRand.Next(1, intSplitCount)
        End If

        dblAverageCount = lngSum / intSplitCount

        ' Populate intCandidates with the file numbers that have residue counts less than dblAverageCount

        lstCandidates = New List(Of Integer)

        For intIndex = 0 To intSplitCount - 1

            If objOutputFiles(intIndex).TotalResiduesInFile < (dblAverageCount) Then
                lstCandidates.Add(intIndex + 1)
            End If

        Next

        If lstCandidates.Count > 0 Then
            ' Now randomly choose an entry in intCandidates
            ' Note that objRand.Next(x,y) returns an integer in the range x <= i < y
            ' In other words, the range of random numbers returned is x through y-1
            '
            ' Thus, we pass intCandidateCount to the upper bound of objRand.Next() to get a
            ' range of values from 0 to intCandidateCount-1

            intRandomIndex = objRand.Next(0, lstCandidates.Count)

            ' Return the file number at index intRandomIndex in intCandidates
            Return lstCandidates.Item(intRandomIndex)
        Else
            ' Pick a file at random
            Return objRand.Next(1, intSplitCount)
        End If


    End Function

    Private Sub InitializeLocalVariables()
        mLocalErrorCode = eFastaFileSplitterErrorCodes.NoError

        mSplitCount = DEFAULT_SPLIT_COUNT

        mInputFileProteinsProcessed = 0
        mInputFileLinesRead = 0
        mInputFileLineSkipCount = 0

        mSplitFastaFileInfo = New List(Of udtFastaFileInfoType)

        FastaFileOptions = New FastaFileOptionsClass

    End Sub

    Public Shared Function IsFastaFile(strFilePath As String) As Boolean
        ' Examines the file's extension and true if it ends in .fasta

        If Path.GetExtension(strFilePath).ToLower = ".fasta" Then
            Return True
        Else
            Return False
        End If

    End Function

    Public Function LoadParameterFileSettings(strParameterFilePath As String) As Boolean

        Dim objSettingsFile As New XmlSettingsFileAccessor

        Try

            If strParameterFilePath Is Nothing OrElse strParameterFilePath.Length = 0 Then
                ' No parameter file specified; nothing to load
                Return True
            End If

            If Not File.Exists(strParameterFilePath) Then
                ' See if strParameterFilePath points to a file in the same directory as the application
                strParameterFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Path.GetFileName(strParameterFilePath))
                If Not File.Exists(strParameterFilePath) Then
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.ParameterFileNotFound)
                    Return False
                End If
            End If

            If objSettingsFile.LoadSettings(strParameterFilePath) Then
                If Not objSettingsFile.SectionPresent(XML_SECTION_OPTIONS) Then
                    ShowErrorMessage("The node '<section name=""" & XML_SECTION_OPTIONS & """> was not found in the parameter file: " & strParameterFilePath)
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile)
                    Return False
                Else

                    Me.SplitCount = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "SplitCount", Me.SplitCount)
                End If
            End If

        Catch ex As Exception
            HandleException("Error in LoadParameterFileSettings", ex)
            Return False
        End Try

        Return True

    End Function

    Protected Function OpenInputFile(strInputFilePath As String,
                                     strOutputDirectoryPath As String,
                                     strOutputFileNameBaseBaseOverride As String,
                                     ByRef objFastaFileReader As ProteinFileReader.FastaFileReader,
                                     ByRef strOutputFilePathBase As String) As Boolean

        Dim blnSuccess As Boolean
        Dim strOutputFileNameBase As String

        Try

            If strInputFilePath Is Nothing OrElse strInputFilePath.Length = 0 Then
                SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath)
            Else

                ' Verify that the input file exists
                If Not File.Exists(strInputFilePath) Then
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath)
                    blnSuccess = False
                    Exit Try
                End If

                ' Instantiate the protein file reader object (assuming strInputFilePath is a .Fasta file)
                objFastaFileReader = New ProteinFileReader.FastaFileReader
                With objFastaFileReader
                    .ProteinLineStartChar = FastaFileOptions.ProteinLineStartChar
                    .ProteinLineAccessionEndChar = FastaFileOptions.ProteinLineAccessionEndChar
                End With

                ' Define the output file name
                strOutputFileNameBase = String.Empty
                If Not strOutputFileNameBaseBaseOverride Is Nothing AndAlso strOutputFileNameBaseBaseOverride.Length > 0 Then
                    If Path.HasExtension(strOutputFileNameBaseBaseOverride) Then
                        strOutputFileNameBase = String.Copy(strOutputFileNameBaseBaseOverride)

                        ' Remove the extension
                        strOutputFileNameBase = Path.GetFileNameWithoutExtension(strOutputFileNameBase)
                    Else
                        strOutputFileNameBase = String.Copy(strOutputFileNameBaseBaseOverride)
                    End If
                End If

                If strOutputFileNameBase.Length = 0 Then
                    ' Output file name is not defined; auto-define it
                    strOutputFileNameBase = Path.GetFileNameWithoutExtension(strInputFilePath)
                End If

                If strOutputDirectoryPath Is Nothing OrElse strOutputDirectoryPath.Length = 0 Then
                    ' This code likely won't be reached since CleanupFilePaths() should have already initialized strOutputDirectoryPath
                    Dim fiInputFile As FileInfo
                    fiInputFile = New FileInfo(strInputFilePath)

                    strOutputDirectoryPath = fiInputFile.Directory.FullName
                End If

                ' Define the full path to output file base name
                strOutputFilePathBase = Path.Combine(strOutputDirectoryPath, strOutputFileNameBase)

                blnSuccess = True
            End If

        Catch ex As Exception
            HandleException("OpenInputFile", ex)
            SetLocalErrorCode(eFastaFileSplitterErrorCodes.ErrorReadingInputFile)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Function SplitFastaFile(strInputFastaFilePath As String, strOutputDirectoryPath As String, intSplitCount As Integer) As Boolean
        Return SplitFastaFile(strInputFastaFilePath, strOutputDirectoryPath, String.Empty, intSplitCount)
    End Function

    ''' <summary>
    ''' Split strInputFastaFilePath into intSplitCount parts
    ''' The output file will be created in strOutputDirectoryPath (or the same directory as strInputFastaFilePath if strOutputDirectoryPath is empty)
    ''' </summary>
    ''' <param name="strInputFastaFilePath"></param>
    ''' <param name="strOutputDirectoryPath"></param>
    ''' <param name="strOutputFileNameBaseOverride"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function SplitFastaFile(strInputFastaFilePath As String,
                                   strOutputDirectoryPath As String,
                                   strOutputFileNameBaseOverride As String,
                                   intSplitCount As Integer) As Boolean

        ' If strOutputFileNameBaseOverride is defined, then uses that name for the protein output filename rather than auto-defining the name

        Dim objFastaFileReader As ProteinFileReader.FastaFileReader = Nothing

        ' The following is a zero-based array that tracks the output file handles, along with the number of residues written to each file
        Dim objOutputFiles() As clsFastaOutputFile = Nothing
        Dim strOutputFilePathBase As String = String.Empty

        Dim blnSuccess = False
        Dim blnInputProteinFound As Boolean

        Dim intOutputFileIndex As Integer

        Try
            mSplitFastaFileInfo.Clear()

            ' Open the input file and define the output file path
            blnSuccess = OpenInputFile(strInputFastaFilePath,
               strOutputDirectoryPath,
               strOutputFileNameBaseOverride,
               objFastaFileReader,
               strOutputFilePathBase)

            ' Abort processing if we couldn't successfully open the input file
            If Not blnSuccess Then Return False

            If intSplitCount < 1 Then intSplitCount = 1

            ' Create the output files
            blnSuccess = CreateOutputFiles(intSplitCount, strOutputFilePathBase, objOutputFiles)
            If Not blnSuccess Then Return False

            ' Attempt to open the input file
            If Not objFastaFileReader.OpenFile(strInputFastaFilePath) Then
                SetLocalErrorCode(eFastaFileSplitterErrorCodes.ErrorReadingInputFile)
                Return False
            End If

            UpdateProgress("Splitting fasta file: " & Path.GetFileName(strInputFastaFilePath), 0)


            ' Read each protein in the input file and process appropriately
            mInputFileProteinsProcessed = 0
            mInputFileLineSkipCount = 0
            mInputFileLinesRead = 0
            Do
                blnInputProteinFound = objFastaFileReader.ReadNextProteinEntry()
                mInputFileLineSkipCount += objFastaFileReader.LineSkipCount

                If blnInputProteinFound Then
                    mInputFileProteinsProcessed += 1
                    mInputFileLinesRead = objFastaFileReader.LinesRead

                    intOutputFileIndex = GetTargetFileNum(intSplitCount, objOutputFiles) - 1

                    If intOutputFileIndex < 0 OrElse intOutputFileIndex >= objOutputFiles.Count Then
                        Console.WriteLine("Programming bug: index is outside the expected range.  Defaulting to use OutputFileIndex=0")
                        intOutputFileIndex = 0
                    End If

                    ' Append the current protein to the file at index intOutputFileIndex
                    objOutputFiles(intOutputFileIndex).StoreProtein(objFastaFileReader.ProteinName, objFastaFileReader.ProteinDescription, objFastaFileReader.ProteinSequence)

                    UpdateProgress(objFastaFileReader.PercentFileProcessed())
                End If
            Loop While blnInputProteinFound

            ' Close the input file
            objFastaFileReader.CloseFile()

            ' Close the output files
            ' Store the info on the newly created files in mSplitFastaFileInfo
            For intIndex = 0 To intSplitCount - 1
                objOutputFiles(intIndex).CloseFile()

                Dim udtFileInfo = New udtFastaFileInfoType
                udtFileInfo.FilePath = objOutputFiles(intIndex).OutputFilePath
                udtFileInfo.NumProteins = objOutputFiles(intIndex).TotalProteinsInFile
                udtFileInfo.NumResidues = objOutputFiles(intIndex).TotalResiduesInFile

                mSplitFastaFileInfo.Add(udtFileInfo)
            Next

            ' Create the stats file
            WriteStatsFile(strOutputFilePathBase & "_SplitStats.txt", intSplitCount, objOutputFiles)

            UpdateProgress("Done: Processed " & mInputFileProteinsProcessed.ToString("###,##0") & " proteins (" & mInputFileLinesRead.ToString("###,###,##0") & " lines)", 100)

            blnSuccess = True

        Catch ex As Exception
            HandleException("Error in SplitFastaFile", ex)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    ''' <summary>
    ''' Main processing function -- Calls SplitFastaFile
    ''' </summary>
    ''' <param name="strInputFilePath"></param>
    ''' <param name="strOutputDirectoryPath"></param>
    ''' <param name="strParameterFilePath"></param>
    ''' <param name="blnResetErrorCode"></param>
    ''' <returns></returns>
    Public Overloads Overrides Function ProcessFile(strInputFilePath As String, strOutputDirectoryPath As String, strParameterFilePath As String, blnResetErrorCode As Boolean) As Boolean
        ' Returns True if success, False if failure

        Dim ioFile As FileInfo
        Dim strInputFilePathFull As String

        Dim blnSuccess As Boolean

        If blnResetErrorCode Then
            SetLocalErrorCode(eFastaFileSplitterErrorCodes.NoError)
        End If

        If Not LoadParameterFileSettings(strParameterFilePath) Then
            ShowErrorMessage("Parameter file load error: " & strParameterFilePath)

            If MyBase.ErrorCode = ProcessFilesErrorCodes.NoError Then
                MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile)
            End If
            Return False
        End If

        Try
            If strInputFilePath Is Nothing OrElse strInputFilePath.Length = 0 Then
                ShowMessage("Input file name is empty")
                MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath)
            Else

                Console.WriteLine()
                Console.WriteLine("Parsing " & Path.GetFileName(strInputFilePath))

                ' Note that CleanupFilePaths() will update mOutputDirectoryPath, which is used by LogMessage()
                If Not CleanupFilePaths(strInputFilePath, strOutputDirectoryPath) Then
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.FilePathError)
                Else

                    MyBase.ResetProgress()

                    Try
                        ' Obtain the full path to the input file
                        ioFile = New FileInfo(strInputFilePath)
                        strInputFilePathFull = ioFile.FullName

                        blnSuccess = SplitFastaFile(strInputFilePathFull, strOutputDirectoryPath, mSplitCount)

                        If blnSuccess Then
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

        Return blnSuccess

    End Function

    Private Sub SetLocalErrorCode(eNewErrorCode As eFastaFileSplitterErrorCodes)
        SetLocalErrorCode(eNewErrorCode, False)
    End Sub

    Private Sub SetLocalErrorCode(eNewErrorCode As eFastaFileSplitterErrorCodes, blnLeaveExistingErrorCodeUnchanged As Boolean)

        If blnLeaveExistingErrorCodeUnchanged AndAlso mLocalErrorCode <> eFastaFileSplitterErrorCodes.NoError Then
            ' An error code is already defined; do not change it
        Else
            mLocalErrorCode = eNewErrorCode

            If eNewErrorCode = eFastaFileSplitterErrorCodes.NoError Then
                If MyBase.ErrorCode = ProcessFilesErrorCodes.LocalizedError Then
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.NoError)
                End If
            Else
                MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.LocalizedError)
            End If
        End If

    End Sub

    Protected Sub WriteStatsFile(strStatsFilePath As String, intSplitCount As Integer, ByRef objOutputFiles() As clsFastaOutputFile)

        Dim intFileIndex As Integer

        Dim ioFileInfo As FileInfo

        Try

            ' Sleep 250 milliseconds to give the system time to close all of the file handles
            Thread.Sleep(250)

            Using statsFileWriter = New StreamWriter(New FileStream(strStatsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

                statsFileWriter.WriteLine("Section" & ControlChars.Tab &
                     "Proteins" & ControlChars.Tab &
                     "Residues" & ControlChars.Tab &
                     "FileSize_MB" & ControlChars.Tab &
                     "FileName")

                For intFileIndex = 0 To intSplitCount - 1

                    statsFileWriter.Write((intFileIndex + 1).ToString & ControlChars.Tab &
                     objOutputFiles(intFileIndex).TotalProteinsInFile & ControlChars.Tab &
                     objOutputFiles(intFileIndex).TotalResiduesInFile)

                    Try
                        ioFileInfo = New FileInfo(objOutputFiles(intFileIndex).OutputFilePath)

                        statsFileWriter.Write(ControlChars.Tab & (ioFileInfo.Length / 1024.0 / 1024.0).ToString("0.000"))
                    Catch ex As Exception
                        ' Error obtaining a FileInfo object; that's odd
                        statsFileWriter.Write(ControlChars.Tab & "??")
                    End Try

                    statsFileWriter.WriteLine(ControlChars.Tab & Path.GetFileName(objOutputFiles(intFileIndex).OutputFilePath))
                Next
            End Using

        Catch ex As Exception
            HandleException("Error in WriteStatsFile", ex)
        End Try

    End Sub

    ' Options class
    Public Class FastaFileOptionsClass

        Public Sub New()
            mProteinLineStartChar = ">"c
            mProteinLineAccessionEndChar = " "c
        End Sub

#Region "Classwide Variables"
        Private mReadonlyClass As Boolean

        Private mProteinLineStartChar As Char
        Private mProteinLineAccessionEndChar As Char

        Private mLookForAddnlRefInDescription As Boolean

        Private mAddnlRefSepChar As Char
        Private mAddnlRefAccessionSepChar As Char

#End Region

#Region "Processing Options Interface Functions"
        Public Property ReadonlyClass() As Boolean
            Get
                Return mReadonlyClass
            End Get
            Set
                If Not mReadonlyClass Then
                    mReadonlyClass = Value
                End If
            End Set
        End Property

        Public Property ProteinLineStartChar() As Char
            Get
                Return mProteinLineStartChar
            End Get
            Set
                If Not Value = Nothing AndAlso Not mReadonlyClass Then
                    mProteinLineStartChar = Value
                End If
            End Set
        End Property

        Public Property ProteinLineAccessionEndChar() As Char
            Get
                Return mProteinLineAccessionEndChar
            End Get
            Set
                If Not Value = Nothing AndAlso Not mReadonlyClass Then
                    mProteinLineAccessionEndChar = Value
                End If
            End Set
        End Property

        Public Property LookForAddnlRefInDescription() As Boolean
            Get
                Return mLookForAddnlRefInDescription
            End Get
            Set
                If Not mReadonlyClass Then
                    mLookForAddnlRefInDescription = Value
                End If
            End Set
        End Property

        Public Property AddnlRefSepChar() As Char
            Get
                Return mAddnlRefSepChar
            End Get
            Set
                If Not Value = Nothing AndAlso Not mReadonlyClass Then
                    mAddnlRefSepChar = Value
                End If
            End Set
        End Property

        Public Property AddnlRefAccessionSepChar() As Char
            Get
                Return mAddnlRefAccessionSepChar
            End Get
            Set
                If Not Value = Nothing AndAlso Not mReadonlyClass Then
                    mAddnlRefAccessionSepChar = Value
                End If
            End Set
        End Property
#End Region

    End Class

End Class
