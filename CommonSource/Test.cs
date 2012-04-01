using System;
using System.Diagnostics;
using System.Threading;
using Midi;

class App {
	static void Main(string[] args) {
		if (args.Length != 1) {
			Console.WriteLine("usage: miditest <filename>");
			return;
		}

		var midi = new MidiFile(args[0]);
		midi.NoteOn += evt => Console.WriteLine("+{0}", evt.Note);
		midi.NoteOff += evt => Console.WriteLine("+{0}", evt.Note);

		var t = new Thread(() => {
			while (!midi.Play())
				Thread.Yield();
		});
		t.Start();

		Thread.Sleep(500);
		midi.Pause();
		Thread.Sleep(500);
		midi.Resume();
		Thread.Sleep(500);
		midi.Stop();

		t.Join();
	}
}
