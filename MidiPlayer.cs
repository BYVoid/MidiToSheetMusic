/*
 * Copyright (c) 2011-2012 Madhav Vaidyanathan
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
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace MidiSheetMusic {

/** @class MidiPlayer
 *
 * The MidiPlayer is the panel at the top used to play the sound
 * of the midi file.  It consists of:
 *
 * - The Rewind button
 * - The Play/Pause button
 * - The Stop button
 * - The Fast Forward button
 * - The Playback speed bar
 * - The Volume bar
 *
 * The sound of the midi file depends on
 * - The MidiOptions (taken from the menus)
 *   Which tracks are selected
 *   How much to transpose the keys by
 *   What instruments to use per track
 * - The tempo (from the Speed bar)
 * - The volume
 *
 * The MidiFile.ChangeSound() method is used to create a new midi file
 * with these options.  The mciSendString() function is used for 
 * playing, pausing, and stopping the sound.
 *
 * For shading the notes during playback, the method
 * SheetMusic.ShadeNotes() is used.  It takes the current 'pulse time',
 * and determines which notes to shade.
 */
public class MidiPlayer : Panel  {
    private Image rewindImage;   /** The rewind image */
    private Image playImage;     /** The play image */
    private Image pauseImage;    /** The pause image */
    private Image stopImage;     /** The stop image */
    private Image fastFwdImage;  /** The fast forward image */
    private Image volumeImage;   /** The volume image */

    private Button rewindButton; /** The rewind button */
    private Button playButton;   /** The play/pause button */
    private Button stopButton;   /** The stop button */
    private Button fastFwdButton;/** The fast forward button */
    private TrackBar speedBar;   /** The trackbar for controlling the playback speed */
    private TrackBar volumeBar;  /** The trackbar for controlling the volume */
    private ToolTip playTip;     /** The tooltip for the play button */

    int playstate;               /** The playing state of the Midi Player */
    const int stopped   = 1;     /** Currently stopped */
    const int playing   = 2;     /** Currently playing music */
    const int paused    = 3;     /** Currently paused */
    const int initStop  = 4;     /** Transitioning from playing to stop */
    const int initPause = 5;     /** Transitioning from playing to pause */

    MidiFile midifile;          /** The midi file to play */
    MidiOptions options;   /** The sound options for playing the midi file */
    string tempSoundFile;       /** The temporary midi filename currently being played */
    double pulsesPerMsec;       /** The number of pulses per millisec */
    SheetMusic sheet;           /** The sheet music to shade while playing */
    Piano piano;                /** The piano to shade while playing */
    Timer timer;                /** Timer used to update the sheet music while playing */
    TimeSpan startTime;         /** Absolute time when music started playing */
    double startPulseTime;      /** Time (in pulses) when music started playing */
    double currentPulseTime;    /** Time (in pulses) music is currently at */
    double prevPulseTime;       /** Time (in pulses) music was last at */
    StringBuilder errormsg;     /** Error messages from midi player */
    Process timidity;           /** The Linux timidity process */

    [DllImport("winmm.dll")]
    public static extern int mciSendString(string lpstrCommand,
                                           string lpstrReturnString,
                                           int uReturnLength,
                                           int dwCallback);

    [DllImport("winmm.dll")]
    public static extern int mciGetErrorString(int errcode, 
                                               StringBuilder msg, uint buflen);


    /** Load the play/pause/stop button images */
    private void loadButtonImages() {
        int buttonheight = this.Font.Height * 2;
        Size imagesize = new Size(buttonheight, buttonheight);
        rewindImage = new Bitmap(typeof(MidiPlayer), "rewind.png");
        rewindImage = new Bitmap(rewindImage, imagesize);
        playImage = new Bitmap(typeof(MidiPlayer), "play.png");
        playImage = new Bitmap(playImage, imagesize);
        pauseImage = new Bitmap(typeof(MidiPlayer), "pause.png");
        pauseImage = new Bitmap(pauseImage, imagesize);
        stopImage = new Bitmap(typeof(MidiPlayer), "stop.png");
        stopImage = new Bitmap(stopImage, imagesize);
        fastFwdImage = new Bitmap(typeof(MidiPlayer), "fastforward.png");
        fastFwdImage = new Bitmap(fastFwdImage, imagesize);
        volumeImage = new Bitmap(typeof(MidiPlayer), "volume.png");
        volumeImage = new Bitmap(volumeImage, imagesize);
    }

