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

/** @class LyricSymbol
 *  A lyric contains the lyric to display, the start time the lyric occurs at,
 *  the the x-coordinate where it will be displayed.
 */
public class LyricSymbol {
    private int starttime;   /** The start time, in pulses */
    private string text;     /** The lyric text */
    private int x;           /** The x (horizontal) position within the staff */

    public LyricSymbol(int starttime, string text) {
        this.starttime = starttime; 
        this.text = text;
    }
     
    public int StartTime {
        get { return starttime; }
        set { starttime = value; }
    }

    public string Text {
        get { return text; }
        set { text = value; }
    }

    public int X {
        get { return x; }
        set { x = value; }
    }

    public int MinWidth {
        get { return minWidth(); }
    }

    /* Return the minimum width in pixels needed to display this lyric.
     * This is an estimation, not exact.
     */
    private int minWidth() { 
        float widthPerChar = SheetMusic.LetterFont.GetHeight() * 2.0f/3.0f;
        float width = text.Length * widthPerChar;
        if (text.IndexOf("i") >= 0) {
            width -= widthPerChar/2.0f;
        }
        if (text.IndexOf("j") >= 0) {
            width -= widthPerChar/2.0f;
        }
        if (text.IndexOf("l") >= 0) {
            width -= widthPerChar/2.0f;
        }
        return (int)width;
    }

    public override 
    string ToString() {
        return string.Format("Lyric start={0} x={1} text={2}",
                             starttime, x, text);
    }

}


}
