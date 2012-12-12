/*
 * Copyright (c) 2007-2012 Madhav Vaidyanathan
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License version 2.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Printing;

namespace MidiSheetMusic {


/** @class SheetMusic
 *
 * The SheetMusic Control is the main class for displaying the sheet music.
 * The SheetMusic class has the following public methods:
 *
 * SheetMusic()
 *   Create a new SheetMusic control from the given midi file and options.
 * 
 * SetZoom()
 *   Set the zoom level to display the sheet music at.
 *
 * DoPrint()
 *   Print a single page of sheet music.
 *
 * GetTotalPages()
 *   Get the total number of sheet music pages.
 *
 * OnPaint()
 *   Method called to draw the SheetMuisc
 *
 * These public methods are called from the MidiSheetMusic Form Window.
 *
 */
public class SheetMusic {

    /* Measurements used when drawing.  All measurements are in pixels.
     * The values depend on whether the menu 'Large Notes' or 'Small Notes' is selected.
     */
    public const  int LineWidth = 1;    /** The width of a line */
    public const  int LeftMargin = 4;   /** The left margin */
    public const  int TitleHeight = 14; /** The height for the title on the first page */
    public static int LineSpace;        /** The space between lines in the staff */
    public static int StaffHeight;      /** The height between the 5 horizontal lines of the staff */
    public static int NoteHeight;      /** The height of a whole note */
    public static int NoteWidth;       /** The width of a whole note */

    public const int PageWidth = 800;    /** The width of each page */
    public const int PageHeight = 1050;  /** The height of each page (when printing) */
    public static Font LetterFont;       /** The font for drawing the letters */

    private List<Staff> staffs; /** The array of staffs to display (from top to bottom) */
    private KeySignature mainkey; /** The main key signature */
    private int    numtracks;     /** The number of tracks */
    private float  zoom;          /** The zoom level to draw at (1.0 == 100%) */
    private bool   scrollVert;    /** Whether to scroll vertically or horizontally */
    private string filename;      /** The name of the midi file */
    private int showNoteLetters;    /** Display the note letters */
    private Color[] NoteColors;     /** The note colors to use */
    private SolidBrush shadeBrush;  /** The brush for shading */
    private SolidBrush shade2Brush; /** The brush for shading left-hand piano */
    private Pen pen;                /** The black pen for drawing */


    /** Initialize the default note sizes.  */
    static SheetMusic() {
        SetNoteSize(false);
    }

    /** Create a new SheetMusic control, using the given parsed MidiFile.
     *  The options can be null.
     */
    public SheetMusic(MidiFile file, MidiOptions options) {
        init(file, options); 
    }

    /** Create a new SheetMusic control, using the given midi filename.
     *  The options can be null.
     */
    public SheetMusic(string filename, MidiOptions options) {
        MidiFile file = new MidiFile(filename);
        init(file, options); 
    }

    /** Create a new SheetMusic control, using the given raw midi byte[] data.
     *  The options can be null.
     */
    public SheetMusic(byte[] data, string title, MidiOptions options) {
        MidiFile file = new MidiFile(data, title);
        init(file, options); 
    }


    /** Create a new SheetMusic control.
     * MidiFile is the parsed midi file to display.
     * SheetMusic Options are the menu options that were selected.
     *
     * - Apply all the Menu Options to the MidiFile tracks.
     * - Calculate the key signature
     * - For each track, create a list of MusicSymbols (notes, rests, bars, etc)
     * - Vertically align the music symbols in all the tracks
     * - Partition the music notes into horizontal staffs
     */
    public void init(MidiFile file, MidiOptions options) {
        if (options == null) {
            options = new MidiOptions(file);
        }
        zoom = 1.0f;
        filename = file.FileName;

        SetColors(options.colors, options.shadeColor, options.shade2Color);
        pen = new Pen(Color.Black, 1);

        List<MidiTrack> tracks = file.ChangeMidiNotes(options);
        SetNoteSize(options.largeNoteSize);
        scrollVert = options.scrollVert;
        showNoteLetters= options.showNoteLetters;
        TimeSignature time = file.Time; 
        if (options.time != null) {
            time = options.time;
        }
        if (options.key == -1) {
            mainkey = GetKeySignature(tracks);
        }
        else {
            mainkey = new KeySignature(options.key);
        }

        numtracks = tracks.Count;

        int lastStart = file.EndTime() + options.shifttime;

        /* Create all the music symbols (notes, rests, vertical bars, and
         * clef changes).  The symbols variable contains a list of music 
         * symbols for each track.  The list does not include the left-side 
         * Clef and key signature symbols.  Those can only be calculated 
         * when we create the staffs.
         */
        List<MusicSymbol>[] symbols = new List<MusicSymbol> [ numtracks ];
        for (int tracknum = 0; tracknum < numtracks; tracknum++) {
            MidiTrack track = tracks[tracknum];
            ClefMeasures clefs = new ClefMeasures(track.Notes, time.Measure);
            List<ChordSymbol> chords = CreateChords(track.Notes, mainkey, time, clefs);
            symbols[tracknum] = CreateSymbols(chords, clefs, time, lastStart);
        }

        List<LyricSymbol>[] lyrics = null;
        if (options.showLyrics) {
            lyrics = GetLyrics(tracks);
        }

        /* Vertically align the music symbols */
        SymbolWidths widths = new SymbolWidths(symbols, lyrics);
        // SymbolWidths widths = new SymbolWidths(symbols);
        AlignSymbols(symbols, widths);

        staffs = CreateStaffs(symbols, mainkey, options, time.Measure);
        CreateAllBeamedChords(symbols, time);
        if (lyrics != null) {
            AddLyricsToStaffs(staffs, lyrics);
        }

        /* After making chord pairs, the stem directions can change,
         * which affects the staff height.  Re-calculate the staff height.
         */
        foreach (Staff staff in staffs) {
            staff.CalculateHeight();
        }

        //BackColor = Color.White;

        SetZoom(1.0f);
    }