    /** Create a new MidiPlayer, displaying the play/stop buttons, the
     *  speed bar, and volume bar.  The midifile and sheetmusic are initially null.
     */
    public MidiPlayer() {
        this.Font = new Font("Arial", 10, FontStyle.Bold);
        loadButtonImages();
        int buttonheight = this.Font.Height * 2;

        this.midifile = null;
        this.options = null;
        this.sheet = null;
        playstate = stopped;
        startTime = DateTime.Now.TimeOfDay;
        startPulseTime = 0;
        currentPulseTime = 0;
        prevPulseTime = -10;
        errormsg = new StringBuilder(256);
        ToolTip tip;

        /* Create the rewind button */
        rewindButton = new Button();
        rewindButton.Parent = this;
        rewindButton.Image = rewindImage;
        rewindButton.ImageAlign = ContentAlignment.MiddleCenter;
        rewindButton.Size = new Size(buttonheight, buttonheight);
        rewindButton.Location = new Point(buttonheight/2, buttonheight/2);
        rewindButton.Click += new EventHandler(Rewind);
        tip = new ToolTip();
        tip.SetToolTip(rewindButton, "Rewind");

        /* Create the play button */
        playButton = new Button();
        playButton.Parent = this;
        playButton.Image = playImage;
        playButton.ImageAlign = ContentAlignment.MiddleCenter;
        playButton.Size = new Size(buttonheight, buttonheight);
        playButton.Location = new Point(buttonheight/2, buttonheight/2);
        playButton.Location = new Point(rewindButton.Location.X + rewindButton.Width + buttonheight/2,
                                        rewindButton.Location.Y);
        playButton.Click += new EventHandler(PlayPause);
        playTip = new ToolTip();
        playTip.SetToolTip(playButton, "Play");

        /* Create the stop button */
        stopButton = new Button();
        stopButton.Parent = this;
        stopButton.Image = stopImage;
        stopButton.ImageAlign = ContentAlignment.MiddleCenter;
        stopButton.Size = new Size(buttonheight, buttonheight);
        stopButton.Location = new Point(playButton.Location.X + playButton.Width + buttonheight/2,
                                        playButton.Location.Y);
        stopButton.Click += new EventHandler(Stop);
        tip = new ToolTip();
        tip.SetToolTip(stopButton, "Stop");

        /* Create the fastFwd button */        
        fastFwdButton = new Button();
        fastFwdButton.Parent = this;
        fastFwdButton.Image = fastFwdImage;
        fastFwdButton.ImageAlign = ContentAlignment.MiddleCenter;
        fastFwdButton.Size = new Size(buttonheight, buttonheight);
        fastFwdButton.Location = new Point(stopButton.Location.X + stopButton.Width + buttonheight/2,                      
                                          stopButton.Location.Y);
        fastFwdButton.Click += new EventHandler(FastForward);
        tip = new ToolTip();
        tip.SetToolTip(fastFwdButton, "Fast Forward");



        /* Create the Speed bar */
        Label speedLabel = new Label();
        speedLabel.Parent = this;
        speedLabel.Text = "Speed: ";
        speedLabel.TextAlign = ContentAlignment.MiddleRight;
        speedLabel.Height = buttonheight;
        speedLabel.Width = buttonheight*2;
        speedLabel.Location = new Point(fastFwdButton.Location.X + fastFwdButton.Width + buttonheight/2,
                                        fastFwdButton.Location.Y);

        speedBar = new TrackBar();
        speedBar.Parent = this;
        speedBar.Minimum = 1;
        speedBar.Maximum = 100;
        speedBar.TickFrequency = 10;
        speedBar.TickStyle = TickStyle.BottomRight;
        speedBar.LargeChange = 10;
        speedBar.Value = 100;
        speedBar.Width = buttonheight * 5;
        speedBar.Location = new Point(speedLabel.Location.X + speedLabel.Width + 2,
                                      speedLabel.Location.Y);
        tip = new ToolTip();
        tip.SetToolTip(speedBar, "Adjust the speed");

        /* Create the Volume bar */
        Label volumeLabel = new Label();
        volumeLabel.Parent = this;
        volumeLabel.Image = volumeImage;
        volumeLabel.ImageAlign = ContentAlignment.MiddleRight;
        volumeLabel.Height = buttonheight;
        volumeLabel.Width = buttonheight*2;
        volumeLabel.Location = new Point(speedBar.Location.X + speedBar.Width + buttonheight/2,
                                         speedBar.Location.Y);

        volumeBar = new TrackBar();
        volumeBar.Parent = this;
        volumeBar.Minimum = 1;
        volumeBar.Maximum = 100;
        volumeBar.TickFrequency = 10;
        volumeBar.TickStyle = TickStyle.BottomRight;
        volumeBar.LargeChange = 10;
        volumeBar.Value = 100;
        volumeBar.Width = buttonheight * 5;
        volumeBar.Location = new Point(volumeLabel.Location.X + volumeLabel.Width + 2,
                                       volumeLabel.Location.Y);
        volumeBar.Scroll += new EventHandler(ChangeVolume);
        tip = new ToolTip();
        tip.SetToolTip(volumeBar, "Adjust the volume");

        Height = buttonheight*2;

        /* Initialize the timer used for playback, but don't start
         * the timer yet (enabled = false).
         */
        timer = new Timer();
        timer.Enabled = false;
        timer.Interval = 100;  /* 100 millisec */
        timer.Tick += new EventHandler(TimerCallback);

        tempSoundFile = "";
    }

