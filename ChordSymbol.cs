/*
 * Copyright (c) 2007-2011 Madhav Vaidyanathan
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
using System.Drawing.Drawing2D;

namespace MidiSheetMusic {

public enum StemDir { Up, Down };

/** @class NoteData
 *  Contains fields for displaying a single note in a chord.
 */
public class NoteData {
    public int number;             /** The Midi note number, used to determine the color */
    public WhiteNote whitenote;    /** The white note location to draw */
    public NoteDuration duration;  /** The duration of the note */
    public bool leftside;          /** Whether to draw note to the left or right of the stem */
    public Accid accid;            /** Used to create the AccidSymbols for the chord */
};

/** @class ChordSymbol
 * A chord symbol represents a group of notes that are played at the same
 * time.  A chord includes the notes, the accidental symbols for each
 * note, and the stem (or stems) to use.  A single chord may have two 
 * stems if the notes have different durations (e.g. if one note is a
 * quarter note, and another is an eighth note).
 */
public class ChordSymbol : MusicSymbol {
    private Clef clef;             /** Which clef the chord is being drawn in */
    private int starttime;         /** The time (in pulses) the notes occurs at */
    private int endtime;           /** The starttime plus the longest note duration */
    private NoteData[] notedata;   /** The notes to draw */
    private AccidSymbol[] accidsymbols;   /** The accidental symbols to draw */
    private int width;             /** The width of the chord */
    private Stem stem1;            /** The stem of the chord. Can be null. */
    private Stem stem2;            /** The second stem of the chord. Can be null */
    private bool hastwostems;      /** True if this chord has two stems */
    private SheetMusic sheetmusic; /** Used to get colors and other options */


    /** Create a new Chord Symbol from the given list of midi notes.
     * All the midi notes will have the same start time.  Use the
     * key signature to get the white key and accidental symbol for
     * each note.  Use the time signature to calculate the duration
     * of the notes. Use the clef when drawing the chord.
     */
    public ChordSymbol(List<MidiNote> midinotes, KeySignature key, 
                       TimeSignature time, Clef c, SheetMusic sheet) {

        int len = midinotes.Count;
        int i;

        hastwostems = false;
        clef = c;
        sheetmusic = sheet;

        starttime = midinotes[0].StartTime;
        endtime = midinotes[0].EndTime;

        for (i = 0; i < midinotes.Count; i++) {
            if (i > 1) {
                if (midinotes[i].Number < midinotes[i-1].Number) {
                    throw new System.ArgumentException("Chord notes not in increasing order by number");
                }
            }
            endtime = Math.Max(endtime, midinotes[i].EndTime);
        }

        notedata = CreateNoteData(midinotes, key, time);
        accidsymbols = CreateAccidSymbols(notedata, clef);


        /* Find out how many stems we need (1 or 2) */
        NoteDuration dur1 = notedata[0].duration;
        NoteDuration dur2 = dur1;
        int change = -1;
        for (i = 0; i < notedata.Length; i++) {
            dur2 = notedata[i].duration;
            if (dur1 != dur2) {
                change = i;
                break;
            }
        }

        if (dur1 != dur2) {
            /* We have notes with different durations.  So we will need
             * two stems.  The first stem points down, and contains the
             * bottom note up to the note with the different duration.
             *
             * The second stem points up, and contains the note with the
             * different duration up to the top note.
             */
            hastwostems = true;
            stem1 = new Stem(notedata[0].whitenote, 
                             notedata[change-1].whitenote,
                             dur1, 
                             Stem.Down,
                             NotesOverlap(notedata, 0, change)
                            );

            stem2 = new Stem(notedata[change].whitenote, 
                             notedata[notedata.Length-1].whitenote,
                             dur2, 
                             Stem.Up,
                             NotesOverlap(notedata, change, notedata.Length)
                            );
        }
        else {
            /* All notes have the same duration, so we only need one stem. */
            int direction = StemDirection(notedata[0].whitenote, 
                                          notedata[notedata.Length-1].whitenote,
                                          clef);

            stem1 = new Stem(notedata[0].whitenote,
                             notedata[notedata.Length-1].whitenote,
                             dur1, 
                             direction,
                             NotesOverlap(notedata, 0, notedata.Length)
                            );
            stem2 = null;
        }

        /* For whole notes, no stem is drawn. */
        if (dur1 == NoteDuration.Whole)
            stem1 = null;
        if (dur2 == NoteDuration.Whole)
            stem2 = null;

        width = MinWidth;
    }


