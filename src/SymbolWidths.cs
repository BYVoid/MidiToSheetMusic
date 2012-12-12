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

namespace MidiSheetMusic {

/** @class SymbolWidths
 * The SymbolWidths class is used to vertically align notes in different
 * tracks that occur at the same time (that have the same starttime).
 * This is done by the following:
 * - Store a list of all the start times.
 * - Store the width of symbols for each start time, for each track.
 * - Store the maximum width for each start time, across all tracks.
 * - Get the extra width needed for each track to match the maximum
 *   width for that start time.
 *
 * See method SheetMusic.AlignSymbols(), which uses this class.
 */

public class SymbolWidths {

    /** Array of maps (starttime -> symbol width), one per track */
    private Dictionary<int, int>[] widths;

    /** Map of starttime -> maximum symbol width */
    private Dictionary<int, int> maxwidths;

    /** An array of all the starttimes, in all tracks */
    private int[] starttimes;


    /** Initialize the symbol width maps, given all the symbols in
     * all the tracks, plus the lyrics in all tracks.
     */
    public SymbolWidths(List<MusicSymbol>[] tracks,
                        List<LyricSymbol>[] tracklyrics) {

        /* Get the symbol widths for all the tracks */
        widths = new Dictionary<int,int>[ tracks.Length ];
        for (int track = 0; track < tracks.Length; track++) {
            widths[track] = GetTrackWidths(tracks[track]);
        }
        maxwidths = new Dictionary<int,int>();

        /* Calculate the maximum symbol widths */
        foreach (Dictionary<int,int> dict in widths) {
            foreach (int time in dict.Keys) {
                if (!maxwidths.ContainsKey(time) ||
                    (maxwidths[time] < dict[time]) ) {

                    maxwidths[time] = dict[time];
                }
            }
        }

        if (tracklyrics != null) {
            foreach (List<LyricSymbol> lyrics in tracklyrics) {
                if (lyrics == null) {
                    continue;
                }
                foreach (LyricSymbol lyric in lyrics) {
                    int width = lyric.MinWidth;
                    int time = lyric.StartTime;
                    if (!maxwidths.ContainsKey(time) ||
                        (maxwidths[time] < width) ) {

                        maxwidths[time] = width;
                    }
                }
            }
        }

        /* Store all the start times to the starttime array */
        starttimes = new int[ maxwidths.Keys.Count ];
        maxwidths.Keys.CopyTo(starttimes, 0);
        Array.Sort<int>(starttimes);
    }

    /** Create a table of the symbol widths for each starttime in the track. */
    private static Dictionary<int,int> GetTrackWidths(List<MusicSymbol> symbols) {
        Dictionary<int,int> widths = new Dictionary<int,int>();

        foreach (MusicSymbol m in symbols) {
            int start = m.StartTime;
            int w = m.MinWidth;

            if (m is BarSymbol) {
                continue;
            }
            else if (widths.ContainsKey(start)) {
                widths[start] += w;
            }
            else {
                widths[start] = w;
            }
        }
        return widths;
    }

    /** Given a track and a start time, return the extra width needed so that
     * the symbols for that start time align with the other tracks.
     */
    public int GetExtraWidth(int track, int start) {
        if (!widths[track].ContainsKey(start)) {
            return maxwidths[start];
        } else {
            return maxwidths[start] - widths[track][start];
        }
    }

    /** Return an array of all the start times in all the tracks */
    public int[] StartTimes {
        get { return starttimes; }
    }




}


}
