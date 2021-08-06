# Fasta File Splitter

## Overview

This program reads a protein FASTA file and splits it apart 
into a number of sections.  Although the splitting is random, 
each section will have a nearly identical number of residues.

Use the `/N` switch to the define the number of files to create.

Alternatively, use the `/MB` switch to specify the size of each file.

Examples:
`FastaFileSplitter.exe H_sapiens_IPI_2008-02-07.fasta /N:25`
`FastaFileSplitter.exe H_sapiens_IPI_2008-02-07.fasta /MB:50`

## Syntax
```
FastaFileSplitter.exe /I:SourceFastaFile [/O:OutputDirectoryPath]
 [/N:SplitCount] [/MB:TargetSizeMB] [/P:ParameterFilePath]
 [/S:[MaxLevel]] [/A:AlternateOutputDirectoryPath] [/R] [/L]
```

The input file path can contain the wildcard character * and should point to a
FASTA file. The output directory switch is optional. If omitted, the output file
will be created in the same directory as the input file.

Use `/N` to define the number of parts to split the input file into.
* For example, `/N:10` will split the input FASTA file into 10 parts

Alternatively, use `/MB` to specify the size of the split FASTA files, in MB (minimum 5 MB)
* For example, `/MB:100` will create separate FASTA files that are each ~100 MB in size
* If both `/N` and `/MB` are specified, `/N` will be ignored

The parameter file path is optional. 
* If included, it should point to a valid XML parameter file.

Use `/S` to process all valid files in the input directory and subdirectories. Include a
number after `/S` (like `/S:2`) to limit the level of subdirectories to examine. 
* When using `/S,` you can redirect the output of the results using `/A`
* When using `/S`, you can use `/R` to re-create the input directory hierarchy in the alternate output directory (if defined)

Use `/L` to log messages to a file.

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/

## License

The FASTA File Splitter is licensed under the 2-Clause BSD License; 
you may not use this program except in compliance with the License.  You may obtain 
a copy of the License at https://opensource.org/licenses/BSD-2-Clause

Copyright 2021 Battelle Memorial Institute