    public void SetPiano(Piano p) {
        piano = p;
    }

    /** The MidiFile and/or SheetMusic has changed. Stop any playback sound,
     *  and store the current midifile and sheet music.
     */
    public void SetMidiFile(MidiFile file, MidiOptions opt, SheetMusic s) {

        /* If we're paused, and using the same midi file, redraw the
         * highlighted notes.
         */
        if ((file == midifile && midifile != null && playstate == paused)) {
            options = opt;
            sheet = s;
            sheet.ShadeNotes((int)currentPulseTime, (int)-10, false);

            /* We have to wait some time (200 msec) for the sheet music
             * to scroll and redraw, before we can re-shade.
             */
            Timer redrawTimer = new Timer();
            redrawTimer.Interval = 200;
            redrawTimer.Tick += new EventHandler(ReShade);
            redrawTimer.Enabled = true;
            redrawTimer.Start();
        }
        else {
            this.Stop(null, null);
            midifile = file;
            options = opt;
            sheet = s;
        }
        this.DeleteSoundFile();
    }

    /** If we're paused, reshade the sheet music and piano. */
    private void ReShade(object sender, EventArgs args) {
        if (playstate == paused) {
            sheet.ShadeNotes((int)currentPulseTime, (int)-10, false);
            piano.ShadeNotes((int)currentPulseTime, (int)prevPulseTime);
        }
        Timer redrawTimer = (Timer) sender;
        redrawTimer.Stop();
        redrawTimer.Dispose();
    }


    /** Delete the temporary midi sound file */
    public void DeleteSoundFile() {
        if (tempSoundFile == "") {
            return;
        }
        try {
            FileInfo soundfile = new FileInfo(tempSoundFile);
            soundfile.Delete();
        }
        catch (IOException e) {
        }
        tempSoundFile = ""; 
    }

    /** Return the number of tracks selected in the MidiOptions.
     *  If the number of tracks is 0, there is no sound to play.
     */
    private int numberTracks() {
        int count = 0;
        for (int i = 0; i < options.tracks.Length; i++) {
            if (options.tracks[i] && !options.mute[i]) {
                count += 1;
            }
        }
        return count;
    }

