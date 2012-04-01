using System.IO;

namespace GhostDrive {
	enum Message : byte {
		List = 0,
		Status,
		Play,
		Pause,
		Next,
		Prev,
		Upload,
		Download,
	}

	public interface IDevice {
		string[] List(string path);
		Status GetStatus();
		void Play(string filename);
		void Pause();
		void Resume()
		bool Next();
		bool Prev();
		void Upload(string filename, Stream stream);
		Stream Download(string filename);
	}

	struct MessageHeader {
		public Message Type { get; private set; }
		public ushort Length { get; private set; }

		public static MessageHeader ReadFrom(Stream stream) {
			type = (Message)stream.ReadByte();
			length = stream.ReadUInt16();
		}
	}

	public class DeviceService {
		IDevice device;
		Stream endpoint;
		Thread thread;

		public DeviceService(IDevice device, Stream endpoint) {
			this.device = device;
			this.endpoint = endpoint;
		}

		public void Start() {
			if (thread != null)
				return;

			thread = new Thread(Process);
			thread.Start();
		}

		public void Stop() {
			if (thread == null)
				return;

			thread.Join();

			thread = null;
 		}

		void Process() {
			var buffer = new byte[1024];
			var stream = new MemoryStream(buffer);
			for (;;) {
				var header = MessageHeader.ReadFrom(endpoint);
				endpoint.Read(buffer, 0, header.Length);

				switch (header.Type) {
				case Message.List:
					
					var files = device.List(
					break;

				case Message.Status:
					// Send status
					break;

				case Message.Play:
				case Message.Pause:
				case Message.Next:
				case Message.Prev:
				case Message.Upload:
				case Message.Download:
				}
			}
		}
	}
}
