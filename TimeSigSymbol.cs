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

/** @class TimeSigSymbol
 * A TimeSigSymbol represents the time signature at the beginning
 * of the staff. We use pre-made images for the numbers, instead of
 * drawing strings.
 */

public class TimeSigSymbol : MusicSymbol {
    private static Image[] images;  /** The images for each number */
    private int  numerator;         /** The numerator */
    private int  denominator;       /** The denominator */
    private int  width;             /** The width in pixels */
    private bool candraw;           /** True if we can draw the time signature */

    /** Create a new TimeSigSymbol */
    public TimeSigSymbol(int numer, int denom) {
        numerator = numer;
        denominator = denom;
        LoadImages();
        if (numer >= 0 && numer < images.Length && images[numer] != null &&
            denom >= 0 && denom < images.Length && images[numer] != null) {
            candraw = true;
        }
        else {
            candraw = false;
        }
        width = MinWidth;
    }

    /** Load the images into memory. */
    private static void LoadImages() {
        if (images == null) {
            images = new Image[13];
            for (int i = 0; i < 13; i++) {
                images[i] = null;
            }
            images[2] = new Bitmap(typeof(TimeSigSymbol), "two.png");
            images[3] = new Bitmap(typeof(TimeSigSymbol), "three.png");
            images[4] = new Bitmap(typeof(TimeSigSymbol), "four.png");
            images[6] = new Bitmap(typeof(TimeSigSymbol), "six.png");
            images[8] = new Bitmap(typeof(TimeSigSymbol), "eight.png");
            images[9] = new Bitmap(typeof(TimeSigSymbol), "nine.png");
            images[12] = new Bitmap(typeof(TimeSigSymbol), "twelve.png");
        }
    }

    /** Get the time (in pulses) this symbol occurs at. */
    public override int StartTime {
        get { return -1; }
    }

    /** Get the minimum width (in pixels) needed to draw this symbol */
    public override int MinWidth {
        get { if (candraw) 
                  return images[2].Width * SheetMusic.NoteHeight * 2 /images[2].Height;
              else
                  return 0;
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
        get {  return 0; }
    }

    /** Get the number of pixels this symbol extends below the staff. Used
     *  to determine the minimum height needed for the staff (Staff.FindBounds).
     */
    public override int BelowStaff {
        get { return 0; } 
    }

    /** Draw the symbol.
     * @param ytop The ylocation (in pixels) where the top of the staff starts.
     */
    public override 
    void Draw(Graphics g, Pen pen, int ytop) {
        if (!candraw)
            return;

        g.TranslateTransform(Width - MinWidth, 0);
        Image numer = images[numerator];
        Image denom = images[denominator];

        /* Scale the image width to match the height */
        int imgheight = SheetMusic.NoteHeight * 2;
        int imgwidth = numer.Width * imgheight / numer.Height;
        g.DrawImage(numer, 0, ytop, imgwidth, imgheight);
        g.DrawImage(denom, 0, ytop + SheetMusic.NoteHeight*2, imgwidth, imgheight);
        g.TranslateTransform(-(Width - MinWidth), 0);
    }

    public override string ToString() {
        return string.Format("TimeSigSymbol numerator={0} denominator={1}",
                             numerator, denominator);
    }
}

}

