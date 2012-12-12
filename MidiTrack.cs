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


namespace MidiSheetMusic {

/** @class MidiTrack
 * The MidiTrack takes as input the raw MidiEvents for the track, and gets:
 * - The list of midi notes in the track.
 * - The first instrument used in the track.
 *
 * For each NoteOn event in the midi file, a new MidiNote is created
 * and added to the track, using the AddNote() method.
 * 
 * The NoteOff() method is called when a NoteOff event is encountered,
 * in order to update the duration of the MidiNote.
 */ 
public class MidiTrack {
    private int tracknum;             /** The track number */
    private List<MidiNote> notes;     /** List of Midi notes */
    private int instrument;           /** Instrument for this track */
    private List<MidiEvent> lyrics;   /** The lyrics in this track */

    /** Create an empty MidiTrack.  Used by the Clone method */
    public MidiTrack(int tracknum) {
        this.tracknum = tracknum;
        notes = new List<MidiNote>(20);
        instrument = 0;
    } 

    /** Create a MidiTrack based on the Midi events.  Extract the NoteOn/NoteOff
     *  events to gather the list of MidiNotes.
     */
    public MidiTrack(List<MidiEvent> events, int tracknum) {
        this.tracknum = tracknum;
        notes = new List<MidiNote>(events.Count);
        instrument = 0;
 
        foreach (MidiEvent mevent in events) {
            if (mevent.EventFlag == MidiFile.EventNoteOn && mevent.Velocity > 0) {
                MidiNote note = new MidiNote(mevent.StartTime, mevent.Channel, mevent.Notenumber, 0);
                AddNote(note);
            }
            else if (mevent.EventFlag == MidiFile.EventNoteOn && mevent.Velocity == 0) {
                NoteOff(mevent.Channel, mevent.Notenumber, mevent.StartTime);
            }
            else if (mevent.EventFlag == MidiFile.EventNoteOff) {
                NoteOff(mevent.Channel, mevent.Notenumber, mevent.StartTime);
            }
            else if (mevent.EventFlag == MidiFile.EventProgramChange) {
                instrument = mevent.Instrument;
            }
            else if (mevent.Metaevent == MidiFile.MetaEventLyric) {
                if (lyrics == null) {
                    lyrics = new List<MidiEvent>();
                } 
                lyrics.Add(mevent);
            }
        }
        if (notes.Count > 0 && notes[0].Channel == 9)  {
            instrument = 128;  /* Percussion */
        }
        int lyriccount = 0;
        if (lyrics != null) { lyriccount = lyrics.Count; }
    }

    public int Number {
        get { return tracknum; }
    }

    public List<MidiNote> Notes {
        get { return notes; }
    }

    public int Instrument {
        get { return instrument; }
        set { instrument = value; }
    }

    public string InstrumentName {
        get { if (instrument >= 0 && instrument <= 128)
                  return MidiFile.Instruments[instrument];
              else
                  return "";
            }
    }

    public List<MidiEvent> Lyrics {
        get { return lyrics; }
        set { lyrics = value; }
    }

    /** Add a MidiNote to this track.  This is called for each NoteOn event */
    public void AddNote(MidiNote m) {
        notes.Add(m);
    }

    /** A NoteOff event occured.  Find the MidiNote of the corresponding
     * NoteOn event, and update the duration of the MidiNote.
     */
    public void NoteOff(int channel, int notenumber, int endtime) {
        for (int i = notes.Count-1; i >= 0; i--) {
            MidiNote note = notes[i];
            if (note.Channel == channel && note.Number == notenumber &&
                note.Duration == 0) {
                note.NoteOff(endtime);
                return;
            }
        }
    }

    /** Return a deep copy clone of this MidiTrack. */
    public MidiTrack Clone() {
        MidiTrack track = new MidiTrack(Number);
        track.instrument = instrument;
        foreach (MidiNote note in notes) {
            track.notes.Add( note.Clone() );
        }
        if (lyrics != null) {
            track.lyrics = new List<MidiEvent>();
            foreach (MidiEvent ev in lyrics) {
                track.lyrics.Add(ev);
            }
        }
        return track;
    }
    public override string ToString() {
        string result = "Track number=" + tracknum + " instrument=" + instrument + "\n";
        foreach (MidiNote n in notes) {
           result = result + n + "\n";
        }
        result += "End Track\n";
        return result;
    }
}

}