    /** Get the best key signature given the midi notes in all the tracks. */
    private KeySignature GetKeySignature(List<MidiTrack> tracks) {
        List<int> notenums = new List<int>();
        foreach (MidiTrack track in tracks) {
            foreach (MidiNote note in track.Notes) {
                notenums.Add(note.Number);
            }
        }
        return KeySignature.Guess(notenums);
    }


    /** Create the chord symbols for a single track.
     * @param midinotes  The Midinotes in the track.
     * @param key        The Key Signature, for determining sharps/flats.
     * @param time       The Time Signature, for determining the measures.
     * @param clefs      The clefs to use for each measure.
     * @ret An array of ChordSymbols
     */
    private
    List<ChordSymbol> CreateChords(List<MidiNote> midinotes, 
                                   KeySignature key,
                                   TimeSignature time,
                                   ClefMeasures clefs) {

        int i = 0;
        List<ChordSymbol> chords = new List<ChordSymbol>();
        List<MidiNote> notegroup = new List<MidiNote>(12);
        int len = midinotes.Count; 

        while (i < len) {

            int starttime = midinotes[i].StartTime;
            Clef clef = clefs.GetClef(starttime);

            /* Group all the midi notes with the same start time
             * into the notes list.
             */
            notegroup.Clear();
            notegroup.Add(midinotes[i]);
            i++;
            while (i < len && midinotes[i].StartTime == starttime) {
                notegroup.Add(midinotes[i]);
                i++;
            }

            /* Create a single chord from the group of midi notes with
             * the same start time.
             */
            ChordSymbol chord = new ChordSymbol(notegroup, key, time, clef, this);
            chords.Add(chord);
        }

        return chords;
    }

    /** Given the chord symbols for a track, create a new symbol list
     * that contains the chord symbols, vertical bars, rests, and clef changes.
     * Return a list of symbols (ChordSymbol, BarSymbol, RestSymbol, ClefSymbol)
     */
    private List<MusicSymbol> 
    CreateSymbols(List<ChordSymbol> chords, ClefMeasures clefs,
                  TimeSignature time, int lastStart) {

        List<MusicSymbol> symbols = new List<MusicSymbol>();
        symbols = AddBars(chords, time, lastStart);
        symbols = AddRests(symbols, time);
        symbols = AddClefChanges(symbols, clefs, time);

        return symbols;
    }

    /** Add in the vertical bars delimiting measures. 
     *  Also, add the time signature symbols.
     */
    private
    List<MusicSymbol> AddBars(List<ChordSymbol> chords, TimeSignature time,
                              int lastStart) {

        List<MusicSymbol> symbols = new List<MusicSymbol>();

        TimeSigSymbol timesig = new TimeSigSymbol(time.Numerator, time.Denominator);
        symbols.Add(timesig);

        /* The starttime of the beginning of the measure */
        int measuretime = 0;

        int i = 0;
        while (i < chords.Count) {
            if (measuretime <= chords[i].StartTime) {
                symbols.Add(new BarSymbol(measuretime) );
                measuretime += time.Measure;
            }
            else {
                symbols.Add(chords[i]);
                i++;
            }
        }

        /* Keep adding bars until the last StartTime (the end of the song) */
        while (measuretime < lastStart) {
            symbols.Add(new BarSymbol(measuretime) );
            measuretime += time.Measure;
        }

        /* Add the final vertical bar to the last measure */
        symbols.Add(new BarSymbol(measuretime) );
        return symbols;
    }

