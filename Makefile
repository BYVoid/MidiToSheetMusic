TARGET = sheet.exe

all: $(TARGET)

$(TARGET): src/*.cs
	gmcs \
	 -res:img/NotePair.ico,MidiSheetMusic.NotePair.ico \
	 -res:img/treble.png,MidiSheetMusic.treble.png  \
	 -res:img/bass.png,MidiSheetMusic.bass.png  \
	 -res:img/two.png,MidiSheetMusic.two.png \
	 -res:img/three.png,MidiSheetMusic.three.png \
	 -res:img/four.png,MidiSheetMusic.four.png \
	 -res:img/six.png,MidiSheetMusic.six.png \
	 -res:img/eight.png,MidiSheetMusic.eight.png \
	 -res:img/nine.png,MidiSheetMusic.nine.png \
	 -res:img/twelve.png,MidiSheetMusic.twelve.png \
	 -target:exe \
	 -out:$(TARGET) \
	 -reference:System.Drawing \
	 src/AccidSymbol.cs \
	 src/BarSymbol.cs \
	 src/BlankSymbol.cs \
	 src/ChordSymbol.cs \
	 src/ClefMeasures.cs \
	 src/ClefSymbol.cs \
	 src/KeySignature.cs \
	 src/MidiNote.cs \
	 src/MidiEvent.cs \
	 src/MidiTrack.cs \
	 src/MidiFile.cs \
	 src/MidiFileException.cs \
	 src/MidiOptions.cs \
	 src/MidiFileReader.cs \
	 src/MusicSymbol.cs \
	 src/RestSymbol.cs \
	 src/SheetMusic.cs \
	 src/Staff.cs \
	 src/Stem.cs \
	 src/SymbolWidths.cs \
	 src/TimeSignature.cs \
	 src/WhiteNote.cs \
	 src/TimeSigSymbol.cs \
	 src/LyricSymbol.cs \
	 src/Main.cs

clean:
	rm $(TARGET)
