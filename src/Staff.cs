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
using System.Drawing.Printing;

namespace MidiSheetMusic {

/* @class Staff
 * The Staff is used to draw a single Staff (a row of measures) in the 
 * SheetMusic Control. A Staff needs to draw
 * - The Clef
 * - The key signature
 * - The horizontal lines
 * - A list of MusicSymbols
 * - The left and right vertical lines
 *
 * The height of the Staff is determined by the number of pixels each
 * MusicSymbol extends above and below the staff.
 *
 * The vertical lines (left and right sides) of the staff are joined
 * with the staffs above and below it, with one exception.  
 * The last track is not joined with the first track.
 */

public class Staff {
    private List<MusicSymbol> symbols;  /** The music symbols in this staff */
    private List<LyricSymbol> lyrics;   /** The lyrics to display (can be null) */
    private int ytop;                   /** The y pixel of the top of the staff */
    private ClefSymbol clefsym;         /** The left-side Clef symbol */
    private AccidSymbol[] keys;         /** The key signature symbols */
    private bool showMeasures;          /** If true, show the measure numbers */
    private int keysigWidth;            /** The width of the clef and key signature */
    private int width;                  /** The width of the staff in pixels */
    private int height;                 /** The height of the staff in pixels */
    private int tracknum;               /** The track this staff represents */
    private int totaltracks;            /** The total number of tracks */
    private int starttime;              /** The time (in pulses) of first symbol */
    private int endtime;                /** The time (in pulses) of last symbol */
    private int measureLength;          /** The time (in pulses) of a measure */

    /** Create a new staff with the given list of music symbols,
     * and the given key signature.  The clef is determined by
     * the clef of the first chord symbol. The track number is used
     * to determine whether to join this left/right vertical sides
     * with the staffs above and below. The SheetMusicOptions are used
     * to check whether to display measure numbers or not.
     */
    public Staff(List<MusicSymbol> symbols, KeySignature key, 
                 MidiOptions options,
                 int tracknum, int totaltracks)  {

        keysigWidth = SheetMusic.KeySignatureWidth(key);
        this.tracknum = tracknum;
        this.totaltracks = totaltracks;
        showMeasures = (options.showMeasures && tracknum == 0);
        measureLength = options.time.Measure;
        Clef clef = FindClef(symbols);

        clefsym = new ClefSymbol(clef, 0, false);
        keys = key.GetSymbols(clef);
        this.symbols = symbols;
        CalculateWidth(options.scrollVert);
        CalculateHeight();
        CalculateStartEndTime();
        FullJustify();
    }

    /** Return the width of the staff */
    public int Width {
        get { return width; }
    }

    /** Return the height of the staff */
    public int Height {
        get { return height; }
    }

    /** Return the track number of this staff (starting from 0 */
    public int Track {
        get { return tracknum; }
    }

    /** Return the starting time of the staff, the start time of
     *  the first symbol.  This is used during playback, to 
     *  automatically scroll the music while playing.
     */
    public int StartTime {
        get { return starttime; }
    }

    /** Return the ending time of the staff, the endtime of
     *  the last symbol.  This is used during playback, to 
     *  automatically scroll the music while playing.
     */
    public int EndTime {
        get { return endtime; }
        set { endtime = value; }
    }

    /** Find the initial clef to use for this staff.  Use the clef of
     * the first ChordSymbol.
     */
    private Clef FindClef(List<MusicSymbol> list) {
        foreach (MusicSymbol m in list) {
            if (m is ChordSymbol) {
                ChordSymbol c = (ChordSymbol) m;
                return c.Clef;
            }
        }
        return Clef.Treble;
    }

