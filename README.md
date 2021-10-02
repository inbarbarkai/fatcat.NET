# fatcat.NET
A port of [Gregwar fatcat](https://github.com/Gregwar/fatcat) to .NET

## Structure
The solution contains 2 projects:
1. fatcat.Core - The core logic of the reading the FAT images.
2. fatcat - A commandline interface for reading FAT images.

## Commandline interface
fatcat <list|read> <options>

* list - Lists entries under the specified path.<br />
Options:
  * -i|--image - The path of the FAT image.
  * -p|--path - The path of the directory to list its contents.
  * -d|--deleted - Indicates whether to list deleted items or not (optional).
  * -o|--offset - The offset of the partition within the image (optional).
* read - Copies the content of a file from the image to the specified location.<br />
*The argument **-p** and the arguments **-s & -c** are mutually exclsive.<br />*
*Those arguments should not be used together.<br />*
Options:
  * -i|--image - The path of the FAT image.
  * -p|--path - The path of the file to copy.
  * -c|--cluster - The number of the cluster of the file to copy.
  * -s|--size - The size of the file.
  * -d|--deleted - Indicates whether the file was deleted or not.
  * -o|--offset - The offset of the partition within the image (optional). 
