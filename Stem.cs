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

/** @class Stem
 * The Stem class is used by ChordSymbol to draw the stem portion of
 * the chord.  The stem has the following fields:
 *
 * duration  - The duration of the stem.
 * direction - Either Up or Down
 * side      - Either left or right
 * top       - The topmost note in the chord
 * bottom    - The bottommost note in the chord
 * end       - The note position where the stem ends.  This is usually
 *             six notes past the last note in the chord.  For 8th/16th
 *             notes, the stem must extend even more.
 *
 * The SheetMusic class can change the direction of a stem after it
 * has been created.  The side and end fields may also change due to
 * the direction change.  But other fields will not change.
 */
 
public class Stem {
    public const int Up =   1;      /* The stem points up */
    public const int Down = 2;      /* The stem points down */
    public const int LeftSide = 1;  /* The stem is to the left of the note */
    public const int RightSide = 2; /* The stem is to the right of the note */

    private NoteDuration duration; /** Duration of the stem. */
    private int direction;         /** Up, Down, or None */
    private WhiteNote top;         /** Topmost note in chord */
    private WhiteNote bottom;      /** Bottommost note in chord */
    private WhiteNote end;         /** Location of end of the stem */
    private bool notesoverlap;     /** Do the chord notes overlap */
    private int side;              /** Left side or right side of note */

    private Stem pair;              /** If pair != null, this is a horizontal 
                                     * beam stem to another chord */
    private int width_to_pair;      /** The width (in pixels) to the chord pair */
    private bool receiver_in_pair;  /** This stem is the receiver of a horizontal
                                    * beam stem from another chord. */

    /** Get/Set the direction of the stem (Up or Down) */
    public int Direction {
        get { return direction; }
        set { ChangeDirection(value); }
    }

    /** Get the duration of the stem (Eigth, Sixteenth, ThirtySecond) */
    public NoteDuration Duration {
        get { return duration; }
    }

    /** Get the top note in the chord. This is needed to determine the stem direction */
    public WhiteNote Top {
        get { return top; }
    }

    /** Get the bottom note in the chord. This is needed to determine the stem direction */
    public WhiteNote Bottom {
        get { return bottom; }
    }

    /** Get/Set the location where the stem ends.  This is usually six notes
     * past the last note in the chord. See method CalculateEnd.
     */
    public WhiteNote End {
        get { return end; }
        set { end = value; }
    }

    /** Set this Stem to be the receiver of a horizontal beam, as part
     * of a chord pair.  In Draw(), if this stem is a receiver, we
     * don't draw a curvy stem, we only draw the vertical line.
     */
    public bool Receiver {
        get { return receiver_in_pair; }
        set { receiver_in_pair = value; }
    }

    /** Create a new stem.  The top note, bottom note, and direction are 
     * needed for drawing the vertical line of the stem.  The duration is 
     * needed to draw the tail of the stem.  The overlap boolean is true
     * if the notes in the chord overlap.  If the notes overlap, the
     * stem must be drawn on the right side.
     */
    public Stem(WhiteNote bottom, WhiteNote top, 
                NoteDuration duration, int direction, bool overlap) {

        this.top = top;
        this.bottom = bottom;
        this.duration = duration;
        this.direction = direction;
        this.notesoverlap = overlap;
        if (direction == Up || notesoverlap)
            side = RightSide;
        else 
            side = LeftSide;
        end = CalculateEnd();
        pair = null;
        width_to_pair = 0;
        receiver_in_pair = false;
    }

    /** Calculate the vertical position (white note key) where 
     * the stem ends 
     */
    public WhiteNote CalculateEnd() {
        if (direction == Up) {
            WhiteNote w = top;
            w = w.Add(6);
            if (duration == NoteDuration.Sixteenth) {
                w = w.Add(2);
            }
            else if (duration == NoteDuration.ThirtySecond) {
                w = w.Add(4);
            }
            return w;
        }
        else if (direction == Down) {
            WhiteNote w = bottom;
            w = w.Add(-6);
            if (duration == NoteDuration.Sixteenth) {
                w = w.Add(-2);
            }
            else if (duration == NoteDuration.ThirtySecond) {
                w = w.Add(-4);
            }
            return w;
        }
        else {
            return null;  /* Shouldn't happen */
        }
    }