    /** Calculate the height of this staff.  Each MusicSymbol contains the
     * number of pixels it needs above and below the staff.  Get the maximum
     * values above and below the staff.
     */
    public void CalculateHeight() {
        int above = 0;
        int below = 0;

        foreach (MusicSymbol s in symbols) {
            above = Math.Max(above, s.AboveStaff);
            below = Math.Max(below, s.BelowStaff);
        }
        above = Math.Max(above, clefsym.AboveStaff);
        below = Math.Max(below, clefsym.BelowStaff);
        ytop = above + SheetMusic.NoteHeight;
        height = SheetMusic.NoteHeight*5 + ytop + below;
        if (showMeasures || lyrics != null) {
            height += SheetMusic.NoteHeight * 3/2;
        }

        /* Add some extra vertical space between the last track
         * and first track.
         */
        if (tracknum == totaltracks-1)
            height += SheetMusic.NoteHeight * 3;
    }

    /** Calculate the width of this staff */
    private void CalculateWidth(bool scrollVert) {
        if (scrollVert) {
            width = SheetMusic.PageWidth;
            return;
        }
        width = keysigWidth;
        foreach (MusicSymbol s in symbols) {
            width += s.Width;
        }
    }


    /** Calculate the start and end time of this staff. */
    private void CalculateStartEndTime() {
        starttime = endtime = 0;
        if (symbols.Count == 0) {
            return;
        }
        starttime = symbols[0].StartTime;
        foreach (MusicSymbol m in symbols) {
            if (endtime < m.StartTime) {
                endtime = m.StartTime;
            }
            if (m is ChordSymbol) {
                ChordSymbol c = (ChordSymbol) m;
                if (endtime < c.EndTime) {
                    endtime = c.EndTime;
                }
            }
        }
    }


    /** Full-Justify the symbols, so that they expand to fill the whole staff. */
    private void FullJustify() {
        if (width != SheetMusic.PageWidth)
            return;

        int totalwidth = keysigWidth;
        int totalsymbols = 0;
        int i = 0;

        while (i < symbols.Count) {
            int start = symbols[i].StartTime;
            totalsymbols++;
            totalwidth += symbols[i].Width;
            i++;
            while (i < symbols.Count && symbols[i].StartTime == start) {
                totalwidth += symbols[i].Width;
                i++;
            }
        }

        int extrawidth = (SheetMusic.PageWidth - totalwidth - 1) / totalsymbols;
        if (extrawidth > SheetMusic.NoteHeight*2) {
            extrawidth = SheetMusic.NoteHeight*2;
        }
        i = 0;
        while (i < symbols.Count) {
            int start = symbols[i].StartTime;
            symbols[i].Width += extrawidth;
            i++;
            while (i < symbols.Count && symbols[i].StartTime == start) {
                i++;
            }
        }
    }


    /** Add the lyric symbols that occur within this staff.
     *  Set the x-position of the lyric symbol. 
     */
    public void AddLyrics(List<LyricSymbol> tracklyrics) {
        if (tracklyrics == null) {
            return;
        }
        lyrics = new List<LyricSymbol>();
        int xpos = 0;
        int symbolindex = 0;
        foreach (LyricSymbol lyric in tracklyrics) {
            if (lyric.StartTime < starttime) {
                continue;
            }
            if (lyric.StartTime > endtime) {
                break;
            }
            /* Get the x-position of this lyric */
            while (symbolindex < symbols.Count && 
                   symbols[symbolindex].StartTime < lyric.StartTime) {
                xpos += symbols[symbolindex].Width;
                symbolindex++;
            }
            lyric.X = xpos;
            if (symbolindex < symbols.Count &&
                (symbols[symbolindex] is BarSymbol)) {
                lyric.X += SheetMusic.NoteWidth;
            }
            lyrics.Add(lyric); 
        }
        if (lyrics.Count == 0) {
            lyrics = null;
        }
    }


    /** Draw the lyrics */
    private void DrawLyrics(Graphics g, Pen pen) {
        /* Skip the left side Clef symbol and key signature */
        int xpos = keysigWidth;
        int ypos = height - SheetMusic.NoteHeight * 2;

        foreach (LyricSymbol lyric in lyrics) {
            g.DrawString(lyric.Text,
                         SheetMusic.LetterFont,
                         Brushes.Black, 
                         xpos + lyric.X, ypos);
        }
    }

