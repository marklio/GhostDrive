﻿using System;
using System.Collections;
using System.IO;

#region This is inane
#if DEVICE
namespace System.Runtime.CompilerServices {
	public sealed class ExtensionAttribute : Attribute {}
}
#endif
#endregion

namespace Midi {
#region Helpers
	// See StreamExtensions
	enum Endianness {
		Little,
		Big,
	}

	// Make Stream a little more useable.
	static class StreamExtensions {
		static byte[] buf = new byte[4];
		public static ushort ReadUInt16(this Stream fs, Endianness en) {
			fs.Read(buf, 0, 2);
			if (en == Endianness.Big)
				return (ushort)(buf[0] << 8 | buf[1]);
			return (ushort)(buf[1] << 8 | buf[0]);
		}

		public static short ReadInt16(this Stream fs, Endianness en) {
			return (short)fs.ReadUInt16(en);
		}

		public static uint ReadUInt32(this Stream fs, Endianness en) {
			fs.Read(buf, 0, 4);
			if (en == Endianness.Big)
				return (uint)(buf[0] << 24 | buf[1] << 16 | buf[2] << 8 | buf[3]);
			return (uint)(buf[3] << 24 | buf[2] << 16 | buf[1] << 8 | buf[0]);
		}

		public static int ReadInt32(this Stream fs, Endianness en) {
			return (int)fs.ReadUInt32(en);
		}

		public static uint ReadVarUInt32(this Stream fs) {
			uint rv = 0, tmp = 0x80;
			for (var i = 0; i < 4 && (tmp & 0x80) == 0x80; i++) {
				tmp = (uint)fs.ReadByte();
				rv = rv << 7 | tmp & 0x7f;
			}
			return rv;
		}
	}
#endregion

#region Miscellaneous MIDI-related private types
	enum MidiEventType {
		NoteOff = 0x8,
		NoteOn,
		NoteAftertouch,
		Controller,
		ProgramChange,
		ChannelAftertouch,
		PitchBend,
		Meta,
	}

	enum MetaEventType {
		EndTrack = 0x2f,
		SetTempo = 0x51,
	}

	struct ChunkHeader {
		public uint Id, Size;

		public static ChunkHeader ReadFrom(Stream fs) {
			return new ChunkHeader {Id = fs.ReadUInt32(Endianness.Big), Size = fs.ReadUInt32(Endianness.Big)};
		}
	}

	struct EventHeader {
		byte packed;
		public uint DeltaTime;
		public byte Channel { get { return (byte)(packed & 0xf); } }
		public bool IsRunningEvent { get { return (packed & 0x80) == 0; } }
		public MidiEventType EventType {
			get { return (MidiEventType)(packed >> 4); }
			set { packed = (byte)(packed & (byte)0xf | (byte)value << 4); } 
		}

		public void ReadFrom(Stream fs) {
			DeltaTime = fs.ReadVarUInt32();
			packed = (byte)fs.ReadByte();
			if (IsRunningEvent)
				fs.Seek(-1, SeekOrigin.Current);
		}
	}
#endregion

#region Event types
	public abstract class Event {
		public ushort TrackId { get; internal set; } // Unique per-track ID
		public uint Time { get; protected set; }     // Absolute tick count
	}

	public class NoteOffEvent : Event {
		public byte Note { get; private set; }

		internal NoteOffEvent() {}
		internal NoteOffEvent Update(uint time, byte note) {
			Time = time;
			Note = note;
			return this;
		}
	}

	public class NoteOnEvent : Event {
		public byte Note { get; private set; }

		internal NoteOnEvent() {}
		internal NoteOnEvent Update(uint time, byte note) {
			Time = time;
			Note = note;
			return this;
		}
	}

	class EndTrackEvent : Event {
		public static EndTrackEvent Instance = new EndTrackEvent();
	}

	class SetTempoEvent : Event {
		public uint Tempo { get; internal set; }
	}
#endregion

#region Track and File types
	class Track {
		FileStream fs;
		long start, pos;
		NoteOnEvent noteOn;
		NoteOffEvent noteOff;
		EventHeader eh;
		MidiEventType run;
		uint time = 0;

		public static Track ReadFrom(ushort id, FileStream fs) {
			var ch = ChunkHeader.ReadFrom(fs);
			if (ch.Id != 0x4d54726b) // 'MTrk'
				throw new Exception("Malformed track header");
			var rv = new Track {
				noteOn = new NoteOnEvent { TrackId = id },
				noteOff = new NoteOffEvent { TrackId = id },
				fs = fs,
				start = fs.Position,
				pos = fs.Position
			};
			fs.Seek(ch.Size, SeekOrigin.Current);
			return rv;
		}

