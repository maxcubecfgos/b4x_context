# B4XContext (WPF/.NET 8 port)

This repository is a WPF/.NET 8 tool. It packages a B4X project's source and layout files into a compact Markdown context bundle suitable for pasting into an AI assistant.

Usage
- Launch from the B4X IDE as an External Tool with parameters: "%FILE%" "%LINE%". The app will scan the project, detect the active Sub at the provided line, and let you assemble a bundle.
- Alternatively run the executable from the command line: `B4XContext.exe "C:\path\to\module.bas" 123`

Features ported
- Accurate .bal layout decoding
- Skeleton generator for B4X modules
- Project scanning for .bas / .bal files
- Compile & check errors integration (uses local B4ABuilder/B4JBuilder when available)
- Generates a Markdown bundle and copies it to the Windows clipboard