    /** Draw the measure numbers for each measure */
    private void DrawMeasureNumbers(Graphics g, Pen pen) {

        /* Skip the left side Clef symbol and key signature */
        int xpos = keysigWidth;
        int ypos = height - SheetMusic.NoteHeight * 3/2;

        foreach (MusicSymbol s in symbols) {
            if (s is BarSymbol) {
                int measure = 1 + s.StartTime / measureLength;
                g.DrawString("" + measure, 
                             SheetMusic.LetterFont,
                             Brushes.Black, 
                             xpos + SheetMusic.NoteWidth, 
                             ypos);
            }
            xpos += s.Width;
        }
    }

    /** Draw the lyrics */


    /** Draw the five horizontal lines of the staff */
    private void DrawHorizLines(Graphics g, Pen pen) {
        int line = 1;
        int y = ytop - SheetMusic.LineWidth;
        pen.Width = 1;
        for (line = 1; line <= 5; line++) {
            g.DrawLine(pen, SheetMusic.LeftMargin, y, 
                            width-1, y);
            y += SheetMusic.LineWidth + SheetMusic.LineSpace;
        }
        pen.Color = Color.Black;

    }

    /** Draw the vertical lines at the far left and far right sides. */
    private void DrawEndLines(Graphics g, Pen pen) {
        pen.Width = 1;

        /* Draw the vertical lines from 0 to the height of this staff,
         * including the space above and below the staff, with two exceptions:
         * - If this is the first track, don't start above the staff.
         *   Start exactly at the top of the staff (ytop - LineWidth)
         * - If this is the last track, don't end below the staff.
         *   End exactly at the bottom of the staff.
         */
        int ystart, yend;
        if (tracknum == 0)
            ystart = ytop - SheetMusic.LineWidth;
        else
            ystart = 0;

        if (tracknum == (totaltracks-1))
            yend = ytop + 4 * SheetMusic.NoteHeight;
        else
            yend = height;

        g.DrawLine(pen, SheetMusic.LeftMargin, ystart,
                        SheetMusic.LeftMargin, yend);

        g.DrawLine(pen, width-1, ystart, width-1, yend);

    }

    /** Draw this staff. Only draw the symbols inside the clip area */
    public void Draw(Graphics g, Rectangle clip, Pen pen) {
        int xpos = SheetMusic.LeftMargin + 5;

        /* Draw the left side Clef symbol */
        g.TranslateTransform(xpos, 0);
        clefsym.Draw(g, pen, ytop);
        g.TranslateTransform(-xpos, 0);
        xpos += clefsym.Width;

        /* Draw the key signature */
        foreach (AccidSymbol a in keys) {
            g.TranslateTransform(xpos, 0);
            a.Draw(g, pen, ytop);
            g.TranslateTransform(-xpos, 0);
            xpos += a.Width;
        }
       
        /* Draw the actual notes, rests, bars.  Draw the symbols one 
         * after another, using the symbol width to determine the
         * x position of the next symbol.
         *
         * For fast performance, only draw symbols that are in the clip area.
         */
        foreach (MusicSymbol s in symbols) {
            if ((xpos <= clip.X + clip.Width + 50) && (xpos + s.Width + 50 >= clip.X)) {
                g.TranslateTransform(xpos, 0);
                s.Draw(g, pen, ytop);
                g.TranslateTransform(-xpos, 0);
            }
            xpos += s.Width;
        }
        DrawHorizLines(g, pen);
        DrawEndLines(g, pen);

        if (showMeasures) {
            DrawMeasureNumbers(g, pen);
        }
        if (lyrics != null) {
            DrawLyrics(g, pen);
        }

    }


