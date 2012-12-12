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


/** Accidentals */
public enum Accid {
    None, Sharp, Flat, Natural
}

/** @class AccidSymbol
 * An accidental (accid) symbol represents a sharp, flat, or natural
 * accidental that is displayed at a specific position (note and clef).
 */
public class AccidSymbol : MusicSymbol {
    private Accid accid;          /** The accidental (sharp, flat, natural) */
    private WhiteNote whitenote;  /** The white note where the symbol occurs */
    private Clef clef;            /** Which clef the symbols is in */
    private int width;            /** Width of symbol */

    /** 
     * Create a new AccidSymbol with the given accidental, that is
     * displayed at the given note in the given clef.
     */
    public AccidSymbol(Accid accid, WhiteNote note, Clef clef) {
        this.accid = accid;
        this.whitenote = note;
        this.clef = clef;
        width = MinWidth;
    }

    /** Return the white note this accidental is displayed at */
    public WhiteNote Note  {
        get { return whitenote; }
    }

    /** Get the time (in pulses) this symbol occurs at.
     * Not used.  Instead, the StartTime of the ChordSymbol containing this
     * AccidSymbol is used.
     */
    public override int StartTime { 
        get { return -1; }  
    }

    /** Get the minimum width (in pixels) needed to draw this symbol */
    public override int MinWidth { 
        get { return 3*SheetMusic.NoteHeight/2; }
    }

    /** Get/Set the width (in pixels) of this symbol. The width is set
     * in SheetMusic.AlignSymbols() to vertically align symbols.
     */
    public override int Width {
        get { return width; }
        set { width = value; }
    }

    /** Get the number of pixels this symbol extends above the staff. Used
     *  to determine the minimum height needed for the staff (Staff.FindBounds).
     */
    public override int AboveStaff {
        get { return GetAboveStaff(); }
    }

    int GetAboveStaff() {
        int dist = WhiteNote.Top(clef).Dist(whitenote) * 
                   SheetMusic.NoteHeight/2;
        if (accid == Accid.Sharp || accid == Accid.Natural)
            dist -= SheetMusic.NoteHeight;
        else if (accid == Accid.Flat)
            dist -= 3*SheetMusic.NoteHeight/2;

        if (dist < 0)
            return -dist;
        else
            return 0;
    }

    /** Get the number of pixels this symbol extends below the staff. Used
     *  to determine the minimum height needed for the staff (Staff.FindBounds).
     */
    public override int BelowStaff {
        get { return GetBelowStaff(); }
    }

    private int GetBelowStaff() {
        int dist = WhiteNote.Bottom(clef).Dist(whitenote) * 
                   SheetMusic.NoteHeight/2 + 
                   SheetMusic.NoteHeight;
        if (accid == Accid.Sharp || accid == Accid.Natural) 
            dist += SheetMusic.NoteHeight;

        if (dist > 0)
            return dist;
        else 
            return 0;
    }

    /** Draw the symbol.
     * @param ytop The ylocation (in pixels) where the top of the staff starts.
     */
    public override void Draw(Graphics g, Pen pen, int ytop) {
        /* Align the symbol to the right */
        g.TranslateTransform(Width - MinWidth, 0);

        /* Store the y-pixel value of the top of the whitenote in ynote. */
        int ynote = ytop + WhiteNote.Top(clef).Dist(whitenote) * 
                    SheetMusic.NoteHeight/2;

        if (accid == Accid.Sharp)
            DrawSharp(g, pen, ynote);
        else if (accid == Accid.Flat)
            DrawFlat(g, pen, ynote);
        else if (accid == Accid.Natural)
            DrawNatural(g, pen, ynote);

        g.TranslateTransform(-(Width - MinWidth), 0);
    }