    /** Change the direction of the stem.  This function is called by 
     * ChordSymbol.MakePair().  When two chords are joined by a horizontal
     * beam, their stems must point in the same direction (up or down).
     */
    public void ChangeDirection(int newdirection) {
        direction = newdirection;
        if (direction == Up || notesoverlap)
            side = RightSide;
        else
            side = LeftSide;
        end = CalculateEnd();
    }

    /** Pair this stem with another Chord.  Instead of drawing a curvy tail,
     * this stem will now have to draw a beam to the given stem pair.  The
     * width (in pixels) to this stem pair is passed as argument.
     */
    public void SetPair(Stem pair, int width_to_pair) {
        this.pair = pair;
        this.width_to_pair = width_to_pair;
    }

    /** Return true if this Stem is part of a horizontal beam. */
    public bool isBeam {
        get { return receiver_in_pair || (pair != null); }
    }

    /** Draw this stem.
     * @param ytop The y location (in pixels) where the top of the staff starts.
     * @param topstaff  The note at the top of the staff.
     */
    public void Draw(Graphics g, Pen pen, int ytop, WhiteNote topstaff) {
        if (duration == NoteDuration.Whole)
            return;

        DrawVerticalLine(g, pen, ytop, topstaff);
        if (duration == NoteDuration.Quarter || 
            duration == NoteDuration.DottedQuarter || 
            duration == NoteDuration.Half ||
            duration == NoteDuration.DottedHalf ||
            receiver_in_pair) {

            return;
        }

        if (pair != null)
            DrawHorizBarStem(g, pen, ytop, topstaff);
        else
            DrawCurvyStem(g, pen, ytop, topstaff);
    }

    /** Draw the vertical line of the stem 
     * @param ytop The y location (in pixels) where the top of the staff starts.
     * @param topstaff  The note at the top of the staff.
     */
    private void DrawVerticalLine(Graphics g, Pen pen, int ytop, WhiteNote topstaff) {
        int xstart;
        if (side == LeftSide)
            xstart = SheetMusic.LineSpace/4 + 1;
        else
            xstart = SheetMusic.LineSpace/4 + SheetMusic.NoteWidth;

        if (direction == Up) {
            int y1 = ytop + topstaff.Dist(bottom) * SheetMusic.NoteHeight/2 
                       + SheetMusic.NoteHeight/4;

            int ystem = ytop + topstaff.Dist(end) * SheetMusic.NoteHeight/2;

            g.DrawLine(pen, xstart, y1, xstart, ystem);
        }
        else if (direction == Down) {
            int y1 = ytop + topstaff.Dist(top) * SheetMusic.NoteHeight/2 
                       + SheetMusic.NoteHeight;

            if (side == LeftSide)
                y1 = y1 - SheetMusic.NoteHeight/4;
            else
                y1 = y1 - SheetMusic.NoteHeight/2;

            int ystem = ytop + topstaff.Dist(end) * SheetMusic.NoteHeight/2 
                          + SheetMusic.NoteHeight;

            g.DrawLine(pen, xstart, y1, xstart, ystem);
        }
    }