    /** Add rest symbols between notes.  All times below are 
     * measured in pulses.
     */
    private
    List<MusicSymbol> AddRests(List<MusicSymbol> symbols, TimeSignature time) {
        int prevtime = 0;

        List<MusicSymbol> result = new List<MusicSymbol>( symbols.Count );

        foreach (MusicSymbol symbol in symbols) {
            int starttime = symbol.StartTime;
            RestSymbol[] rests = GetRests(time, prevtime, starttime);
            if (rests != null) {
                foreach (RestSymbol r in rests) {
                    result.Add(r);
                }
            }

            result.Add(symbol);

            /* Set prevtime to the end time of the last note/symbol. */
            if (symbol is ChordSymbol) {
                ChordSymbol chord = (ChordSymbol)symbol;
                prevtime = Math.Max( chord.EndTime, prevtime );
            }
            else {
                prevtime = Math.Max(starttime, prevtime);
            }
        }
        return result;
    }

    /** Return the rest symbols needed to fill the time interval between
     * start and end.  If no rests are needed, return nil.
     */
    private
    RestSymbol[] GetRests(TimeSignature time, int start, int end) {
        RestSymbol[] result;
        RestSymbol r1, r2;

        if (end - start < 0)
            return null;

        NoteDuration dur = time.GetNoteDuration(end - start);
        switch (dur) {
            case NoteDuration.Whole:
            case NoteDuration.Half:
            case NoteDuration.Quarter:
            case NoteDuration.Eighth:
                r1 = new RestSymbol(start, dur);
                result = new RestSymbol[]{ r1 };
                return result;

            case NoteDuration.DottedHalf:
                r1 = new RestSymbol(start, NoteDuration.Half);
                r2 = new RestSymbol(start + time.Quarter*2, 
                                    NoteDuration.Quarter);
                result = new RestSymbol[]{ r1, r2 };
                return result;

            case NoteDuration.DottedQuarter:
                r1 = new RestSymbol(start, NoteDuration.Quarter);
                r2 = new RestSymbol(start + time.Quarter, 
                                    NoteDuration.Eighth);
                result = new RestSymbol[]{ r1, r2 };
                return result; 

            case NoteDuration.DottedEighth:
                r1 = new RestSymbol(start, NoteDuration.Eighth);
                r2 = new RestSymbol(start + time.Quarter/2, 
                                    NoteDuration.Sixteenth);
                result = new RestSymbol[]{ r1, r2 };
                return result;

            default:
                return null;
        }
    }

    /** The current clef is always shown at the beginning of the staff, on
     * the left side.  However, the clef can also change from measure to 
     * measure. When it does, a Clef symbol must be shown to indicate the 
     * change in clef.  This function adds these Clef change symbols.
     * This function does not add the main Clef Symbol that begins each
     * staff.  That is done in the Staff() contructor.
     */
    private
    List<MusicSymbol> AddClefChanges(List<MusicSymbol> symbols,
                                     ClefMeasures clefs,
                                     TimeSignature time) {

        List<MusicSymbol> result = new List<MusicSymbol>( symbols.Count );
        Clef prevclef = clefs.GetClef(0);
        foreach (MusicSymbol symbol in symbols) {
            /* A BarSymbol indicates a new measure */
            if (symbol is BarSymbol) {
                Clef clef = clefs.GetClef(symbol.StartTime);
                if (clef != prevclef) {
                    result.Add(new ClefSymbol(clef, symbol.StartTime-1, true));
                }
                prevclef = clef;
            }
            result.Add(symbol);
        }
        return result;
    }
           

    /** Notes with the same start times in different staffs should be
     * vertically aligned.  The SymbolWidths class is used to help 
     * vertically align symbols.
     *
     * First, each track should have a symbol for every starttime that
     * appears in the Midi File.  If a track doesn't have a symbol for a
     * particular starttime, then add a "blank" symbol for that time.
     *
     * Next, make sure the symbols for each start time all have the same
     * width, across all tracks.  The SymbolWidths class stores
     * - The symbol width for each starttime, for each track
     * - The maximum symbol width for a given starttime, across all tracks.
     *
     * The method SymbolWidths.GetExtraWidth() returns the extra width
     * needed for a track to match the maximum symbol width for a given
     * starttime.
     */
    private
    void AlignSymbols(List<MusicSymbol>[] allsymbols, SymbolWidths widths) {

        for (int track = 0; track < allsymbols.Length; track++) {
            List<MusicSymbol> symbols = allsymbols[track];
            List<MusicSymbol> result = new List<MusicSymbol>();

            int i = 0;

            /* If a track doesn't have a symbol for a starttime,
             * add a blank symbol.
             */
            foreach (int start in widths.StartTimes) {

                /* BarSymbols are not included in the SymbolWidths calculations */
                while (i < symbols.Count && (symbols[i] is BarSymbol) &&
                    symbols[i].StartTime <= start) {
                    result.Add(symbols[i]);
                    i++;
                }

                if (i < symbols.Count && symbols[i].StartTime == start) {

                    while (i < symbols.Count && 
                           symbols[i].StartTime == start) {

                        result.Add(symbols[i]);
                        i++;
                    }
                }
                else {
                    result.Add(new BlankSymbol(start, 0));
                }
            }

            /* For each starttime, increase the symbol width by
             * SymbolWidths.GetExtraWidth().
             */
            i = 0;
            while (i < result.Count) {
                if (result[i] is BarSymbol) {
                    i++;
                    continue;
                }
                int start = result[i].StartTime;
                int extra = widths.GetExtraWidth(track, start);
                result[i].Width += extra;

                /* Skip all remaining symbols with the same starttime. */
                while (i < result.Count && result[i].StartTime == start) {
                    i++;
                }
            } 
            allsymbols[track] = result;
        }
    }