    /** Given the raw midi notes (the note number and duration in pulses),
     * calculate the following note data:
     * - The white key
     * - The accidental (if any)
     * - The note duration (half, quarter, eighth, etc)
     * - The side it should be drawn (left or side)
     * By default, notes are drawn on the left side.  However, if two notes
     * overlap (like A and B) you cannot draw the next note directly above it.
     * Instead you must shift one of the notes to the right.
     *
     * The KeySignature is used to determine the white key and accidental.
     * The TimeSignature is used to determine the duration.
     */
 
    private static NoteData[] 
    CreateNoteData(List<MidiNote> midinotes, KeySignature key,
                              TimeSignature time) {

        int len = midinotes.Count;
        NoteData[] notedata = new NoteData[len];

        for (int i = 0; i < len; i++) {
            MidiNote midi = midinotes[i];
            notedata[i] = new NoteData();
            notedata[i].number = midi.Number;
            notedata[i].leftside = true;
            notedata[i].whitenote = key.GetWhiteNote(midi.Number);
            notedata[i].duration = time.GetNoteDuration(midi.EndTime - midi.StartTime);
            notedata[i].accid = key.GetAccidental(midi.Number, midi.StartTime / time.Measure);
            
            if (i > 0 && (notedata[i].whitenote.Dist(notedata[i-1].whitenote) == 1)) {
                /* This note (notedata[i]) overlaps with the previous note.
                 * Change the side of this note.
                 */

                if (notedata[i-1].leftside) {
                    notedata[i].leftside = false;
                } else {
                    notedata[i].leftside = true;
                }
            } else {
                notedata[i].leftside = true;
            }
        }
        return notedata;
    }


    /** Given the note data (the white keys and accidentals), create 
     * the Accidental Symbols and return them.
     */
    private static AccidSymbol[] 
    CreateAccidSymbols(NoteData[] notedata, Clef clef) {
        int count = 0;
        foreach (NoteData n in notedata) {
            if (n.accid != Accid.None) {
                count++;
            }
        }
        AccidSymbol[] symbols = new AccidSymbol[count];
        int i = 0;
        foreach (NoteData n in notedata) {
            if (n.accid != Accid.None) {
                symbols[i] = new AccidSymbol(n.accid, n.whitenote, clef);
                i++;
            }
        }
        return symbols;
    }

    /** Calculate the stem direction (Up or down) based on the top and
     * bottom note in the chord.  If the average of the notes is above
     * the middle of the staff, the direction is down.  Else, the
     * direction is up.
     */
    private static int 
    StemDirection(WhiteNote bottom, WhiteNote top, Clef clef) {
        WhiteNote middle;
        if (clef == Clef.Treble)
            middle = new WhiteNote(WhiteNote.B, 5);
        else
            middle = new WhiteNote(WhiteNote.D, 3);

        int dist = middle.Dist(bottom) + middle.Dist(top);
        if (dist >= 0)
            return Stem.Up;
        else 
            return Stem.Down;
    }

    /** Return whether any of the notes in notedata (between start and
     * end indexes) overlap.  This is needed by the Stem class to
     * determine the position of the stem (left or right of notes).
     */
    private static bool NotesOverlap(NoteData[] notedata, int start, int end) {
        for (int i = start; i < end; i++) {
            if (!notedata[i].leftside) {
                return true;
            }
        }
        return false;
    }

    /** Get the time (in pulses) this symbol occurs at.
     * This is used to determine the measure this symbol belongs to.
     */
    public override int StartTime { 
        get { return starttime; }
    }

    /** Get the end time (in pulses) of the longest note in the chord.
     * Used to determine whether two adjacent chords can be joined
     * by a stem.
     */
    public int EndTime { 
        get { return endtime; }
    }

    /** Return the clef this chord is drawn in. */
    public Clef Clef { 
        get { return clef; }
    }

    /** Return true if this chord has two stems */
    public bool HasTwoStems {
        get { return hastwostems; }
    }

    /* Return the stem will the smallest duration.  This property
     * is used when making chord pairs (chords joined by a horizontal
     * beam stem). The stem durations must match in order to make
     * a chord pair.  If a chord has two stems, we always return
     * the one with a smaller duration, because it has a better 
     * chance of making a pair.
     */
    public Stem Stem {
        get { 
            if (stem1 == null) { return stem2; }
            else if (stem2 == null) { return stem1; }
            else if (stem1.Duration < stem2.Duration) { return stem1; }
            else { return stem2; }
        }
    }

