/*
 * Copyright (c) 2007-2008 Madhav Vaidyanathan
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

/** @class ClefMeasures
 * The ClefMeasures class is used to report what Clef (Treble or Bass) a
 * given measure uses.
 */
public class ClefMeasures {
    private List<Clef> clefs;  /** The clefs used for each measure (for a single track) */
    private int measure;       /** The length of a measure, in pulses */

 
    /** Given the notes in a track, calculate the appropriate Clef to use
     * for each measure.  Store the result in the clefs list.
     * @param notes  The midi notes
     * @param measurelen The length of a measure, in pulses
     */
    public ClefMeasures(List<MidiNote> notes, int measurelen) {
        measure = measurelen;
        Clef mainclef = MainClef(notes);
        int nextmeasure = measurelen;
        int pos = 0;
        Clef clef = mainclef;

        clefs = new List<Clef>();

        while (pos < notes.Count) {
            /* Sum all the notes in the current measure */
            int sumnotes = 0;
            int notecount = 0;
            while (pos < notes.Count && notes[pos].StartTime < nextmeasure) {
                sumnotes += notes[pos].Number;
                notecount++;
                pos++;
            }
            if (notecount == 0)
                notecount = 1;

            /* Calculate the "average" note in the measure */
            int avgnote = sumnotes / notecount;
            if (avgnote == 0) {
                /* This measure doesn't contain any notes.
                 * Keep the previous clef.
                 */
            }
            else if (avgnote >= WhiteNote.BottomTreble.Number()) {
                clef = Clef.Treble;
            }
            else if (avgnote <= WhiteNote.TopBass.Number()) {
                clef = Clef.Bass;
            }
            else {
                /* The average note is between G3 and F4. We can use either
                 * the treble or bass clef.  Use the "main" clef, the clef
                 * that appears most for this track.
                 */
                clef = mainclef;
            }

            clefs.Add(clef);
            nextmeasure += measurelen;
        }
        clefs.Add(clef);
    }

    /** Given a time (in pulses), return the clef used for that measure. */
    public Clef GetClef(int starttime) {

        /* If the time exceeds the last measure, return the last measure */
        if (starttime / measure >= clefs.Count) {
            return clefs[ clefs.Count-1 ];
        }
        else {
            return clefs[ starttime / measure ];
        }
    }

    /** Calculate the best clef to use for the given notes.  If the
     * average note is below Middle C, use a bass clef.  Else, use a treble
     * clef.
     */
    private static Clef MainClef(List<MidiNote> notes) {
        int middleC = WhiteNote.MiddleC.Number();
        int total = 0;
        foreach (MidiNote m in notes) {
            total += m.Number;
        }
        if (notes.Count == 0) {
            return Clef.Treble;
        }
        else if (total/notes.Count >= middleC) {
            return Clef.Treble;
        }
        else {
            return Clef.Bass;
        }
    }
}


}