    private static bool IsChord(MusicSymbol symbol) {
        return symbol is ChordSymbol;
    }


    /** Find 2, 3, 4, or 6 chord symbols that occur consecutively (without any
     *  rests or bars in between).  There can be BlankSymbols in between.
     *
     *  The startIndex is the index in the symbols to start looking from.
     *
     *  Store the indexes of the consecutive chords in chordIndexes.
     *  Store the horizontal distance (pixels) between the first and last chord.
     *  If we failed to find consecutive chords, return false.
     */
    private static bool
    FindConsecutiveChords(List<MusicSymbol> symbols, TimeSignature time,
                          int startIndex, int[] chordIndexes, 
                          ref int horizDistance) {

        int i = startIndex;
        int numChords = chordIndexes.Length;

        while (true) {
            horizDistance = 0;

            /* Find the starting chord */
            while (i < symbols.Count - numChords) {
                if (symbols[i] is ChordSymbol) {
                    ChordSymbol c = (ChordSymbol) symbols[i];
                    if (c.Stem != null) {
                        break;
                    }
                }
                i++;
            }
            if (i >= symbols.Count - numChords) {
                chordIndexes[0] = -1;
                return false;
            }
            chordIndexes[0] = i;
            bool foundChords = true;
            for (int chordIndex = 1; chordIndex < numChords; chordIndex++) {
                i++;
                int remaining = numChords - 1 - chordIndex;
                while ((i < symbols.Count - remaining) && (symbols[i] is BlankSymbol)) {
                    horizDistance += symbols[i].Width;
                    i++;
                }
                if (i >= symbols.Count - remaining) {
                    return false;
                }
                if (!(symbols[i] is ChordSymbol)) {
                    foundChords = false;
                    break;
                }
                chordIndexes[chordIndex] = i;
                horizDistance += symbols[i].Width;
            }
            if (foundChords) {
                return true;
            }

            /* Else, start searching again from index i */
        }
    }


    /** Connect chords of the same duration with a horizontal beam.
     *  numChords is the number of chords per beam (2, 3, 4, or 6).
     *  if startBeat is true, the first chord must start on a quarter note beat.
     */
    private static void
    CreateBeamedChords(List<MusicSymbol>[] allsymbols, TimeSignature time,
                       int numChords, bool startBeat) {
        int[] chordIndexes = new int[numChords];
        ChordSymbol[] chords = new ChordSymbol[numChords];

        foreach (List<MusicSymbol> symbols in allsymbols) {
            int startIndex = 0;
            while (true) {
                int horizDistance = 0;
                bool found = FindConsecutiveChords(symbols, time,
                                                   startIndex,
                                                   chordIndexes,
                                                   ref horizDistance);
                if (!found) {
                    break;
                }
                for (int i = 0; i < numChords; i++) {
                    chords[i] = (ChordSymbol)symbols[ chordIndexes[i] ];
                }

                if (ChordSymbol.CanCreateBeam(chords, time, startBeat)) {
                    ChordSymbol.CreateBeam(chords, horizDistance);
                    startIndex = chordIndexes[numChords-1] + 1;
                }
                else {
                    startIndex = chordIndexes[0] + 1;
                }

                /* What is the value of startIndex here?
                 * If we created a beam, we start after the last chord.
                 * If we failed to create a beam, we start after the first chord.
                 */
            }
        }
    }