    /** Draw a curvy stem tail.  This is only used for single chords, not chord pairs.
     * @param ytop The y location (in pixels) where the top of the staff starts.
     * @param topstaff  The note at the top of the staff.
     */
    private void DrawCurvyStem(Graphics g, Pen pen, int ytop, WhiteNote topstaff) {

        pen.Width = 2;

        int xstart = 0;
        if (side == LeftSide)
            xstart = SheetMusic.LineSpace/4 + 1;
        else
            xstart = SheetMusic.LineSpace/4 + SheetMusic.NoteWidth;

        if (direction == Up) {
            int ystem = ytop + topstaff.Dist(end) * SheetMusic.NoteHeight/2;


            if (duration == NoteDuration.Eighth ||
                duration == NoteDuration.DottedEighth ||
                duration == NoteDuration.Triplet ||
                duration == NoteDuration.Sixteenth ||
                duration == NoteDuration.ThirtySecond) {

                g.DrawBezier(pen, 
                             xstart, ystem,
                             xstart, 
                             ystem + 3*SheetMusic.LineSpace/2,
                             xstart + SheetMusic.LineSpace*2, 
                             ystem + SheetMusic.NoteHeight*2,
                             xstart + SheetMusic.LineSpace/2, 
                             ystem + SheetMusic.NoteHeight*3);
            }
            ystem += SheetMusic.NoteHeight;

            if (duration == NoteDuration.Sixteenth ||
                duration == NoteDuration.ThirtySecond) {

                g.DrawBezier(pen, 
                             xstart, ystem,
                             xstart, 
                             ystem + 3*SheetMusic.LineSpace/2,
                             xstart + SheetMusic.LineSpace*2, 
                             ystem + SheetMusic.NoteHeight*2,
                             xstart + SheetMusic.LineSpace/2, 
                             ystem + SheetMusic.NoteHeight*3);
            }

            ystem += SheetMusic.NoteHeight;
            if (duration == NoteDuration.ThirtySecond) {
                g.DrawBezier(pen, 
                             xstart, ystem,
                             xstart, 
                             ystem + 3*SheetMusic.LineSpace/2,
                             xstart + SheetMusic.LineSpace*2, 
                             ystem + SheetMusic.NoteHeight*2,
                             xstart + SheetMusic.LineSpace/2, 
                             ystem + SheetMusic.NoteHeight*3);
            }

        }

        else if (direction == Down) {
            int ystem = ytop + topstaff.Dist(end)*SheetMusic.NoteHeight/2 +
                        SheetMusic.NoteHeight;

            if (duration == NoteDuration.Eighth ||
                duration == NoteDuration.DottedEighth ||
                duration == NoteDuration.Triplet ||
                duration == NoteDuration.Sixteenth ||
                duration == NoteDuration.ThirtySecond) {

                g.DrawBezier(pen, 
                             xstart, ystem,
                             xstart, 
                             ystem - SheetMusic.LineSpace,
                             xstart + SheetMusic.LineSpace*2, 
                             ystem - SheetMusic.NoteHeight*2,
                             xstart + SheetMusic.LineSpace, 
                             ystem - SheetMusic.NoteHeight*2 - 
                               SheetMusic.LineSpace/2);
            }
            ystem -= SheetMusic.NoteHeight;

            if (duration == NoteDuration.Sixteenth ||
                duration == NoteDuration.ThirtySecond) {

                g.DrawBezier(pen, 
                             xstart, ystem,
                             xstart, 
                             ystem - SheetMusic.LineSpace,
                             xstart + SheetMusic.LineSpace*2, 
                             ystem - SheetMusic.NoteHeight*2,
                             xstart + SheetMusic.LineSpace, 
                             ystem - SheetMusic.NoteHeight*2 - 
                               SheetMusic.LineSpace/2);
            }

            ystem -= SheetMusic.NoteHeight;
            if (duration == NoteDuration.ThirtySecond) {
                g.DrawBezier(pen, 
                             xstart, ystem,
                             xstart, 
                             ystem - SheetMusic.LineSpace,
                             xstart + SheetMusic.LineSpace*2, 
                             ystem - SheetMusic.NoteHeight*2,
                             xstart + SheetMusic.LineSpace, 
                             ystem - SheetMusic.NoteHeight*2 - 
                               SheetMusic.LineSpace/2);
            }

        }
        pen.Width = 1;

    }