    /** Get/Set the width (in pixels) of this symbol. The width is set
     * in SheetMusic.AlignSymbols() to vertically align symbols.
     */
    public override int Width {
        get { return width; }
        set { width = value; }
    }

    /** Get the minimum width (in pixels) needed to draw this symbol */
    public override int MinWidth {
        get { return GetMinWidth(); }
    }

    /* Return the minimum width needed to display this chord.
     *
     * The accidental symbols can be drawn above one another as long
     * as they don't overlap (they must be at least 6 notes apart).
     * If two accidental symbols do overlap, the accidental symbol
     * on top must be shifted to the right.  So the width needed for
     * accidental symbols depends on whether they overlap or not.
     *
     * If we are also displaying the letters, include extra width.
     */
    int GetMinWidth() {
        /* The width needed for the note circles */
        int result = 2*SheetMusic.NoteHeight + SheetMusic.NoteHeight*3/4;

        if (accidsymbols.Length > 0) {
            result += accidsymbols[0].MinWidth;
            for (int i = 1; i < accidsymbols.Length; i++) {
                AccidSymbol accid = accidsymbols[i];
                AccidSymbol prev = accidsymbols[i-1];
                if (accid.Note.Dist(prev.Note) < 6) {
                    result += accid.MinWidth;
                }
            }
        }
        if (sheetmusic != null && sheetmusic.ShowNoteLetters != MidiOptions.NoteNameNone) {
            result += 8;
        }
        return result;
    }


    /** Get the number of pixels this symbol extends above the staff. Used
     *  to determine the minimum height needed for the staff (Staff.FindBounds).
     */
    public override int AboveStaff {
        get { return GetAboveStaff(); }
    }

    private int GetAboveStaff() {
        /* Find the topmost note in the chord */
        WhiteNote topnote = notedata[ notedata.Length-1 ].whitenote;

        /* The stem.End is the note position where the stem ends.
         * Check if the stem end is higher than the top note.
         */
        if (stem1 != null)
            topnote = WhiteNote.Max(topnote, stem1.End);
        if (stem2 != null)
            topnote = WhiteNote.Max(topnote, stem2.End);

        int dist = topnote.Dist(WhiteNote.Top(clef)) * SheetMusic.NoteHeight/2;
        int result = 0;
        if (dist > 0)
            result = dist;

        /* Check if any accidental symbols extend above the staff */
        foreach (AccidSymbol symbol in accidsymbols) {
            if (symbol.AboveStaff > result) {
                result = symbol.AboveStaff;
            }
        }
        return result;
    }

    /** Get the number of pixels this symbol extends below the staff. Used
     *  to determine the minimum height needed for the staff (Staff.FindBounds).
     */
    public override int BelowStaff {
        get { return GetBelowStaff(); }
    }

    private int GetBelowStaff() {
        /* Find the bottom note in the chord */
        WhiteNote bottomnote = notedata[0].whitenote;

        /* The stem.End is the note position where the stem ends.
         * Check if the stem end is lower than the bottom note.
         */
        if (stem1 != null)
            bottomnote = WhiteNote.Min(bottomnote, stem1.End);
        if (stem2 != null)
            bottomnote = WhiteNote.Min(bottomnote, stem2.End);

        int dist = WhiteNote.Bottom(clef).Dist(bottomnote) *
                   SheetMusic.NoteHeight/2;

        int result = 0;
        if (dist > 0)
            result = dist;

        /* Check if any accidental symbols extend below the staff */ 
        foreach (AccidSymbol symbol in accidsymbols) {
            if (symbol.BelowStaff > result) {
                result = symbol.BelowStaff;
            }
        }
        return result;
    }