    /** Connect chords of the same duration with a horizontal beam.
     *
     *  We create beams in the following order:
     *  - 6 connected 8th note chords, in 3/4, 6/8, or 6/4 time
     *  - Triplets that start on quarter note beats
     *  - 3 connected chords that start on quarter note beats (12/8 time only)
     *  - 4 connected chords that start on quarter note beats (4/4 or 2/4 time only)
     *  - 2 connected chords that start on quarter note beats
     *  - 2 connected chords that start on any beat
     */ 
    private static void
    CreateAllBeamedChords(List<MusicSymbol>[] allsymbols, TimeSignature time) {
        if ((time.Numerator == 3 && time.Denominator == 4) ||
            (time.Numerator == 6 && time.Denominator == 8) ||
            (time.Numerator == 6 && time.Denominator == 4) ) {

            CreateBeamedChords(allsymbols, time, 6, true);
        }
        CreateBeamedChords(allsymbols, time, 3, true);
        CreateBeamedChords(allsymbols, time, 4, true);
        CreateBeamedChords(allsymbols, time, 2, true);
        CreateBeamedChords(allsymbols, time, 2, false);
    }

    /** Get the width (in pixels) needed to display the key signature */
    public static int
    KeySignatureWidth(KeySignature key) {
        ClefSymbol clefsym = new ClefSymbol(Clef.Treble, 0, false);
        int result = clefsym.MinWidth;
        AccidSymbol[] keys = key.GetSymbols(Clef.Treble);
        foreach (AccidSymbol symbol in keys) {
            result += symbol.MinWidth;
        }
        return result + SheetMusic.LeftMargin + 5;
    }


    /** Given MusicSymbols for a track, create the staffs for that track.
     *  Each Staff has a maxmimum width of PageWidth (800 pixels).
     *  Also, measures should not span multiple Staffs.
     */
    private List<Staff> 
    CreateStaffsForTrack(List<MusicSymbol> symbols, int measurelen, 
                         KeySignature key, MidiOptions options,
                         int track, int totaltracks) {
        int keysigWidth = KeySignatureWidth(key);
        int startindex = 0;
        List<Staff> thestaffs = new List<Staff>(symbols.Count / 50);

        while (startindex < symbols.Count) {
            /* startindex is the index of the first symbol in the staff.
             * endindex is the index of the last symbol in the staff.
             */
            int endindex = startindex;
            int width = keysigWidth;
            int maxwidth;

            /* If we're scrolling vertically, the maximum width is PageWidth. */
            if (scrollVert) {
                maxwidth = SheetMusic.PageWidth;
            }
            else {
                maxwidth = 2000000;
            }

            while (endindex < symbols.Count &&
                   width + symbols[endindex].Width < maxwidth) {

                width += symbols[endindex].Width;
                endindex++;
            }
            endindex--;

            /* There's 3 possibilities at this point:
             * 1. We have all the symbols in the track.
             *    The endindex stays the same.
             *
             * 2. We have symbols for less than one measure.
             *    The endindex stays the same.
             *
             * 3. We have symbols for 1 or more measures.
             *    Since measures cannot span multiple staffs, we must
             *    make sure endindex does not occur in the middle of a
             *    measure.  We count backwards until we come to the end
             *    of a measure.
             */

            if (endindex == symbols.Count - 1) {
                /* endindex stays the same */
            }
            else if (symbols[startindex].StartTime / measurelen ==
                     symbols[endindex].StartTime / measurelen) {
                /* endindex stays the same */
            }
            else {
                int endmeasure = symbols[endindex+1].StartTime/measurelen;
                while (symbols[endindex].StartTime / measurelen == 
                       endmeasure) {
                    endindex--;
                }
            }
            int range = endindex + 1 - startindex;
            if (scrollVert) {
                width = SheetMusic.PageWidth;
            }
            Staff staff = new Staff(symbols.GetRange(startindex, range),
                                    key, options, track, totaltracks);
            thestaffs.Add(staff);
            startindex = endindex + 1;
        }
        return thestaffs;
    }


    /** Given all the MusicSymbols for every track, create the staffs
     * for the sheet music.  There are two parts to this:
     *
     * - Get the list of staffs for each track.
     *   The staffs will be stored in trackstaffs as:
     *
     *   trackstaffs[0] = { Staff0, Staff1, Staff2, ... } for track 0
     *   trackstaffs[1] = { Staff0, Staff1, Staff2, ... } for track 1
     *   trackstaffs[2] = { Staff0, Staff1, Staff2, ... } for track 2
     *
     * - Store the Staffs in the staffs list, but interleave the
     *   tracks as follows:
     *
     *   staffs = { Staff0 for track 0, Staff0 for track1, Staff0 for track2,
     *              Staff1 for track 0, Staff1 for track1, Staff1 for track2,
     *              Staff2 for track 0, Staff2 for track1, Staff2 for track2,
     *              ... } 
     */
    private List<Staff> 
    CreateStaffs(List<MusicSymbol>[] allsymbols, KeySignature key, 
                 MidiOptions options, int measurelen) {

        List<Staff>[] trackstaffs = new List<Staff>[ allsymbols.Length ];
        int totaltracks = trackstaffs.Length;

        for (int track = 0; track < totaltracks; track++) {
            List<MusicSymbol> symbols = allsymbols[ track ];
            trackstaffs[track] = CreateStaffsForTrack(symbols, measurelen, key, options, track, totaltracks);
        }

        /* Update the EndTime of each Staff. EndTime is used for playback */
        foreach (List<Staff> list in trackstaffs) {
            for (int i = 0; i < list.Count-1; i++) {
                list[i].EndTime = list[i+1].StartTime;
            }
        }

        /* Interleave the staffs of each track into the result array. */
        int maxstaffs = 0;
        for (int i = 0; i < trackstaffs.Length; i++) {
            if (maxstaffs < trackstaffs[i].Count) {
                maxstaffs = trackstaffs[i].Count;
            }
        }
        List<Staff> result = new List<Staff>(maxstaffs * trackstaffs.Length);
        for (int i = 0; i < maxstaffs; i++) {
            foreach (List<Staff> list in trackstaffs) {
                if (i < list.Count) {
                    result.Add(list[i]);
                }
            }
        }
        return result;
    }

