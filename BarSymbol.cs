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


/** @class BarSymbol
 * The BarSymbol represents the vertical bars which delimit measures.
 * The starttime of the symbol is the beginning of the new
 * measure.
 */
public class BarSymbol : MusicSymbol {
    private int starttime;
    private int width;

    /** Create a BarSymbol. The starttime should be the beginning of a measure. */
    public BarSymbol(int starttime) {
        this.starttime = starttime;
        width = MinWidth;
    }

    /** Get the time (in pulses) this symbol occurs at.
     * This is used to determine the measure this symbol belongs to.
     */
    public override int StartTime {
        get { return starttime; }
    }

    /** Get the minimum width (in pixels) needed to draw this symbol */
    public override int MinWidth { 
        get { return 2 * SheetMusic.LineSpace; }
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
        get { return 0; } 
    }

    /** Get the number of pixels this symbol extends below the staff. Used
     *  to determine the minimum height needed for the staff (Staff.FindBounds).
     */
    public override int BelowStaff { 
        get { return 0; }
    }

    /** Draw a vertical bar.
     * @param ytop The ylocation (in pixels) where the top of the staff starts.
     */
    public override 
    void Draw(Graphics g, Pen pen, int ytop) {
        int y = ytop;
        int yend = y + SheetMusic.LineSpace*4 + SheetMusic.LineWidth*4;
        pen.Width = 1;
        g.DrawLine(pen, SheetMusic.NoteWidth/2, y, 
                        SheetMusic.NoteWidth/2, yend);

    }

    public override string ToString() {
        return string.Format("BarSymbol starttime={0} width={1}", 
                             starttime, width);
    }
}


}