    /** Get the name for this note */
    private string NoteName(int notenumber, WhiteNote whitenote) {
        if (sheetmusic.ShowNoteLetters == MidiOptions.NoteNameLetter) {
            return Letter(notenumber, whitenote);
        }
        else if (sheetmusic.ShowNoteLetters == MidiOptions.NoteNameFixedDoReMi) {
            string[] fixedDoReMi = {
                "La", "Li", "Ti", "Do", "Di", "Re", "Ri", "Mi", "Fa", "Fi", "So", "Si" 
            };
            int notescale = NoteScale.FromNumber(notenumber);
            return fixedDoReMi[notescale];
        }
        else if (sheetmusic.ShowNoteLetters == MidiOptions.NoteNameMovableDoReMi) {
            string[] fixedDoReMi = {
                "La", "Li", "Ti", "Do", "Di", "Re", "Ri", "Mi", "Fa", "Fi", "So", "Si" 
            };
            int mainscale = sheetmusic.MainKey.Notescale();
            int diff = NoteScale.C - mainscale;
            notenumber += diff;
            if (notenumber < 0) {
                notenumber += 12;
            }
            int notescale = NoteScale.FromNumber(notenumber);
            return fixedDoReMi[notescale];
        }
        else if (sheetmusic.ShowNoteLetters == MidiOptions.NoteNameFixedNumber) {
            string[] num = {
                "10", "11", "12", "1", "2", "3", "4", "5", "6", "7", "8", "9" 
            };
            int notescale = NoteScale.FromNumber(notenumber);
            return num[notescale];
        }
        else if (sheetmusic.ShowNoteLetters == MidiOptions.NoteNameMovableNumber) {
            string[] num = {
                "10", "11", "12", "1", "2", "3", "4", "5", "6", "7", "8", "9" 
            };
            int mainscale = sheetmusic.MainKey.Notescale();
            int diff = NoteScale.C - mainscale;
            notenumber += diff;
            if (notenumber < 0) {
                notenumber += 12;
            }
            int notescale = NoteScale.FromNumber(notenumber);
            return num[notescale];
        }
        else {
            return "";
        }
    }

    /** Get the letter (A, A#, Bb) representing this note */
    private string Letter(int notenumber, WhiteNote whitenote) {
        int notescale = NoteScale.FromNumber(notenumber);
        switch(notescale) {
            case NoteScale.A: return "A";
            case NoteScale.B: return "B";
            case NoteScale.C: return "C";
            case NoteScale.D: return "D";
            case NoteScale.E: return "E";
            case NoteScale.F: return "F";
            case NoteScale.G: return "G";
            case NoteScale.Asharp:
                if (whitenote.Letter == WhiteNote.A)
                    return "A#";
                else
                    return "Bb";
            case NoteScale.Csharp:
                if (whitenote.Letter == WhiteNote.C)
                    return "C#";
                else
                    return "Db";
            case NoteScale.Dsharp:
                if (whitenote.Letter == WhiteNote.D)
                    return "D#";
                else
                    return "Eb";
            case NoteScale.Fsharp:
                if (whitenote.Letter == WhiteNote.F)
                    return "F#";
                else
                    return "Gb";
            case NoteScale.Gsharp:
                if (whitenote.Letter == WhiteNote.G)
                    return "G#";
                else
                    return "Ab";
            default:
                return "";
        }
    }

    /** Draw the Chord Symbol:
     * - Draw the accidental symbols.
     * - Draw the black circle notes.
     * - Draw the stems.
      @param ytop The ylocation (in pixels) where the top of the staff starts.
     */
    public override void Draw(Graphics g, Pen pen, int ytop) {
        /* Align the chord to the right */
        g.TranslateTransform(Width - MinWidth, 0);

        /* Draw the accidentals. */
        WhiteNote topstaff = WhiteNote.Top(clef);
        int xpos = DrawAccid(g, pen, ytop);

        /* Draw the notes */
        g.TranslateTransform(xpos, 0);
        DrawNotes(g, pen, ytop, topstaff);
        if (sheetmusic != null && sheetmusic.ShowNoteLetters != 0) {
            DrawNoteLetters(g, pen, ytop, topstaff);
        }

        /* Draw the stems */
        if (stem1 != null)
            stem1.Draw(g, pen, ytop, topstaff);
        if (stem2 != null)
            stem2.Draw(g, pen, ytop, topstaff);

        g.TranslateTransform(-xpos, 0);
        g.TranslateTransform(-(Width - MinWidth), 0);
    }

    /* Draw the accidental symbols.  If two symbols overlap (if they
     * are less than 6 notes apart), we cannot draw the symbol directly
     * above the previous one.  Instead, we must shift it to the right.
     * @param ytop The ylocation (in pixels) where the top of the staff starts.
     * @return The x pixel width used by all the accidentals.
     */
    public int DrawAccid(Graphics g, Pen pen, int ytop) {
        int xpos = 0;

        AccidSymbol prev = null;
        foreach (AccidSymbol symbol in accidsymbols) {
            if (prev != null && symbol.Note.Dist(prev.Note) < 6) {
                xpos += symbol.Width;
            }
            g.TranslateTransform(xpos, 0);
            symbol.Draw(g, pen, ytop);
            g.TranslateTransform(-xpos, 0);
            prev = symbol;
        }
        if (prev != null) {
            xpos += prev.Width;
        }
        return xpos;
    }