    /** Get the lyrics for each track */
    private static List<LyricSymbol>[]
    GetLyrics(List<MidiTrack> tracks) {
        bool hasLyrics = false;
        List<LyricSymbol>[] result = new List<LyricSymbol>[tracks.Count];
        for (int tracknum = 0; tracknum < tracks.Count; tracknum++) {
            MidiTrack track = tracks[tracknum];
            if (track.Lyrics == null) {
                continue;
            }
            hasLyrics = true;
            result[tracknum] = new List<LyricSymbol>();
            foreach (MidiEvent ev in track.Lyrics) {
                String text = UTF8Encoding.UTF8.GetString(ev.Value, 0, ev.Value.Length);
                LyricSymbol sym = new LyricSymbol(ev.StartTime, text);
                result[tracknum].Add(sym);
            }
        }
        if (!hasLyrics) {
            return null;
        }
        else {
            return result;
        }
    }

    /** Add the lyric symbols to the corresponding staffs */
    static void
    AddLyricsToStaffs(List<Staff> staffs, List<LyricSymbol>[] tracklyrics) {
        foreach (Staff staff in staffs) {
            List<LyricSymbol> lyrics = tracklyrics[staff.Track];
            staff.AddLyrics(lyrics);
        }
    }


    /** Set the zoom level to display at (1.0 == 100%).
     * Recalculate the SheetMusic width and height based on the
     * zoom level.  Then redraw the SheetMusic. 
     */
    public void SetZoom(float value) {
        zoom = value;
        float width = 0;
        float height = 0;
        foreach (Staff staff in staffs) {
            width = Math.Max(width, staff.Width * zoom);
            height += (staff.Height * zoom);
        }
        // Width = (int)(width + 2);
        // Height = ((int)height) + LeftMargin;
        //this.Invalidate();
    }

    /** Change the note colors for the sheet music, and redraw. */
    private void SetColors(Color[] newcolors, Color newshade, Color newshade2) {
        if (NoteColors == null) {
            NoteColors = new Color[12];
            for (int i = 0; i < 12; i++) {
                NoteColors[i] = Color.Black;
            }
        }
        if (newcolors != null) {
            for (int i = 0; i < 12; i++) {
                NoteColors[i] = newcolors[i];
            }
        }
        else {
            for (int i = 0; i < 12; i++) {
                NoteColors[i] = Color.Black;
            }
        }
        if (shadeBrush != null) {
            shadeBrush.Dispose(); 
            shade2Brush.Dispose(); 
        }
        shadeBrush = new SolidBrush(newshade);
        shade2Brush = new SolidBrush(newshade2);
    }

    /** Get the color for a given note number */
    public Color NoteColor(int number) {
        return NoteColors[ NoteScale.FromNumber(number) ];
    }

    /** Get the shade brush */
    public Brush ShadeBrush {
        get { return shadeBrush; }
    }

    /** Get the shade2 brush */
    public Brush Shade2Brush {
        get { return shade2Brush; }
    }

    /** Get whether to show note letters or not */
    public int ShowNoteLetters {
        get { return showNoteLetters; }
    }

    /** Get the main key signature */
    public KeySignature MainKey {
        get { return mainkey; }
    }


    /** Set the size of the notes, large or small.  Smaller notes means
     * more notes per staff.
     */
    public static void SetNoteSize(bool largenotes) {
        if (largenotes)
            LineSpace = 7;
        else
            LineSpace = 5;

        StaffHeight = LineSpace*4 + LineWidth*5;
        NoteHeight = LineSpace + LineWidth;
        NoteWidth = 3 * LineSpace / 2;
        LetterFont = new Font("Arial", 8, FontStyle.Bold);
    }


