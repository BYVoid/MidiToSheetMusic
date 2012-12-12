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


/** @class BlankSymbol 
 * The Blank symbol is a music symbol that doesn't draw anything.  This
 * symbol is used for alignment purposes, to align notes in different 
 * staffs which occur at the same time.
 */
public class BlankSymbol : MusicSymbol {
    private int starttime; 
    private int width;

    /** Create a new BlankSymbol with the given starttime and width */
    public BlankSymbol(int starttime, int width) {
        this.starttime = starttime;
        this.width = width;
    }

    /** Get the time (in pulses) this symbol occurs at.
     * This is used to determine the measure this symbol belongs to.
     */
    public override int StartTime { 
        get { return starttime; }
    }

    /** Get the minimum width (in pixels) needed to draw this symbol */
    public override int MinWidth {
        get { return 0; }
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

    /** Draw nothing.
     * @param ytop The ylocation (in pixels) where the top of the staff starts.
     */
    public override void Draw(Graphics g, Pen pen, int ytop) {}

    public override string ToString() {
        return string.Format("BlankSymbol starttime={0} width={1}", 
                             starttime, width);
    }
}


}