    /** Draw the black circle notes.
     * @param ytop The ylocation (in pixels) where the top of the staff starts.
     * @param topstaff The white note of the top of the staff.
     */
    public void DrawNotes(Graphics g, Pen pen, int ytop, WhiteNote topstaff) {
        pen.Width = 1;
        foreach (NoteData note in notedata) {
            /* Get the x,y position to draw the note */
            int ynote = ytop + topstaff.Dist(note.whitenote) * 
                        SheetMusic.NoteHeight/2;

            int xnote = SheetMusic.LineSpace/4;
            if (!note.leftside)
                xnote += SheetMusic.NoteWidth;

            /* Draw rotated ellipse.  You must first translate (0,0)
             * to the center of the ellipse.
             */
            g.TranslateTransform(xnote + SheetMusic.NoteWidth/2 + 1, 
                                 ynote - SheetMusic.LineWidth + 
                                 SheetMusic.NoteHeight/2);
            g.RotateTransform(-45);

            if (sheetmusic != null) {
                pen.Color = sheetmusic.NoteColor(note.number);
            }
            else {
                pen.Color = Color.Black;
            }

            if (note.duration == NoteDuration.Whole || 
                note.duration == NoteDuration.Half ||
                note.duration == NoteDuration.DottedHalf) {

                g.DrawEllipse(pen, -SheetMusic.NoteWidth/2, 
                              -SheetMusic.NoteHeight/2,
                              SheetMusic.NoteWidth,
                              SheetMusic.NoteHeight-1);

                g.DrawEllipse(pen, -SheetMusic.NoteWidth/2, 
                              -SheetMusic.NoteHeight/2 + 1,
                              SheetMusic.NoteWidth,
                              SheetMusic.NoteHeight-2);

                g.DrawEllipse(pen, -SheetMusic.NoteWidth/2, 
                              -SheetMusic.NoteHeight/2 + 1,
                              SheetMusic.NoteWidth,
                              SheetMusic.NoteHeight-3);

            }
            else {
                Brush brush = Brushes.Black;
                if (pen.Color != Color.Black) {
                    brush = new SolidBrush(pen.Color);
                }
                g.FillEllipse(brush, -SheetMusic.NoteWidth/2, 
                              -SheetMusic.NoteHeight/2,
                              SheetMusic.NoteWidth,
                              SheetMusic.NoteHeight-1);
                if (pen.Color != Color.Black) {
                    brush.Dispose();
                }
            }

            pen.Color = Color.Black;
            g.DrawEllipse(pen, -SheetMusic.NoteWidth/2, 
                          -SheetMusic.NoteHeight/2,
                           SheetMusic.NoteWidth,
                           SheetMusic.NoteHeight-1);

            g.RotateTransform(45);
            g.TranslateTransform( - (xnote + SheetMusic.NoteWidth/2 + 1), 
                                  - (ynote - SheetMusic.LineWidth + 
                                     SheetMusic.NoteHeight/2));

            /* Draw a dot if this is a dotted duration. */
            if (note.duration == NoteDuration.DottedHalf ||
                note.duration == NoteDuration.DottedQuarter ||
                note.duration == NoteDuration.DottedEighth) {

                g.FillEllipse(Brushes.Black, 
                              xnote + SheetMusic.NoteWidth + 
                              SheetMusic.LineSpace/3,
                              ynote + SheetMusic.LineSpace/3, 4, 4);

            }

            /* Draw horizontal lines if note is above/below the staff */
            WhiteNote top = topstaff.Add(1);
            int dist = note.whitenote.Dist(top);
            int y = ytop - SheetMusic.LineWidth;

            if (dist >= 2) {
                for (int i = 2; i <= dist; i += 2) {
                    y -= SheetMusic.NoteHeight;
                    g.DrawLine(pen, xnote - SheetMusic.LineSpace/4, y, 
                                    xnote + SheetMusic.NoteWidth + 
                                    SheetMusic.LineSpace/4, y);
                }
            }

            WhiteNote bottom = top.Add(-8);
            y = ytop + (SheetMusic.LineSpace + SheetMusic.LineWidth) * 4 - 1;
            dist = bottom.Dist(note.whitenote);
            if (dist >= 2) {
                for (int i = 2; i <= dist; i+= 2) {
                    y += SheetMusic.NoteHeight;
                    g.DrawLine(pen, xnote - SheetMusic.LineSpace/4, y, 
                                    xnote + SheetMusic.NoteWidth + 
                                    SheetMusic.LineSpace/4, y);
                }
            }
            /* End drawing horizontal lines */

        }
    }

