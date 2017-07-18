Option Strict On

Public Class clsFastaOutputFile

#Region "Constants and Enums"
    Public Const DEFAULT_PROTEIN_LINE_START_CHAR As Char = ">"c
    Public Const DEFAULT_PROTEIN_LINE_ACCESSION_END_CHAR As Char = " "c
    Public Const DEFAULT_RESIDUES_PER_LINE As Integer = 60
#End Region

#Region "Classwide Variables"

    Protected mOutputFileIsOpen As Boolean
    Protected mOutputFilePath As String
    Protected mOutputFile As System.IO.StreamWriter

    Protected mProteinLineStartChar As String
    Protected mProteinLineAccessionEndChar As String

    Protected mResiduesPerLine As Integer

    Protected mTotalProteinsInFile As Integer
    Protected mTotalResiduesInFile As System.Int64

#End Region

#Region "Properties"

    Public ReadOnly Property OutputFileIsOpen() As Boolean
        Get
            Return mOutputFileIsOpen
        End Get
    End Property

    Public ReadOnly Property OutputFilePath() As String
        Get
            Return mOutputFilePath
        End Get
    End Property

    Public Property ResiduesPerLine() As Integer
        Get
            Return mResiduesPerLine
        End Get
        Set(ByVal value As Integer)
            If value < 1 Then value = 1
            mResiduesPerLine = value
        End Set
    End Property

    Public ReadOnly Property TotalProteinsInFile() As Integer
        Get
            Return mTotalProteinsInFile
        End Get
    End Property

    Public ReadOnly Property TotalResiduesInFile() As Int64
        Get
            Return mTotalResiduesInFile
        End Get
    End Property
#End Region

    Public Sub New(ByVal strOutputFilePath As String)
        Me.New(strOutputFilePath, DEFAULT_PROTEIN_LINE_START_CHAR, DEFAULT_PROTEIN_LINE_ACCESSION_END_CHAR)
    End Sub

    Public Sub New(ByVal strOutputFilePath As String, ByVal chProteinLineStartChar As Char, ByVal chProteinLineAccessionEndChar As Char)
        If strOutputFilePath Is Nothing OrElse strOutputFilePath.Length = 0 Then
            Throw New System.Exception("OutputFilePath is empty; cannot instantiate class")
        End If
        mOutputFilePath = strOutputFilePath

        mOutputFile = New System.IO.StreamWriter(New System.IO.FileStream(strOutputFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Write))
        mOutputFileIsOpen = True

        mProteinLineStartChar = chProteinLineStartChar
        mProteinLineAccessionEndChar = chProteinLineAccessionEndChar

        mResiduesPerLine = DEFAULT_RESIDUES_PER_LINE

        mTotalProteinsInFile = 0
        mTotalResiduesInFile = 0
    End Sub

    Public Sub CloseFile()
        Try
            If mOutputFileIsOpen AndAlso Not mOutputFile Is Nothing Then
                mOutputFile.Close()
                mOutputFileIsOpen = False
            End If
        Catch ex As Exception
            ' Ignore errors here
        End Try
    End Sub

    Public Sub StoreProtein(ByVal strProteinName As String, ByRef strDescription As String, ByRef strSequence As String)

        Dim intStartIndex As Integer
        Dim intCharCount As Integer

        If mOutputFileIsOpen Then

            Try
                ' Write out the protein header and description line
                mOutputFile.WriteLine(mProteinLineStartChar & strProteinName & mProteinLineAccessionEndChar & strDescription)

                ' Now write out the residues, storing mResiduesPerLine residues per line
                intStartIndex = 0
                Do While intStartIndex < strSequence.Length
                    intCharCount = mResiduesPerLine
                    If intStartIndex + intCharCount > strSequence.Length Then
                        intCharCount = strSequence.Length - intStartIndex
                    End If
                    mOutputFile.WriteLine(strSequence.Substring(intStartIndex, intCharCount))
                    intStartIndex += intCharCount
                Loop

                mTotalProteinsInFile += 1
                mTotalResiduesInFile += strSequence.Length

            Catch ex As Exception
                Console.WriteLine("Error in StoreProtein: " & ex.Message)
                Throw ex
            End Try

        End If

    End Sub

End Class