    /* Draw a horizontal beam stem, connecting this stem with the Stem pair.
     * @param ytop The y location (in pixels) where the top of the staff starts.
     * @param topstaff  The note at the top of the staff.
     */
    private void DrawHorizBarStem(Graphics g, Pen pen, int ytop, WhiteNote topstaff) {
        pen.Width = SheetMusic.NoteHeight/2;

        int xstart = 0;
        int xstart2 = 0;

        if (side == LeftSide)
            xstart = SheetMusic.LineSpace/4 + 1;
        else if (side == RightSide)
            xstart = SheetMusic.LineSpace/4 + SheetMusic.NoteWidth;

        if (pair.side == LeftSide)
            xstart2 = SheetMusic.LineSpace/4 + 1;
        else if (pair.side == RightSide)
            xstart2 = SheetMusic.LineSpace/4 + SheetMusic.NoteWidth;


        if (direction == Up) {
            int xend = width_to_pair + xstart2;
            int ystart = ytop + topstaff.Dist(end) * SheetMusic.NoteHeight/2;
            int yend = ytop + topstaff.Dist(pair.end) * SheetMusic.NoteHeight/2;

            if (duration == NoteDuration.Eighth ||
                duration == NoteDuration.DottedEighth || 
                duration == NoteDuration.Triplet || 
                duration == NoteDuration.Sixteenth ||
                duration == NoteDuration.ThirtySecond) {

                g.DrawLine(pen, xstart, ystart, xend, yend);
            }
            ystart += SheetMusic.NoteHeight;
            yend += SheetMusic.NoteHeight;

            /* A dotted eighth will connect to a 16th note. */
            if (duration == NoteDuration.DottedEighth) {
                int x = xend - SheetMusic.NoteHeight;
                double slope = (yend - ystart) * 1.0 / (xend - xstart);
                int y = (int)(slope * (x - xend) + yend); 

                g.DrawLine(pen, x, y, xend, yend);
            }

            if (duration == NoteDuration.Sixteenth ||
                duration == NoteDuration.ThirtySecond) {

                g.DrawLine(pen, xstart, ystart, xend, yend);
            }
            ystart += SheetMusic.NoteHeight;
            yend += SheetMusic.NoteHeight;
            
            if (duration == NoteDuration.ThirtySecond) {
                g.DrawLine(pen, xstart, ystart, xend, yend);
            }
        }

        else {
            int xend = width_to_pair + xstart2;
            int ystart = ytop + topstaff.Dist(end) * SheetMusic.NoteHeight/2 + 
                         SheetMusic.NoteHeight;
            int yend = ytop + topstaff.Dist(pair.end) * SheetMusic.NoteHeight/2 
                         + SheetMusic.NoteHeight;

            if (duration == NoteDuration.Eighth ||
                duration == NoteDuration.DottedEighth ||
                duration == NoteDuration.Triplet ||
                duration == NoteDuration.Sixteenth ||
                duration == NoteDuration.ThirtySecond) {

                g.DrawLine(pen, xstart, ystart, xend, yend);
            }
            ystart -= SheetMusic.NoteHeight;
            yend -= SheetMusic.NoteHeight;

            /* A dotted eighth will connect to a 16th note. */
            if (duration == NoteDuration.DottedEighth) {
                int x = xend - SheetMusic.NoteHeight;
                double slope = (yend - ystart) * 1.0 / (xend - xstart);
                int y = (int)(slope * (x - xend) + yend); 

                g.DrawLine(pen, x, y, xend, yend);
            }

            if (duration == NoteDuration.Sixteenth ||
                duration == NoteDuration.ThirtySecond) {

                g.DrawLine(pen, xstart, ystart, xend, yend);
            }
            ystart -= SheetMusic.NoteHeight;
            yend -= SheetMusic.NoteHeight;
            
            if (duration == NoteDuration.ThirtySecond) {
                g.DrawLine(pen, xstart, ystart, xend, yend);
            }
        }
        pen.Width = 1;
    }

    public override string ToString() {
        return string.Format("Stem duration={0} direction={1} top={2} bottom={3} end={4}" +
                             " overlap={5} side={6} width_to_pair={7} receiver_in_pair={8}",
                             duration, direction, top.ToString(), bottom.ToString(),
                             end.ToString(), notesoverlap, side, width_to_pair, receiver_in_pair);
    }

} 


}