    /** Draw the note letters (A, A#, Bb, etc) next to the note circles.
     * @param ytop The ylocation (in pixels) where the top of the staff starts.
     * @param topstaff The white note of the top of the staff.
     */
    public void DrawNoteLetters(Graphics g, Pen pen, int ytop, WhiteNote topstaff) {
        bool overlap = NotesOverlap(notedata, 0, notedata.Length);
        pen.Width = 1;

        foreach (NoteData note in notedata) {
            if (!note.leftside) {
                /* There's not enought pixel room to show the letter */
                continue;
            }

            /* Get the x,y position to draw the note */
            int ynote = ytop + topstaff.Dist(note.whitenote) * 
                        SheetMusic.NoteHeight/2;

            /* Draw the letter to the right side of the note */
            int xnote = SheetMusic.NoteWidth + SheetMusic.LineSpace/4;

            if (note.duration == NoteDuration.DottedHalf ||
                note.duration == NoteDuration.DottedQuarter ||
                note.duration == NoteDuration.DottedEighth || overlap) {

                xnote += SheetMusic.NoteWidth/2;
            } 
            g.DrawString(NoteName(note.number, note.whitenote),
                         SheetMusic.LetterFont, 
                         Brushes.Black,
                         xnote,
                         ynote - SheetMusic.NoteHeight/2);
        }
    }


    /** Return true if the chords can be connected, where their stems are
     * joined by a horizontal beam. In order to create the beam:
     *
     * - The chords must be in the same measure.
     * - The chord stems should not be a dotted duration.
     * - The chord stems must be the same duration, with one exception
     *   (Dotted Eighth to Sixteenth).
     * - The stems must all point in the same direction (up or down).
     * - The chord cannot already be part of a beam.
     *
     * - 6-chord beams must be 8th notes in 3/4, 6/8, or 6/4 time
     * - 3-chord beams must be either triplets, or 8th notes (12/8 time signature)
     * - 4-chord beams are ok for 2/2, 2/4 or 4/4 time, any duration
     * - 4-chord beams are ok for other times if the duration is 16th
     * - 2-chord beams are ok for any duration
     *
     * If startQuarter is true, the first note should start on a quarter note
     * (only applies to 2-chord beams).
     */
    public static 
    bool CanCreateBeam(ChordSymbol[] chords, TimeSignature time, bool startQuarter) {
        int numChords = chords.Length;
        Stem firstStem = chords[0].Stem;
        Stem lastStem = chords[chords.Length-1].Stem;
        if (firstStem == null || lastStem == null) {
            return false;
        }
        int measure = chords[0].StartTime / time.Measure;
        NoteDuration dur = firstStem.Duration;
        NoteDuration dur2 = lastStem.Duration;

        bool dotted8_to_16 = false;
        if (chords.Length == 2 && dur == NoteDuration.DottedEighth &&
            dur2 == NoteDuration.Sixteenth) {
            dotted8_to_16 = true;
        } 

        if (dur == NoteDuration.Whole || dur == NoteDuration.Half ||
            dur == NoteDuration.DottedHalf || dur == NoteDuration.Quarter ||
            dur == NoteDuration.DottedQuarter ||
            (dur == NoteDuration.DottedEighth && !dotted8_to_16)) {

            return false;
        }

        if (numChords == 6) {
            if (dur != NoteDuration.Eighth) {
                return false;
            }
            bool correctTime = 
               ((time.Numerator == 3 && time.Denominator == 4) ||
                (time.Numerator == 6 && time.Denominator == 8) ||
                (time.Numerator == 6 && time.Denominator == 4) );

            if (!correctTime) {
                return false;
            }

            if (time.Numerator == 6 && time.Denominator == 4) {
                /* first chord must start at 1st or 4th quarter note */
                int beat = time.Quarter * 3;
                if ((chords[0].StartTime % beat) > time.Quarter/6) {
                    return false;
                } 
            }
        }
        else if (numChords == 4) {
            if (time.Numerator == 3 && time.Denominator == 8) {
                return false;
            }
            bool correctTime = 
              (time.Numerator == 2 || time.Numerator == 4 || time.Numerator == 8);
            if (!correctTime && dur != NoteDuration.Sixteenth) {
                return false;
            }

            /* chord must start on quarter note */
            int beat = time.Quarter;
            if (dur == NoteDuration.Eighth) {
                /* 8th note chord must start on 1st or 3rd quarter note */
                beat = time.Quarter * 2;
            }
            else if (dur == NoteDuration.ThirtySecond) {
                /* 32nd note must start on an 8th beat */
                beat = time.Quarter / 2;
            }

            if ((chords[0].StartTime % beat) > time.Quarter/6) {
                return false;
            }
        }
        else if (numChords == 3) {
            bool valid = (dur == NoteDuration.Triplet) || 
                          (dur == NoteDuration.Eighth &&
                           time.Numerator == 12 && time.Denominator == 8);
            if (!valid) {
                return false;
            }

            /* chord must start on quarter note */
            int beat = time.Quarter;
            if (time.Numerator == 12 && time.Denominator == 8) {
                /* In 12/8 time, chord must start on 3*8th beat */
                beat = time.Quarter/2 * 3;
            }
            if ((chords[0].StartTime % beat) > time.Quarter/6) {
                return false;
            }
        }

        else if (numChords == 2) {
            if (startQuarter) {
                int beat = time.Quarter;
                if ((chords[0].StartTime % beat) > time.Quarter/6) {
                    return false;
                }
            }
        }

        foreach (ChordSymbol chord in chords) {
            if ((chord.StartTime / time.Measure) != measure)
                return false;
            if (chord.Stem == null)
                return false;
            if (chord.Stem.Duration != dur && !dotted8_to_16)
                return false;
            if (chord.Stem.isBeam)
                return false;
        }

        /* Check that all stems can point in same direction */
        bool hasTwoStems = false;
        int direction = Stem.Up; 
        foreach (ChordSymbol chord in chords) {
            if (chord.HasTwoStems) {
                if (hasTwoStems && chord.Stem.Direction != direction) {
                    return false;
                }
                hasTwoStems = true;
                direction = chord.Stem.Direction;
            }
        }

        /* Get the final stem direction */
        if (!hasTwoStems) {
            WhiteNote note1;
            WhiteNote note2;
            note1 = (firstStem.Direction == Stem.Up ? firstStem.Top : firstStem.Bottom);
            note2 = (lastStem.Direction == Stem.Up ? lastStem.Top : lastStem.Bottom);
            direction = StemDirection(note1, note2, chords[0].Clef);
        }

        /* If the notes are too far apart, don't use a beam */
        if (direction == Stem.Up) {
            if (Math.Abs(firstStem.Top.Dist(lastStem.Top)) >= 11) {
                return false;
            }
        }
        else {
            if (Math.Abs(firstStem.Bottom.Dist(lastStem.Bottom)) >= 11) {
                return false;
            }
        }
        return true;
    }


