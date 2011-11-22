using System;
using System.Collections;
using System.IO;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace GhostDrive
{
    /// <summary>
    /// Parser for MIDI files, adapted from an online source
    /// </summary>
    public class MIDIFileParser
    {
        private FileStream _File;

        public uint Format { get; private set; }
        public uint TrackCount { get; private set; }
        public uint TicksPerBeat { get; private set; }
        public uint Tempo { get; private set; }
        public NoteEvent[] NoteEvents { get; private set; }
        public int NoteCount { get; private set; }

        public struct NoteEvent
        {
            public uint DeltaTime;
            public byte EventType;
            public byte NoteNumber;
        }

        public void ParseFile(string path)
        {
            //Pre-allocate or reuse array
            NoteEvents = NoteEvents ?? new NoteEvent[500];
            NoteCount = 0;
            Tempo = 500000;

            using (_File = File.OpenRead(path))
            {
                ReadHeaderChunk();
                BurnTrackChunk(); //The first track is typically metadata
                ReadTrackChunk();
            }
            Debug.Print("Read " + NoteCount + " notes");
        }

        //buffer for reading values
        byte[] _ReadBuffer = new byte[4];
        /// <summary>
        /// ensure the read buffer is large enough for a read
        /// </summary>
        /// <param name="requiredLength"></param>
        void EnsureBuffer(int requiredLength)
        {
            var length = _ReadBuffer.Length;
            if (length < requiredLength)
            {
                while (length < requiredLength)
                {
                    length *= 2;
                }
                _ReadBuffer = new byte[length];
            }
        }

        /// <summary>
        /// Reads a DWORD from a MIDI file (big endian)
        /// </summary>
        /// <returns></returns>
        uint ReadDWord()
        {
            FillBuffer(4);
            return (uint)((_ReadBuffer[0] << 24) + (_ReadBuffer[1] << 16) + (_ReadBuffer[2] << 8) + _ReadBuffer[3]);
        }

        /// <summary>
        /// Reads a WORD from a MIDI file (big endian)
        /// </summary>
        /// <returns></returns>
        uint ReadWord()
        {
            FillBuffer(2);
            return (uint)((_ReadBuffer[0] << 8) + _ReadBuffer[1]);
        }

        /// <summary>
        /// Reads a byte
        /// </summary>
        /// <remarks>
        /// Implemented this way because FileStream.ReadByte seems to do some allocation,
        /// But FileStream.Read seems to also do allocation, so it may not come out ahead.
        /// </remarks>
        /// <returns></returns>
        uint ReadByte()
        {
            FillBuffer(1);
            return (uint)_ReadBuffer[0];
        }

        /// <summary>
        /// Fills the read buffer with the specified length
        /// </summary>
        /// <param name="length"></param>
        private void FillBuffer(int length)
        {
            EnsureBuffer(length);
            var read = _File.Read(_ReadBuffer, 0, length);
            if (read != length) throw new Exception("Read past EOF");
        }

        /// <summary>
        /// Reads the header chunk from the file
        /// </summary>
        /// <returns></returns>
        private bool ReadHeaderChunk()
        {
            // Read "MThd"
            if (ReadByte() == 0x4D &&
                ReadByte() == 0x54 &&
                ReadByte() == 0x68 &&
                ReadByte() == 0x64)
            {
                // Read header length (should always be 6)
                var length = ReadDWord();
                if (length == 0x06)
                {
                    // Read format - 0 = single track, 1 = multiple track, 2 = multiple song
                    Format = ReadWord();
                    Debug.Print("Format " + Format);

                    // Read number of tracks that follow the header chunk
                    TrackCount = ReadWord();
                    Debug.Print("TrackCount " + TrackCount);

                    // Read the division (ticks per beat)
                    TicksPerBeat = ReadWord();
                    Debug.Print("TicksPerBeat " + TicksPerBeat);

                    return true;
                }
                else
                {
                    Debug.Print("Header length wasn't 6! - " + length);
                }
            }
            else
            {
                Debug.Print("Didn't read the header magic word!");
            }

            return false;
        }

        long _TrackStart;
        long _TrackEnd;
        /// <summary>
        /// Reads a track chunk and digests note events
        /// </summary>
        /// <returns></returns>
        private bool ReadTrackChunk()
        {
            uint trackLength;

            // Read "MTrk"
            if (ReadByte() == 0x4d &&
                ReadByte() == 0x54 &&
                ReadByte() == 0x72 &&
                ReadByte() == 0x6B)
            {
                // Read track length
                trackLength = ReadDWord();
                Debug.Print("Track Length: " + trackLength);

                _TrackStart = _File.Position;
                _TrackEnd = _TrackStart + trackLength;
                while (_File.Position < _TrackEnd)
                {
                    ReadTrackEvent();
                }

                return true;
            }
            else
            {
                Debug.Print("Didn't find magic number for track chunk");
            }

            return false;
        }

        /// <summary>
        /// Skips a track
        /// </summary>
        /// <returns></returns>
        private bool BurnTrackChunk()
        {
            uint trackLength;

            // Read "MTrk"
            if (ReadByte() == 0x4d &&
                ReadByte() == 0x54 &&
                ReadByte() == 0x72 &&
                ReadByte() == 0x6B)
            {
                // Read track length
                trackLength = ReadDWord();
                Debug.Print("Track Length: " + trackLength);

                _File.Seek(trackLength, SeekOrigin.Current);

                return true;
            }
            else
            {
                Debug.Print("Didn't find magic number for track chunk");
            }

            return false;
        }

        /// <summary>
        /// Reads and decodes a track event, adding note events
        /// </summary>
        private void ReadTrackEvent()
        {
            uint deltaTime = ReadDeltaTime();

            var eventType = ReadByte();

            if (eventType == 0xF0 ||
                eventType == 0xF7)
            {
                Debug.Print("Sys Ex Event");
                ReadSysExEvent();
            }
            else if (eventType == 0xFF)
            {
                Debug.Print("Meta Event");
                ReadMetaEvent();
            }
            else
            {
                Debug.Print("Midi Event");
                ReadMidiEvent(eventType, deltaTime);
            }
        }

        private uint _RunningStatus;
        /// <summary>
        /// Reads a MIDI event
        /// </summary>
        /// <param name="eventTypeIn"></param>
        /// <param name="deltaTime"></param>
        private void ReadMidiEvent(uint eventTypeIn, uint deltaTime)
        {
            uint eventType;
            uint parm1, parm2;

            // Check for running status
            if ((eventTypeIn & 0x80) > 0)
            {
                eventType = eventTypeIn;
                _RunningStatus = eventType;
                parm1 = ReadByte();
            }
            else
            {
                // running status, use last value
                eventType = _RunningStatus;
                parm1 = eventTypeIn;
            }



            NoteEvent noteEvent = new NoteEvent
            {
                DeltaTime = deltaTime,
                EventType = (byte)eventType,
                NoteNumber = (byte)parm1,
            };

            if ((eventType & 0xF0) == 0x80)
            {
                // Note Off
                Debug.Print("Note Off: ");// + parm1.ToString());
                parm2 = ReadByte();

                AddNoteEvent(noteEvent);
            }
            else if ((eventType & 0xF0) == 0x90)
            {
                // Note On
                Debug.Print("Note On: ");// + parm1.ToString());
                parm2 = ReadByte();

                AddNoteEvent(noteEvent);
            }
            else
            {
                if (((eventType & 0xF0) == 0xC0) ||
                    ((eventType & 0xF0) == 0xD0))
                {

                }
                else
                {
                    parm2 = ReadByte();
                }
                // Unhandled MIDI events
                Debug.Print("Unhandled MIDI Event: ");// + eventType.ToString());
            }
        }

        /// <summary>
        /// Adds a note event to the array
        /// </summary>
        /// <param name="noteEvent"></param>
        private void AddNoteEvent(NoteEvent noteEvent)
        {
            if (NoteEvents.Length <= NoteCount)
            {
                return;
                //TODO: sensible growth?
                var newArray = new NoteEvent[NoteEvents.Length * 2];
                Array.Copy(NoteEvents, newArray, NoteCount);
            }
            NoteEvents[NoteCount++] = noteEvent;
        }

        private void ReadMetaEvent()
        {
            uint metaType = ReadByte();
            uint len = ReadVLength();

            if (metaType == 0x51)
            {
                this.Tempo = ReadByte() << 16;
                this.Tempo += ReadByte() << 8;
                this.Tempo += ReadByte();
                Debug.Print("New Tempo: ");// + this.Tempo.ToString());
            }
            else
            {
                Debug.Print("Ignored Meta Event: ");//Length=" + len.ToString());

                if (metaType == 0x2f)
                {
                    Debug.Print("End of Track");
                }
                _File.Seek(len, SeekOrigin.Current);
            }
        }

        private void ReadSysExEvent()
        {
            uint len = ReadVLength();
            _File.Seek(len, SeekOrigin.Current);
        }

        private uint ReadDeltaTime()
        {
            uint time = ReadVLength();
            Debug.Print("Delta Time: ");// + time.ToString());
            return time;
        }

        /// <summary>
        /// Reads the silly variable-length data from a MIDI file
        /// </summary>
        /// <returns></returns>
        private uint ReadVLength()
        {
            uint value = 0;
            uint data;

            for (int i = 0; i < 4; i++)
            {
                data = ReadByte();
                value += data & 0x7F;
                if ((data & 0x80) == 0)
                {
                    break;
                }
                else
                {
                    value <<= 7;
                }
            }

            return value;
        }
    }
}