    /** Shade all the chords played in the given time.
     *  Un-shade any chords shaded in the previous pulse time.
     *  Store the x coordinate location where the shade was drawn.
     */
    public void ShadeNotes(Graphics g, SolidBrush shadeBrush, Pen pen,
                           int currentPulseTime, int prevPulseTime, ref int x_shade) {

        /* If there's nothing to unshade, or shade, return */
        if ((starttime > prevPulseTime || endtime < prevPulseTime) &&
            (starttime > currentPulseTime || endtime < currentPulseTime)) {
            return;
        }

        /* Skip the left side Clef symbol and key signature */
        int xpos = keysigWidth;

        MusicSymbol curr = null;
        ChordSymbol prevChord = null;
        int prev_xpos = 0;

        /* Loop through the symbols. 
         * Unshade symbols where start <= prevPulseTime < end
         * Shade symbols where start <= currentPulseTime < end
         */ 
        for (int i = 0; i < symbols.Count; i++) {
            curr = symbols[i];
            if (curr is BarSymbol) {
                xpos += curr.Width;
                continue;
            }

            int start = curr.StartTime;
            int end = 0;
            if (i+2 < symbols.Count && symbols[i+1] is BarSymbol) {
                end = symbols[i+2].StartTime;
            }
            else if (i+1 < symbols.Count) {
                end = symbols[i+1].StartTime;
            }
            else {
                end = endtime;
            }


            /* If we've past the previous and current times, we're done. */
            if ((start > prevPulseTime) && (start > currentPulseTime)) {
                if (x_shade == 0) {
                    x_shade = xpos;
                }

                return;
            }
            /* If shaded notes are the same, we're done */
            if ((start <= currentPulseTime) && (currentPulseTime < end) &&
                (start <= prevPulseTime) && (prevPulseTime < end)) {

                x_shade = xpos;
                return;
            }

            bool redrawLines = false;

            /* If symbol is in the previous time, draw a white background */
            if ((start <= prevPulseTime) && (prevPulseTime < end)) {
                g.TranslateTransform(xpos-2, -2);
                g.FillRectangle(Brushes.White, 0, 0, curr.Width+4, this.Height+4);
                g.TranslateTransform(-(xpos-2), 2);
                g.TranslateTransform(xpos, 0);
                curr.Draw(g, pen, ytop);
                g.TranslateTransform(-xpos, 0);

                redrawLines = true;
            }

            /* If symbol is in the current time, draw a shaded background */
            if ((start <= currentPulseTime) && (currentPulseTime < end)) {
                x_shade = xpos;
                g.TranslateTransform(xpos, 0);
                g.FillRectangle(shadeBrush, 0, 0, curr.Width, this.Height);
                curr.Draw(g, pen, ytop);
                g.TranslateTransform(-xpos, 0);
                redrawLines = true;
            }

            /* If either a gray or white background was drawn, we need to redraw
             * the horizontal staff lines, and redraw the stem of the previous chord.
             */
            if (redrawLines) {
                int line = 1;
                int y = ytop - SheetMusic.LineWidth;
                pen.Width = 1;
                g.TranslateTransform(xpos-2, 0);
                for (line = 1; line <= 5; line++) {
                    g.DrawLine(pen, 0, y, curr.Width+4, y);
                    y += SheetMusic.LineWidth + SheetMusic.LineSpace;
                }
                g.TranslateTransform(-(xpos-2), 0);

                if (prevChord != null) {
                    g.TranslateTransform(prev_xpos, 0);
                    prevChord.Draw(g, pen, ytop);
                    g.TranslateTransform(-prev_xpos, 0);
                }
                if (showMeasures) {
                    DrawMeasureNumbers(g, pen);
                }
                if (lyrics != null) {
                    DrawLyrics(g, pen);
                }
            }
            if (curr is ChordSymbol) {
                ChordSymbol chord = (ChordSymbol) curr;
                if (chord.Stem != null && !chord.Stem.Receiver) {
                    prevChord = (ChordSymbol) curr;
                    prev_xpos = xpos;
                }
            }
            xpos += curr.Width;
        }
    }

    public override string ToString() {
        string result = "Staff clef=" + clefsym.ToString() + "\n";
        result += "  Keys:\n";
        foreach (AccidSymbol a in keys) {
            result += "    " + a.ToString() + "\n";
        }
        result += "  Symbols:\n";
        foreach (MusicSymbol s in keys) {
            result += "    " + s.ToString() + "\n";
        }
        foreach (MusicSymbol m in symbols) {
            result += "    " + m.ToString() + "\n";
        }
        result += "End Staff\n";
        return result;
    }

}

}