    /** Draw a sharp symbol. 
     * @param ynote The pixel location of the top of the accidental's note. 
     */
    public void DrawSharp(Graphics g, Pen pen, int ynote) {

        /* Draw the two vertical lines */
        int ystart = ynote - SheetMusic.NoteHeight;
        int yend = ynote + 2*SheetMusic.NoteHeight;
        int x = SheetMusic.NoteHeight/2;
        pen.Width = 1;
        g.DrawLine(pen, x, ystart + 2, x, yend);
        x += SheetMusic.NoteHeight/2;
        g.DrawLine(pen, x, ystart, x, yend - 2);

        /* Draw the slightly upwards horizontal lines */
        int xstart = SheetMusic.NoteHeight/2 - SheetMusic.NoteHeight/4;
        int xend = SheetMusic.NoteHeight + SheetMusic.NoteHeight/4;
        ystart = ynote + SheetMusic.LineWidth;
        yend = ystart - SheetMusic.LineWidth - SheetMusic.LineSpace/4;
        pen.Width = SheetMusic.LineSpace/2;
        g.DrawLine(pen, xstart, ystart, xend, yend);
        ystart += SheetMusic.LineSpace;
        yend += SheetMusic.LineSpace;
        g.DrawLine(pen, xstart, ystart, xend, yend);
        pen.Width = 1;
    }

    /** Draw a flat symbol.
     * @param ynote The pixel location of the top of the accidental's note.
     */
    public void DrawFlat(Graphics g, Pen pen, int ynote) {
        int x = SheetMusic.LineSpace/4;

        /* Draw the vertical line */
        pen.Width = 1;
        g.DrawLine(pen, x, ynote - SheetMusic.NoteHeight - SheetMusic.NoteHeight/2,
                        x, ynote + SheetMusic.NoteHeight);

        /* Draw 3 bezier curves.
         * All 3 curves start and stop at the same points.
         * Each subsequent curve bulges more and more towards 
         * the topright corner, making the curve look thicker
         * towards the top-right.
         */
        g.DrawBezier(pen, x, ynote + SheetMusic.LineSpace/4,
            x + SheetMusic.LineSpace/2, ynote - SheetMusic.LineSpace/2,
            x + SheetMusic.LineSpace, ynote + SheetMusic.LineSpace/3,
            x, ynote + SheetMusic.LineSpace + SheetMusic.LineWidth + 1);

        g.DrawBezier(pen, x, ynote + SheetMusic.LineSpace/4,
            x + SheetMusic.LineSpace/2, ynote - SheetMusic.LineSpace/2,
            x + SheetMusic.LineSpace + SheetMusic.LineSpace/4, 
              ynote + SheetMusic.LineSpace/3 - SheetMusic.LineSpace/4,
            x, ynote + SheetMusic.LineSpace + SheetMusic.LineWidth + 1);


        g.DrawBezier(pen, x, ynote + SheetMusic.LineSpace/4,
            x + SheetMusic.LineSpace/2, ynote - SheetMusic.LineSpace/2,
            x + SheetMusic.LineSpace + SheetMusic.LineSpace/2, 
             ynote + SheetMusic.LineSpace/3 - SheetMusic.LineSpace/2,
            x, ynote + SheetMusic.LineSpace + SheetMusic.LineWidth + 1);


    }

    /** Draw a natural symbol.
     * @param ynote The pixel location of the top of the accidental's note.
     */
    public void DrawNatural(Graphics g, Pen pen, int ynote) {

        /* Draw the two vertical lines */
        int ystart = ynote - SheetMusic.LineSpace - SheetMusic.LineWidth;
        int yend = ynote + SheetMusic.LineSpace + SheetMusic.LineWidth;
        int x = SheetMusic.LineSpace/2;
        pen.Width = 1;
        g.DrawLine(pen, x, ystart, x, yend);
        x += SheetMusic.LineSpace - SheetMusic.LineSpace/4;
        ystart = ynote - SheetMusic.LineSpace/4;
        yend = ynote + 2*SheetMusic.LineSpace + SheetMusic.LineWidth - 
                 SheetMusic.LineSpace/4;
        g.DrawLine(pen, x, ystart, x, yend);

        /* Draw the slightly upwards horizontal lines */
        int xstart = SheetMusic.LineSpace/2;
        int xend = xstart + SheetMusic.LineSpace - SheetMusic.LineSpace/4;
        ystart = ynote + SheetMusic.LineWidth;
        yend = ystart - SheetMusic.LineWidth - SheetMusic.LineSpace/4;
        pen.Width = SheetMusic.LineSpace/2;
        g.DrawLine(pen, xstart, ystart, xend, yend);
        ystart += SheetMusic.LineSpace;
        yend += SheetMusic.LineSpace;
        g.DrawLine(pen, xstart, ystart, xend, yend);
        pen.Width = 1;
    }

    public override string ToString() {
        return string.Format(
          "AccidSymbol accid={0} whitenote={1} clef={2} width={3}",
          accid, whitenote, clef, width);
    }

}

}


