Option Strict On

Imports System.IO

Public Class clsFastaOutputFile

#Region "Constants and Enums"
    Public Const DEFAULT_PROTEIN_LINE_START_CHAR As Char = ">"c
    Public Const DEFAULT_PROTEIN_LINE_ACCESSION_END_CHAR As Char = " "c
    Public Const DEFAULT_RESIDUES_PER_LINE As Integer = 60
#End Region

#Region "Classwide Variables"

    Protected mOutputFileIsOpen As Boolean
    Protected mOutputFilePath As String
    Protected mOutputFile As StreamWriter

    Protected mProteinLineStartChar As String
    Protected mProteinLineAccessionEndChar As String

    Protected mResiduesPerLine As Integer

    Protected mTotalProteinsInFile As Integer
    Protected mTotalResiduesInFile As Int64

#End Region

#Region "Properties"

    Public ReadOnly Property OutputFileIsOpen As Boolean
        Get
            Return mOutputFileIsOpen
        End Get
    End Property

    Public ReadOnly Property OutputFilePath As String
        Get
            Return mOutputFilePath
        End Get
    End Property

    Public Property ResiduesPerLine As Integer
        Get
            Return mResiduesPerLine
        End Get
        Set
            If Value < 1 Then Value = 1
            mResiduesPerLine = Value
        End Set
    End Property

    Public ReadOnly Property TotalProteinsInFile As Integer
        Get
            Return mTotalProteinsInFile
        End Get
    End Property

    Public ReadOnly Property TotalResiduesInFile As Int64
        Get
            Return mTotalResiduesInFile
        End Get
    End Property
#End Region

    Public Sub New(outputFilePath As String)
        Me.New(outputFilePath, DEFAULT_PROTEIN_LINE_START_CHAR, DEFAULT_PROTEIN_LINE_ACCESSION_END_CHAR)
    End Sub

    Public Sub New(outputFilePath As String, proteinLineStartChar As Char, proteinLineAccessionEndChar As Char)
        If outputFilePath Is Nothing OrElse outputFilePath.Length = 0 Then
            Throw New Exception("OutputFilePath is empty; cannot instantiate class")
        End If
        mOutputFilePath = outputFilePath

        mOutputFile = New StreamWriter(New FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        mOutputFileIsOpen = True

        mProteinLineStartChar = proteinLineStartChar
        mProteinLineAccessionEndChar = proteinLineAccessionEndChar

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

    Public Sub StoreProtein(proteinName As String, ByRef description As String, ByRef sequence As String)

        If mOutputFileIsOpen Then

            Try
                ' Write out the protein header and description line
                mOutputFile.WriteLine(mProteinLineStartChar & proteinName & mProteinLineAccessionEndChar & description)

                ' Now write out the residues, storing mResiduesPerLine residues per line
                Dim startIndex = 0
                Do While startIndex < sequence.Length
                    Dim charCount = mResiduesPerLine
                    If startIndex + charCount > sequence.Length Then
                        charCount = sequence.Length - startIndex
                    End If
                    mOutputFile.WriteLine(sequence.Substring(startIndex, charCount))
                    startIndex += charCount
                Loop

                mTotalProteinsInFile += 1
                mTotalResiduesInFile += sequence.Length

            Catch ex As Exception
                Console.WriteLine("Error in StoreProtein: " & ex.Message)
                Throw
            End Try

        End If

    End Sub

End Class