		// Would've used iterator methods, but they require ManagedThreadId.
		public Event Current { get; private set; }

		public bool MoveNext() {
			var rv = (Event)null;
			fs.Seek(pos, SeekOrigin.Begin);

			while (rv == null) {
				eh.ReadFrom(fs);
				if (eh.IsRunningEvent)
					eh.EventType = run;
				time += eh.DeltaTime;

				switch (eh.EventType) {
				case MidiEventType.Meta:
					rv = ParseMetaEvent(eh);
					break;

				case MidiEventType.NoteOff:
					rv = noteOff.Update(time, (byte)fs.ReadUInt16(Endianness.Little));
					break;

				case MidiEventType.NoteOn:
					rv = noteOn.Update(time, (byte)fs.ReadUInt16(Endianness.Little));
					break;

				case MidiEventType.NoteAftertouch: goto case MidiEventType.PitchBend;
				case MidiEventType.Controller: goto case MidiEventType.PitchBend;
				case MidiEventType.PitchBend:
					fs.ReadUInt16(Endianness.Little);
					break;

				default:
					if ((int)eh.EventType < 0x8)
						throw new Exception("bad event type!");
					fs.ReadByte();
					break;
				}

				run = eh.EventType;
			}

			pos = fs.Position;
			if (rv is EndTrackEvent)
				return false;
			Current = rv;
			return true;
		}

		public void Reset() {
			pos = start;
		}

		Event ParseMetaEvent(EventHeader eh) {
			if (eh.Channel == 0x00 || eh.Channel == 0x07) { // SysEx events
				fs.Seek((int)fs.ReadVarUInt32(), SeekOrigin.Current);
				return null;
			}

			var type = (MetaEventType)fs.ReadByte();
			var length = fs.ReadVarUInt32();

			switch (type) {
			case MetaEventType.EndTrack:
				return EndTrackEvent.Instance;

			case MetaEventType.SetTempo:
				return new SetTempoEvent { Tempo = (uint)(fs.ReadByte() << 16 | fs.ReadByte() << 8 | fs.ReadByte()) };

			default:
				fs.Seek((int)length, SeekOrigin.Current);
				return null;
			}
		}
	}

	public delegate void NoteOffEventHandler(NoteOffEvent @event);
	public delegate void NoteOnEventHandler(NoteOnEvent @event);

	public class MidiFile {
		private Track[] tracks;

		public ushort Format { get; private set; }
		public ushort TrackCount { get; private set; }
		public ushort TicksPerBeat { get; private set; }
		public uint Tempo { get; private set; }

		public event NoteOffEventHandler NoteOff; // Fires on note off events
		public event NoteOnEventHandler NoteOn;   // Fires on note on events

		public MidiFile(string path) {
			var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 8);

			var ch = ChunkHeader.ReadFrom(fs);
			if (ch.Id != 0x4d546864 || ch.Size != 6) // 'MThd'
				throw new Exception("Malformed MIDI header");
			Format = fs.ReadUInt16(Endianness.Big);
			TrackCount = fs.ReadUInt16(Endianness.Big);
			TicksPerBeat = fs.ReadUInt16(Endianness.Big);
			Tempo = 500000;

			tracks = new Track[TrackCount];
			for (var i = (ushort)0; i < TrackCount; i++)
				tracks[i] = Track.ReadFrom(i, fs);
		}

#if DEVICE
		// Fire events on all tracks in chronological order
		public void Play() {
            var events = new PriorityQueue(tracks.Length, (x) => ((Track)x).Current.Time);
			foreach (var t in tracks) {
				if (t.MoveNext())
					events.Push(t);
			}

			while (!events.IsEmpty) {
				var track = events.Pop() as Track;
				var @event = track.Current;

				if (@event is NoteOnEvent && NoteOn != null)
					NoteOn((NoteOnEvent)@event);
				else if (@event is NoteOffEvent && NoteOff != null)
					NoteOff((NoteOffEvent)@event);
				else if (@event is SetTempoEvent)
					Tempo = ((SetTempoEvent)@event).Tempo;

				if (track.MoveNext())
					events.Push(track);
			}
		}
#endif
	}
#endregion
}