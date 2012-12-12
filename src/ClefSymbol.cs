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

/** The possible clefs, Treble or Bass */
public enum Clef { Treble, Bass };

/** @class ClefSymbol 
 * A ClefSymbol represents either a Treble or Bass Clef image.
 * The clef can be either normal or small size.  Normal size is
 * used at the beginning of a new staff, on the left side.  The
 * small symbols are used to show clef changes within a staff.
 */

public class ClefSymbol : MusicSymbol {
    private static Image treble;  /** The treble clef image */
    private static Image bass;    /** The bass clef image */

    private int starttime;        /** Start time of the symbol */
    private bool smallsize;       /** True if this is a small clef, false otherwise */
    private Clef clef;            /** The clef, Treble or Bass */
    private int width;

    /** Create a new ClefSymbol, with the given clef, starttime, and size */
    public ClefSymbol(Clef clef, int starttime, bool small) {
        this.clef = clef;
        this.starttime = starttime;
        smallsize = small;
        LoadImages();
        width = MinWidth;
    }

    /** Load the Treble/Bass clef images into memory. */
    private static void LoadImages() {
        if (treble == null)
            treble = new Bitmap(typeof(ClefSymbol), "treble.png");

        if (bass == null)
            bass = new Bitmap(typeof(ClefSymbol), "bass.png");

    }

    /** Get the time (in pulses) this symbol occurs at.
     * This is used to determine the measure this symbol belongs to.
     */
    public override int StartTime { 
        get { return starttime; }
    }

    /** Get the minimum width (in pixels) needed to draw this symbol */
    public override int MinWidth {
        get { 
            if (smallsize)
                return SheetMusic.NoteWidth * 2;
            else
                return SheetMusic.NoteWidth * 3;
        }
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
        get { 
            if (clef == Clef.Treble && !smallsize)
                return SheetMusic.NoteHeight * 2;
            else
                return 0;
        }
    }

    /** Get the number of pixels this symbol extends below the staff. Used
     *  to determine the minimum height needed for the staff (Staff.FindBounds).
     */
    public override int BelowStaff {
        get {
            if (clef == Clef.Treble && !smallsize)
                return SheetMusic.NoteHeight * 2;
            else if (clef == Clef.Treble && smallsize)
                return SheetMusic.NoteHeight;
            else
                return 0;
        }
    }

    /** Draw the symbol.
     * @param ytop The ylocation (in pixels) where the top of the staff starts.
     */
    public override 
    void Draw(Graphics g, Pen pen, int ytop) {
        g.TranslateTransform(Width - MinWidth, 0);
        int y = ytop;
        Image image;
        int height;

        /* Get the image, height, and top y pixel, depending on the clef
         * and the image size.
         */
        if (clef == Clef.Treble) {
            image = treble;
            if (smallsize) {
                height = SheetMusic.StaffHeight + SheetMusic.StaffHeight/4;
            } else {
                height = 3 * SheetMusic.StaffHeight/2 + SheetMusic.NoteHeight/2;
                y = ytop - SheetMusic.NoteHeight;
            }
        }
        else {
            image = bass;
            if (smallsize) {
                height = SheetMusic.StaffHeight - 3*SheetMusic.NoteHeight/2;
            } else {
                height = SheetMusic.StaffHeight - SheetMusic.NoteHeight;
            }
        }

        /* Scale the image width to match the height */
        int imgwidth = image.Width * height / image.Height;
        g.DrawImage(image, 0, y, imgwidth, height);
        g.TranslateTransform(-(Width - MinWidth), 0);
    }

    public override string ToString() {
        return string.Format("ClefSymbol clef={0} small={1} width={2}",
                             clef, smallsize, width);
    }
}


}