    /** Connect the chords using a horizontal beam. 
     *
     * spacing is the horizontal distance (in pixels) between the right side 
     * of the first chord, and the right side of the last chord.
     *
     * To make the beam:
     * - Change the stem directions for each chord, so they match.
     * - In the first chord, pass the stem location of the last chord, and
     *   the horizontal spacing to that last stem.
     * - Mark all chords (except the first) as "receiver" pairs, so that 
     *   they don't draw a curvy stem.
     */
    public static 
    void CreateBeam(ChordSymbol[] chords, int spacing) {
        Stem firstStem = chords[0].Stem;
        Stem lastStem = chords[chords.Length-1].Stem;

        /* Calculate the new stem direction */
        int newdirection = -1;
        foreach (ChordSymbol chord in chords) {
            if (chord.HasTwoStems) {
                newdirection = chord.Stem.Direction;
                break;
            }
        }

        if (newdirection == -1) {
            WhiteNote note1;
            WhiteNote note2;
            note1 = (firstStem.Direction == Stem.Up ? firstStem.Top : firstStem.Bottom);
            note2 = (lastStem.Direction == Stem.Up ? lastStem.Top : lastStem.Bottom);
            newdirection = StemDirection(note1, note2, chords[0].Clef);
        }
        foreach (ChordSymbol chord in chords) {
            chord.Stem.Direction = newdirection;
        }

        if (chords.Length == 2) {
            BringStemsCloser(chords);
        }
        else {
            LineUpStemEnds(chords);
        }

        firstStem.SetPair(lastStem, spacing);
        for (int i = 1; i < chords.Length; i++) {
            chords[i].Stem.Receiver = true;
        }
    }