    /** Create a new midi file with all the MidiOptions incorporated.
     *  Save the new file to TEMP/<originalfile>.MSM.mid, and store
     *  this temporary filename in tempSoundFile.
     */ 
    private void CreateMidiFile() {
        double inverse_tempo = 1.0 / midifile.Time.Tempo;
        double inverse_tempo_scaled = inverse_tempo * speedBar.Value / 100.0;
        options.tempo = (int)(1.0 / inverse_tempo_scaled);
        pulsesPerMsec = midifile.Time.Quarter * (1000.0 / options.tempo);

        string filename = Path.GetFileName(midifile.FileName).Replace(".mid", "") + ".MSM.mid";
        tempSoundFile = System.IO.Path.GetTempPath() + "/" + filename;

        /* If the filename is > 127 chars, the sound won't play */
        if (tempSoundFile.Length > 127) {
            tempSoundFile = System.IO.Path.GetTempPath() + "/MSM.mid";
        }

        if (midifile.ChangeSound(tempSoundFile, options) == false) {
            /* Failed to write to tempSoundFile */
            tempSoundFile = ""; 
        }
    }


    /** Play the sound for the given MIDI file */
    private void PlaySound(string filename) {
        if (Type.GetType("Mono.Runtime") != null)
            PlaySoundMono(filename);
        else 
            PlaySoundWindows(filename);
    }

    /** On Linux Mono, we spawn the timidity command to play the
     *  midi file for us.
     */
    private void PlaySoundMono(string filename) {
        try {
            ProcessStartInfo info = new ProcessStartInfo();
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardInput = true;
            info.UseShellExecute = false;
            info.FileName = "/usr/bin/timidity";
            info.Arguments = "\"" + filename + "\"";
            timidity = new Process();
            timidity.StartInfo = info;
            timidity.Start();
        }
        catch (Exception e) {
            timidity = null;
        }
    }

    /** On Windows, we use the Win32 C function mciSendString()
     *  for playing the music.
     */
    private void PlaySoundWindows(string filename) {
        string cmd = "open \"sequencer!" + filename + "\" alias midisheet";
        int ret = mciSendString(cmd, "", 0, 0);
        // mciGetErrorString(ret, errormsg, 256);
        ret = mciSendString("play midisheet", "", 0, 0);
        // mciGetErrorString(ret, errormsg, 256);
    }

    /** Stop playing the MIDI music */
    private void StopSound() {
        if (Type.GetType("Mono.Runtime") != null)
            StopSoundMono();
        else 
            StopSoundWindows();
    }

    /** Stop playing the music on Windows */
    private void StopSoundWindows() {
        int ret = mciSendString("stop midisheet", "", 0, 0);
        // mciGetErrorString(ret, errormsg, 256);
        ret = mciSendString("close midisheet", "", 0, 0);
        // mciGetErrorString(ret, errormsg, 256);
    }

    /** Stop playing the music on Linux */
    private void StopSoundMono() {
        if (timidity != null) {
            timidity.Kill();
            timidity.Dispose();
            timidity = null;
        }
    }


    /** The Linux Timidity MIDI player skips leading silence, and starts
     * playing the first note immediately.  We have to deal with this
     * by 'deleting' the silence time from the start/current pulse times.
     */
    private void SkipLeadingSilence() {

        /* Find how much silence there is between the 'pause time' and
         * the first note played after the pause time.
         */
        int silence = -1;
        for (int tracknum = 0; tracknum < midifile.Tracks.Count; tracknum++) {
            MidiTrack track = midifile.Tracks[tracknum];
            if (!options.tracks[tracknum]) {
                continue;
            }
            foreach (MidiNote m in track.Notes) {
                if (m.StartTime < options.pauseTime) {
                    continue;
                }
                if (silence == -1 || silence > m.StartTime - options.pauseTime) {
                    silence = m.StartTime - options.pauseTime;
                }
            }
        }

        if (silence > 0) {
            startPulseTime += silence;
            currentPulseTime += silence;
            prevPulseTime += silence;
        }
    }