    /** Draw the SheetMusic.
     * Scale the graphics by the current zoom factor.
     * Get the vertical start and end points of the clip area.
     * Only draw Staffs which lie inside the clip area.
     */
	/*
    protected override void OnPaint(PaintEventArgs e) {
        Rectangle clip = 
          new Rectangle((int) (e.ClipRectangle.X / zoom),
                        (int) (e.ClipRectangle.Y / zoom),
                        (int) (e.ClipRectangle.Width / zoom),
                        (int) (e.ClipRectangle.Height / zoom));

        Graphics g = e.Graphics;
        g.ScaleTransform(zoom, zoom);
        // g.PageScale = zoom; 
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int ypos = 0;
        foreach (Staff staff in staffs) {
            if ((ypos + staff.Height < clip.Y) || (ypos > clip.Y + clip.Height))  {
                // Staff is not in the clip, don't need to draw it 
            }
            else {
                g.TranslateTransform(0, ypos);
                staff.Draw(g, clip, pen);
                g.TranslateTransform(0, -ypos);
            }

            ypos += staff.Height;
        }
        g.ScaleTransform(1.0f/zoom, 1.0f/zoom);
    }*/

    /** Write the MIDI filename at the top of the page */
    private void DrawTitle(Graphics g) {
        int leftmargin = 20;
        int topmargin = 20;
        string title = Path.GetFileName(filename);
        title = title.Replace(".mid", "").Replace("_", " ");
        Font font = new Font("Arial", 10, FontStyle.Bold);
        g.TranslateTransform(leftmargin, topmargin);
        g.DrawString(title, font, Brushes.Black, 0, 0);
        g.TranslateTransform(-leftmargin, -topmargin);
        font.Dispose();
    }

    /** Print the given page of the sheet music. 
     * Page numbers start from 1.
     * A staff should fit within a single page, not be split across two pages.
     * If the sheet music has exactly 2 tracks, then two staffs should
     * fit within a single page, and not be split across two pages.
     */
    public void DoPrint(Graphics g, int pagenumber)
    {
        int leftmargin = 20;
        int topmargin = 20;
        int rightmargin = 20;
        int bottommargin = 20;

        float scale = (g.VisibleClipBounds.Width - leftmargin - rightmargin) / PageWidth;
        g.PageScale = scale;

        int viewPageHeight = (int)( (g.VisibleClipBounds.Height - topmargin - bottommargin) / scale);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.FillRectangle(Brushes.White, 0, 0, 
                        g.VisibleClipBounds.Width,
                        g.VisibleClipBounds.Height);

        Rectangle clip = new Rectangle(0, 0, PageWidth, PageHeight);

        int ypos = TitleHeight;
        int pagenum = 1;
        int staffnum = 0;

        if (numtracks == 2 && (staffs.Count % 2) == 0) {
            /* Skip the staffs until we reach the given page number */
            while (staffnum + 1 < staffs.Count && pagenum < pagenumber) {
                int heights = staffs[staffnum].Height + staffs[staffnum+1].Height;
                if (ypos + heights >= viewPageHeight) {
                    pagenum++;
                    ypos = 0;
                }
                else {
                    ypos += heights;
                    staffnum += 2;
                }
            }
            /* Print the staffs until the height reaches viewPageHeight */
            if (pagenum == 1) {
                DrawTitle(g);
                ypos = TitleHeight;
            }
            else {
                ypos = 0;
            }
            for (; staffnum + 1 < staffs.Count; staffnum += 2) {
                int heights = staffs[staffnum].Height + staffs[staffnum+1].Height;

                if (ypos + heights >= viewPageHeight)
                    break;

                g.TranslateTransform(leftmargin, topmargin + ypos);
                staffs[staffnum].Draw(g, clip, pen);
                g.TranslateTransform(-leftmargin, -(topmargin + ypos));
                ypos += staffs[staffnum].Height;
                g.TranslateTransform(leftmargin, topmargin + ypos);
                staffs[staffnum + 1].Draw(g, clip, pen);
                g.TranslateTransform(-leftmargin, -(topmargin + ypos));
                ypos += staffs[staffnum + 1].Height;
            }
        }

        else {
            /* Skip the staffs until we reach the given page number */
            while (staffnum < staffs.Count && pagenum < pagenumber) {
                if (ypos + staffs[staffnum].Height >= viewPageHeight) {
                    pagenum++;
                    ypos = 0;
                }
                else {
                    ypos += staffs[staffnum].Height;
                    staffnum++;
                }
            }

            /* Print the staffs until the height reaches viewPageHeight */
            if (pagenum == 1) {
                DrawTitle(g);
                ypos = TitleHeight;
            }
            else {
                ypos = 0;
            }
            for (; staffnum < staffs.Count; staffnum++) {
                if (ypos + staffs[staffnum].Height >= viewPageHeight)
                    break;

                g.TranslateTransform(leftmargin, topmargin + ypos);
                staffs[staffnum].Draw(g, clip, pen);
                g.TranslateTransform(-leftmargin, -(topmargin + ypos));
                ypos += staffs[staffnum].Height;
            }
        }

        /* Draw the page number */
        Font font = new Font("Arial", 10, FontStyle.Bold);
        g.DrawString("" + pagenumber, font, Brushes.Black, 
                     PageWidth-leftmargin, topmargin + viewPageHeight - 12);
        font.Dispose();
    }

