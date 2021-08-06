#!/bin/bash

mono FastaFileSplitter.exe  H_sapiens_IPI_2008-02-07.fasta
mono FastaFileSplitter.exe  H_sapiens_IPI_2008-02-07.fasta /N:4
mono FastaFileSplitter.exe  H_sapiens_IPI_2008-02-07.fasta /MB:15