    /** The callback for the play/pause button (a single button).
     *  If we're stopped or pause, then play the midi file.
     *  If we're currently playing, then initiate a pause.
     *  (The actual pause is done when the timer is invoked).
     */
    private void PlayPause(object sender, EventArgs args) {
        if (midifile == null || sheet == null || numberTracks() == 0) {
            return;
        }
        else if (playstate == initStop || playstate == initPause) {
            return;
        }
        else if (playstate == playing) {
            playstate = initPause;
            return;
        }
        else if (playstate == stopped || playstate == paused) {
            /* The startPulseTime is the pulse time of the midi file when
             * we first start playing the music.  It's used during shading.
             */
            if (options.playMeasuresInLoop) {
                /* If we're playing measures in a loop, make sure the
                 * currentPulseTime is somewhere inside the loop measures.
                 */
                int measure = (int)(currentPulseTime / midifile.Time.Measure);
                if ((measure < options.playMeasuresInLoopStart) ||
                    (measure > options.playMeasuresInLoopEnd)) {
                    currentPulseTime = options.playMeasuresInLoopStart * midifile.Time.Measure;
                }
                startPulseTime = currentPulseTime;
                options.pauseTime = (int)(currentPulseTime - options.shifttime);
            }
            else if (playstate == paused) {
                startPulseTime = currentPulseTime;
                options.pauseTime = (int)(currentPulseTime - options.shifttime);
            }
            else {
                options.pauseTime = 0;
                startPulseTime = options.shifttime;
                currentPulseTime = options.shifttime;
                prevPulseTime = options.shifttime - midifile.Time.Quarter;
            }

            if (Type.GetType("Mono.Runtime") != null) {
                SkipLeadingSilence();
            }

            CreateMidiFile();
            playstate = playing;
            Volume.SetVolume(volumeBar.Value);
            PlaySound(tempSoundFile);
            startTime = DateTime.Now.TimeOfDay;
            timer.Start(); 
            playButton.Image = pauseImage;
            playTip.SetToolTip(playButton, "Pause");
            sheet.ShadeNotes((int)currentPulseTime, (int)prevPulseTime, true);
            piano.ShadeNotes((int)currentPulseTime, (int)prevPulseTime);
            return;
        }
    }

    /** The callback for the Stop button.
     *  If playing, initiate a stop and wait for the timer to finish.
     *  Then do the actual stop.
     */
    public void Stop(object sender, EventArgs args) {
        if (midifile == null || sheet == null || playstate == stopped) {
            return;
        }

        if (playstate == initPause || playstate == initStop || playstate == playing) {
            /* Wait for timer to finish */
            playstate = initStop;
            System.Threading.Thread.Sleep(300); 
            DoStop();
        }
        else if (playstate == paused) {
            DoStop();
        }
    }

    /** Perform the actual stop, by stopping the sound,
     *  removing any shading, and clearing the state.
     */
    void DoStop() { 
        playstate = stopped;
        StopSound();

        /* Remove all shading by redrawing the music */
        sheet.Invalidate();
        piano.Invalidate();

        startPulseTime = 0;
        currentPulseTime = 0;
        prevPulseTime = 0;
        playButton.Image = playImage;
        playTip.SetToolTip(playButton, "Play");
        return;
    }

    /** Rewind the midi music back one measure.
     *  The music must be in the paused state.
     *  When we resume in playPause, we start at the currentPulseTime.
     *  So to rewind, just decrease the currentPulseTime,
     *  and re-shade the sheet music.
     */
    private void Rewind(object sender, EventArgs evt) {
        if (midifile == null || sheet == null || playstate != paused) {
            return;
        }
        /* Remove any highlighted notes */
        sheet.ShadeNotes(-10, (int)currentPulseTime, false);
        piano.ShadeNotes(-10, (int)currentPulseTime);
   
        prevPulseTime = currentPulseTime; 
        currentPulseTime -= midifile.Time.Measure;
        if (currentPulseTime < options.shifttime) {
            currentPulseTime = options.shifttime;
        }
        sheet.ShadeNotes((int)currentPulseTime, (int)prevPulseTime, false);
        piano.ShadeNotes((int)currentPulseTime, (int)prevPulseTime);
    }
    