    /** We're connecting the stems of two chords using a horizontal beam.
     *  Adjust the vertical endpoint of the stems, so that they're closer
     *  together.  For a dotted 8th to 16th beam, increase the stem of the
     *  dotted eighth, so that it's as long as a 16th stem.
     */
    static void
    BringStemsCloser(ChordSymbol[] chords) {
        Stem firstStem = chords[0].Stem;
        Stem lastStem = chords[1].Stem;

        /* If we're connecting a dotted 8th to a 16th, increase
         * the stem end of the dotted eighth.
         */
        if (firstStem.Duration == NoteDuration.DottedEighth &&
            lastStem.Duration == NoteDuration.Sixteenth) {
            if (firstStem.Direction == Stem.Up) {
                firstStem.End = firstStem.End.Add(2);
            }
            else {
                firstStem.End = firstStem.End.Add(-2);
            }
        }

        /* Bring the stem ends closer together */
        int distance = Math.Abs(firstStem.End.Dist(lastStem.End));
        if (firstStem.Direction == Stem.Up) {
            if (WhiteNote.Max(firstStem.End, lastStem.End) == firstStem.End)
                lastStem.End = lastStem.End.Add(distance/2);
            else
                firstStem.End = firstStem.End.Add(distance/2);
        }
        else {
            if (WhiteNote.Min(firstStem.End, lastStem.End) == firstStem.End)
                lastStem.End = lastStem.End.Add(-distance/2);
            else
                firstStem.End = firstStem.End.Add(-distance/2);
        }
    }

    /** We're connecting the stems of three or more chords using a horizontal beam.
     *  Adjust the vertical endpoint of the stems, so that the middle chord stems
     *  are vertically in between the first and last stem.
     */
    static void
    LineUpStemEnds(ChordSymbol[] chords) {
        Stem firstStem = chords[0].Stem;
        Stem lastStem = chords[chords.Length-1].Stem;
        Stem middleStem = chords[1].Stem;

        if (firstStem.Direction == Stem.Up) {
            /* Find the highest stem. The beam will either:
             * - Slant downwards (first stem is highest)
             * - Slant upwards (last stem is highest)
             * - Be straight (middle stem is highest)
             */
            WhiteNote top = firstStem.End;
            foreach (ChordSymbol chord in chords) {
                top = WhiteNote.Max(top, chord.Stem.End);
            }
            if (top == firstStem.End && top.Dist(lastStem.End) >= 2) {
                firstStem.End = top;
                middleStem.End = top.Add(-1);
                lastStem.End = top.Add(-2);
            }
            else if (top == lastStem.End && top.Dist(firstStem.End) >= 2) {
                firstStem.End = top.Add(-2);
                middleStem.End = top.Add(-1);
                lastStem.End = top;
            }
            else {
                firstStem.End = top;
                middleStem.End = top;
                lastStem.End = top;
            }
        }
        else {
            /* Find the bottommost stem. The beam will either:
             * - Slant upwards (first stem is lowest)
             * - Slant downwards (last stem is lowest)
             * - Be straight (middle stem is highest)
             */
            WhiteNote bottom = firstStem.End;
            foreach (ChordSymbol chord in chords) {
                bottom = WhiteNote.Min(bottom, chord.Stem.End);
            }

            if (bottom == firstStem.End && lastStem.End.Dist(bottom) >= 2) {
                middleStem.End = bottom.Add(1);
                lastStem.End = bottom.Add(2);
            }
            else if (bottom == lastStem.End && firstStem.End.Dist(bottom) >= 2) {
                middleStem.End = bottom.Add(1);
                firstStem.End = bottom.Add(2);
            }
            else {
                firstStem.End = bottom;
                middleStem.End = bottom;
                lastStem.End = bottom;
            }
        }

        /* All middle stems have the same end */
        for (int i = 1; i < chords.Length-1; i++) {
            Stem stem = chords[i].Stem;
            stem.End = middleStem.End;
        }
    }

    public override string ToString() {
        string result = string.Format("ChordSymbol clef={0} start={1} end={2} width={3} hastwostems={4} ", 
                                      clef, StartTime, EndTime, Width, hastwostems);
        foreach (AccidSymbol symbol in accidsymbols) {
            result += symbol.ToString() + " ";
        }
        foreach (NoteData note in notedata) {
            result += string.Format("Note whitenote={0} duration={1} leftside={2} ",
                                    note.whitenote, note.duration, note.leftside);
        }
        if (stem1 != null) {
            result += stem1.ToString() + " ";
        }
        if (stem2 != null) {
            result += stem2.ToString() + " ";
        }
        return result; 
    }

}


}


