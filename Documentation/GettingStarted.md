#### MDRDesk
[Up](../README.md)
## Getting Started 

###### For installation instructions see README document.

##### Limitations  
The indexing of a crash dump is limited to 268,435,392 objects, instances on the heap. This is because of
.NET object size limit of 2 GB. _I might deal with this later_.
The MDRDesktop is using a lot of memory, the largest crash dump, I tested, had size of 15,090,522,609 bytes and contained 165,448,895 objects.