    /** Fast forward the midi music by one measure.
     *  The music must be in the paused/stopped state.
     *  When we resume in playPause, we start at the currentPulseTime.
     *  So to fast forward, just increase the currentPulseTime,
     *  and re-shade the sheet music.
     */
    private void FastForward(object sender, EventArgs evt) {
        if (midifile == null || sheet == null) {
            return;
        }
        if (playstate != paused && playstate != stopped) {
            return;
        }
        playstate = paused;

        /* Remove any highlighted notes */
        sheet.ShadeNotes(-10, (int)currentPulseTime, false);
        piano.ShadeNotes(-10, (int)currentPulseTime);
   
        prevPulseTime = currentPulseTime; 
        currentPulseTime += midifile.Time.Measure;
        if (currentPulseTime > midifile.TotalPulses) {
            currentPulseTime -= midifile.Time.Measure;
        }
        sheet.ShadeNotes((int)currentPulseTime, (int)prevPulseTime, false);
        piano.ShadeNotes((int)currentPulseTime, (int)prevPulseTime);
    }


    /** The callback for the timer. If the midi is still playing, 
     *  update the currentPulseTime and shade the sheet music.  
     *  If a stop or pause has been initiated (by someone clicking
     *  the stop or pause button), then stop the timer.
     */
    void TimerCallback(object sender, EventArgs args) {
        if (midifile == null || sheet == null) {
            timer.Stop();
            playstate = stopped;
            return;
        }
        else if (playstate == stopped || playstate == paused) {
            /* This case should never happen */
            timer.Stop();
            return;
        }
        else if (playstate == initStop) {
            timer.Stop();
            return;
        }
        else if (playstate == playing) {
            TimeSpan diff = DateTime.Now.TimeOfDay.Subtract(startTime);
            long msec = diff.Minutes * 60 * 1000 + 
                        diff.Seconds * 1000 + diff.Milliseconds;
            prevPulseTime = currentPulseTime;
            currentPulseTime = startPulseTime + msec * pulsesPerMsec;

            /* If we're playing in a loop, stop and restart */
            if (options.playMeasuresInLoop) {
                double nearEndTime = currentPulseTime + pulsesPerMsec*10;
                int measure = (int)(nearEndTime / midifile.Time.Measure);
                if (measure > options.playMeasuresInLoopEnd) {
                    RestartPlayMeasuresInLoop();
                    return;
                }
            }

            /* Stop if we've reached the end of the song */
            if (currentPulseTime > midifile.TotalPulses) {
                timer.Stop();
                DoStop();
                return;
            }

            sheet.ShadeNotes((int)currentPulseTime, (int)prevPulseTime, true);
            piano.ShadeNotes((int)currentPulseTime, (int)prevPulseTime);
            return;
        }
        else if (playstate == initPause) {
            timer.Stop();
            TimeSpan diff = DateTime.Now.TimeOfDay.Subtract(startTime);
            long msec = diff.Minutes * 60 * 1000 + 
                        diff.Seconds * 1000 + diff.Milliseconds;

            StopSound();

            prevPulseTime = currentPulseTime;
            currentPulseTime = startPulseTime + msec * pulsesPerMsec;
            sheet.ShadeNotes((int)currentPulseTime, (int)prevPulseTime, false);
            piano.ShadeNotes((int)currentPulseTime, (int)prevPulseTime);
            playstate = paused;
            playButton.Image = playImage;
            playTip.SetToolTip(playButton, "Play");
            return;
        }
    }

    /** The "Play Measures in a Loop" feature is enabled, and we've reached
     *  the last measure. Stop the sound, unshade the music, and then
     *  start playing again.
     */
    private void RestartPlayMeasuresInLoop() {
        timer.Stop();
        playstate = stopped;
        StopSound();

        sheet.ShadeNotes(-10, (int)prevPulseTime, false);
        piano.ShadeNotes(-10, (int)prevPulseTime);
        currentPulseTime = -1;
        prevPulseTime = 0;
        System.Threading.Thread.Sleep(300);

        PlayPause(null, null);
    }

    /** Callback for volume bar.  Adjust the volume if the midi sound
     *  is currently playing.
     */
    private void ChangeVolume(object sender, EventArgs args) {
        if (playstate == playing) {
            Volume.SetVolume(volumeBar.Value);
        }
    }

}

}