    /**
     * Return the number of pages needed to print this sheet music.
     * A staff should fit within a single page, not be split across two pages.
     * If the sheet music has exactly 2 tracks, then two staffs should
     * fit within a single page, and not be split across two pages.
     */
    public int GetTotalPages() {
        int num = 1;
        int currheight = TitleHeight;

        if (numtracks == 2 && (staffs.Count % 2) == 0) {
            for (int i = 0; i < staffs.Count; i += 2) {
                int heights = staffs[i].Height + staffs[i+1].Height;
                if (currheight + heights > PageHeight) {
                    num++;
                    currheight = heights;
                }
                else {
                    currheight += heights;
                }
            }
        }
        else {
            foreach (Staff staff in staffs) {
                if (currheight + staff.Height > PageHeight) {
                    num++;
                    currheight = staff.Height;
                }
                else {
                    currheight += staff.Height;
                }
            }
        }
        return num;
    }

    /** Shade all the chords played at the given pulse time.
     *  Loop through all the staffs and call staff.Shade().
     *  If scrollGradually is true, scroll gradually (smooth scrolling)
     *  to the shaded notes.
     */
	/*
    public void ShadeNotes(int currentPulseTime, int prevPulseTime, bool scrollGradually) {
        Graphics g = CreateGraphics();
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.ScaleTransform(zoom, zoom);
        int ypos = 0;

        int x_shade = 0;
        int y_shade = 0;

        foreach (Staff staff in staffs) {
            g.TranslateTransform(0, ypos);
            staff.ShadeNotes(g, shadeBrush, pen, 
                             currentPulseTime, prevPulseTime, ref x_shade);
            g.TranslateTransform(0, -ypos);
            ypos += staff.Height;
            if (currentPulseTime >= staff.EndTime) {
                y_shade += staff.Height;
            }
        }
        g.ScaleTransform(1.0f/zoom, 1.0f/zoom);
        g.Dispose();
        x_shade = (int)(x_shade * zoom);
        y_shade -= NoteHeight;
        y_shade = (int)(y_shade * zoom);
        if (currentPulseTime >= 0) {
            ScrollToShadedNotes(x_shade, y_shade, scrollGradually);
        }
    }
    */

    /** Scroll the sheet music so that the shaded notes are visible.
      * If scrollGradually is true, scroll gradually (smooth scrolling)
      * to the shaded notes.
      */
	/*
    void ScrollToShadedNotes(int x_shade, int y_shade, bool scrollGradually) {
        Panel scrollview = (Panel) this.Parent;
        Point scrollPos = scrollview.AutoScrollPosition;

        // The scroll position is in negative coordinates for some reason
        scrollPos.X = -scrollPos.X;
        scrollPos.Y = -scrollPos.Y;
        Point newPos = scrollPos;

        if (scrollVert) {
            int scrollDist = (int)(y_shade - scrollPos.Y);

            if (scrollGradually) {
                if (scrollDist > (zoom * StaffHeight * 8))
                    scrollDist = scrollDist/2;
                else if (scrollDist > (NoteHeight * 3 * zoom))
                    scrollDist = (int)(NoteHeight * 3 * zoom);
            }
            newPos = new Point(scrollPos.X, scrollPos.Y + scrollDist);
        }
        else {
            int x_view = scrollPos.X + 40 * scrollview.Width/100;
            int xmax   = scrollPos.X + 65 * scrollview.Width/100;
            int scrollDist = x_shade - x_view;

            if (scrollGradually) {
                if (x_shade > xmax) 
                    scrollDist = (x_shade - x_view)/3;
                else if (x_shade > x_view)
                    scrollDist = (x_shade - x_view)/6;
            }

            newPos = new Point(scrollPos.X + scrollDist, scrollPos.Y);
            if (newPos.X < 0) {
                newPos.X = 0;
            }
        }
        scrollview.AutoScrollPosition = newPos;
    }
    */

    public override string ToString() {
        string result = "SheetMusic staffs=" + staffs.Count + "\n";
        foreach (Staff staff in staffs) {
            result += staff.ToString();
        }
        result += "End SheetMusic\n";
        return result;
    }

}

}
