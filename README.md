#MidiToSheetMusic

MidiToSheetMusic is a very simple tool used for conversion from MIDI file to sheet music. It is forked and simplified from the original project [Midi Sheet Music](http://midisheetmusic.sourceforge.net/). MidiToSheetMusic is written in C# and Mono.

## Download

[Source code](https://github.com/BYVoid/MidiToSheetMusic/archive/master.zip)

[Binary version](http://www.byvoid.com/upload/projects/MidiToSheetMusic/sheet.exe) (for all platforms)

## Usage

Run ``sheet.exe`` (or ``mono sheet.exe``) in command line on your operating system (Mono is needed on Linux or Mac), then you will see:

    Usage: sheet input.mid output_prefix(_[page_number].png)

For example I would like to convert songs/sample.mid to sheet music, simply run:

    sheet.exe songs/sample.mid sample

Then you will find ``sample_1.png`` generated.

## Build

Have Mono SDK (or VS) installed, then run ``make``.

### Install mono compiler on Ubuntu

    sudo apt-get install mono-gmcs

## Screenshot

![Tchaikovsky](http://www.byvoid.com/upload/projects/MidiToSheetMusic/Tchaikovsky.png)

## License

GNU GENERAL PUBLIC LICENSE Version 2
