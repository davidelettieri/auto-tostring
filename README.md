# auto-tostring
VS extension to generate a simple tostring

The extension allow to generate a ToString method printing all the public properties of an object, it uses a formatted string implicitily calling ToString on property value. So if a classes does not have a proper ToString implementation it will print the name of the class e.g. arrays.

The extension register a code refactoring that is available on a class or a struct if it doesn't already contains a ToString method. [Here](https://www.loom.com/share/ad3fa5e155a145ec833c70169717efdc?sid=98d619ba-4637-424e-8790-bfcceece6d0b) a sample video that shows how it works
