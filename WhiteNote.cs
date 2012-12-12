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
using System.Collections.Generic;

namespace MidiSheetMusic {

/** Enumeration of the notes in a scale (A, A#, ... G#) */
public class NoteScale {
    public const int A      = 0;
    public const int Asharp = 1;
    public const int Bflat  = 1;
    public const int B      = 2;
    public const int C      = 3;
    public const int Csharp = 4;
    public const int Dflat  = 4;
    public const int D      = 5;
    public const int Dsharp = 6;
    public const int Eflat  = 6;
    public const int E      = 7;
    public const int F      = 8;
    public const int Fsharp = 9;
    public const int Gflat  = 9;
    public const int G      = 10;
    public const int Gsharp = 11;
    public const int Aflat  = 11;

    /** Convert a note (A, A#, B, etc) and octave into a
     * Midi Note number.
     */
    public static int ToNumber(int notescale, int octave) {
        return 9 + notescale + octave * 12;
    }

    /** Convert a Midi note number into a notescale (A, A#, B) */
    public static int FromNumber(int number) {
        return (number + 3) % 12;
    }

    /** Return true if this notescale number is a black key */
    public static bool IsBlackKey(int notescale) {
        if (notescale == Asharp ||
            notescale == Csharp ||
            notescale == Dsharp ||
            notescale == Fsharp ||
            notescale == Gsharp) {

            return true;
        }
        else {
            return false;
        }
    }
}


/** @class WhiteNote
 * The WhiteNote class represents a white key note, a non-sharp,
 * non-flat note.  To display midi notes as sheet music, the notes
 * must be converted to white notes and accidentals. 
 *
 * White notes consist of a letter (A thru G) and an octave (0 thru 10).
 * The octave changes from G to A.  After G2 comes A3.  Middle-C is C4.
 *
 * The main operations are calculating distances between notes, and comparing notes.
 */ 

public class WhiteNote : IComparer<WhiteNote> {

    /* The possible note letters */
    public const int A = 0;
    public const int B = 1;
    public const int C = 2;
    public const int D = 3;
    public const int E = 4;
    public const int F = 5;
    public const int G = 6;

    /* Common white notes used in calculations */
    public static WhiteNote TopTreble = new WhiteNote(WhiteNote.E, 5);
    public static WhiteNote BottomTreble = new WhiteNote(WhiteNote.F, 4);
    public static WhiteNote TopBass = new WhiteNote(WhiteNote.G, 3);
    public static WhiteNote BottomBass = new WhiteNote(WhiteNote.A, 3);
    public static WhiteNote MiddleC = new WhiteNote(WhiteNote.C, 4);

    private int letter;  /* The letter of the note, A thru G */
    private int octave;  /* The octave, 0 thru 10. */

    /* Get the letter */
    public int Letter {
        get { return letter; }
    }

    /* Get the octave */
    public int Octave {
        get { return octave; }
    }


    /** Create a new note with the given letter and octave. */
    public WhiteNote(int letter, int octave) {
        if (!(letter >= 0 && letter <= 6)) {
            throw new System.ArgumentException("Letter " + letter + " is incorrect");
        }

        this.letter = letter;
        this.octave = octave;
    }

    /** Return the distance (in white notes) between this note
     * and note w, i.e.  this - w.  For example, C4 - A4 = 2,
     */
    public int Dist(WhiteNote w) {
        return (octave - w.octave) * 7 + (letter - w.letter);
    }

    /** Return this note plus the given amount (in white notes).
     * The amount may be positive or negative.  For example,
     * A4 + 2 = C4, and C4 + (-2) = A4.
     */
    public WhiteNote Add(int amount) {
        int num = octave * 7 + letter;
        num += amount;
        return new WhiteNote(num % 7, num / 7);
    }

    /** Return the midi note number corresponding to this white note.
     * The midi note numbers cover all keys, including sharps/flats,
     * so each octave is 12 notes.  Middle C (C4) is 60.  Some example
     * numbers for various notes:
     *
     *  A 2 = 33
     *  A#2 = 34
     *  G 2 = 43
     *  G#2 = 44 
     *  A 3 = 45
     *  A 4 = 57
     *  A#4 = 58
     *  B 4 = 59
     *  C 4 = 60
     */

    public int Number() {
        int offset = 0;
        switch (letter) {
            case A: offset = NoteScale.A; break;
            case B: offset = NoteScale.B; break;
            case C: offset = NoteScale.C; break;
            case D: offset = NoteScale.D; break;
            case E: offset = NoteScale.E; break;
            case F: offset = NoteScale.F; break;
            case G: offset = NoteScale.G; break;
            default: offset = 0; break;
        }
        return NoteScale.ToNumber(offset, octave);
    }

    /** Compare the two notes.  Return
     *  < 0  if x is less (lower) than y
     *    0  if x is equal to y
     *  > 0  if x is greater (higher) than y
     */
    public int Compare(WhiteNote x, WhiteNote y) {
        return x.Dist(y);
    }

    /** Return the higher note, x or y */
    public static WhiteNote Max(WhiteNote x, WhiteNote y) {
        if (x.Dist(y) > 0)
            return x;
        else
            return y;
    }

    /** Return the lower note, x or y */
    public static WhiteNote Min(WhiteNote x, WhiteNote y) {
        if (x.Dist(y) < 0)
            return x;
        else
            return y;
    }

    /** Return the top note in the staff of the given clef */
    public static WhiteNote Top(Clef clef) {
        if (clef == Clef.Treble)
            return TopTreble;
        else
            return TopBass;
    }

    /** Return the bottom note in the staff of the given clef */
    public static WhiteNote Bottom(Clef clef) {
        if (clef == Clef.Treble)
            return BottomTreble;
        else
            return BottomBass;
    }

    /** Return the string <letter><octave> for this note. */
    public override 
    string ToString() {
        string[] s = { "A", "B", "C", "D", "E", "F", "G" };
        return s[letter] + octave;
    }
}



}
