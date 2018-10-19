# Fasta File Splitter

## Overview

This program reads a protein fasta file and splits it apart 
into a number of sections.  Although the splitting is random, 
each section will have a nearly identical number of residues.

Use the /N switch to the define the number of sections.

Example:
`FastaFileSplitter.exe H_sapiens_IPI_2008-02-07.fasta /N:25`

## Syntax
```
FastaFileSplitter.exe /I:SourceFastaFile [/O:OutputFolderPath]
 [/N:SplitCount] [/P:ParameterFilePath]
 [/S:[MaxLevel]] [/A:AlternateOutputFolderPath] [/R] [/L]
```

The input file path can contain the wildcard character * and should point to a
fasta file. The output folder switch is optional.  If omitted, the output file
will be created in the same folder as the input file.

Use /N to define the number of parts to split the input file into.

The parameter file path is optional. If included, it should point to a valid XML
parameter file.

Use /S to process all valid files in the input folder and subfolders. Include a
number after /S (like /S:2) to limit the level of subfolders to examine. When using /S, 
you can redirect the output of the results using /A. When using /S, you can use /R 
to re-create the input folder hierarchy in the alternate output folder (if defined).

Use /L to log messages to a file.

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/

## License

The FASTA File Splitter is licensed under the 2-Clause BSD License; 
you may not use this file except in compliance with the License.  You may obtain 
a copy of the License at https://opensource.org/licenses/BSD-2-Clause

Copyright 2018 Battelle Memorial Institute
