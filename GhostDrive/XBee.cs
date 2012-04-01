using System;
using System.IO.Ports;
using System.Threading;

namespace XBee {
    public delegate bool FrameReceivedEventHandler(Frame frame);

	public enum CommandId : byte {
		AtCommand = 0x08,
		AtCommandQueue = 0x09,
		TxRequest = 0x10,
		ExplicitTxRequest = 0x11,
		RemoteCommandRequest = 0x17,
		CreateSourceRoute = 0x18,
		AtCommandResponse = 0x88,
		ModemStatus = 0x8a,
		TxStatus = 0x8b,
		RxPacket = 0x90,
		ExplicitRxPacket = 0x91,
		DataSampleRx = 0x92,
		SensorRead = 0x93,
		NodeIdentify = 0x94,
		RemoteCommandResponse = 0x95,
		FirmwareUpdateStatus = 0xa0,
		RouteRecord = 0xa1,
		RouteRequest = 0xa2,
	}

    public class Frame {
        public CommandId CommandId { get; set; }
        public int Length { get; set; }
        public byte[] Buffer { get; set; }
    }

	public class XBeeDevice : IDisposable {
		enum State { Open, Closed, Disposed }

		SerialPort port;
        Frame frame = new Frame { Buffer = new byte[512] }; // Just a guess on buffer size.
        byte[] writeBuffer = new byte[512];
		AutoResetEvent release = new AutoResetEvent(false);
		Thread thread;
		State state = State.Closed;

		public event FrameReceivedEventHandler FrameReceived;

		public XBeeDevice(SerialPort port)
        {
			this.port = port;
		}

		public void Dispose()
        {
			Monitor.Enter(this);
            {
				if (state != State.Disposed)
                {
					state = State.Disposed;
					thread.Join();
					port.Dispose();
				}
			}
			Monitor.Exit(this);	
		}

		public void Open()
        {
			Monitor.Enter(this);
            {
				if (state == State.Closed)
                {
					if (!port.IsOpen)
						port.Open();
                    if (thread == null)
                    {
                        thread = new Thread(Reader);
                        thread.Start();
                    }
					state = State.Open;
					release.Set();
				}
			}
			Monitor.Exit(this);
		}

		public void Close()
        {
			Monitor.Enter(this);
            {
				if (state == State.Open)
                {
					while (thread.ThreadState != ThreadState.WaitSleepJoin)
						Thread.Sleep(0);
					port.Close();
					state = State.Closed;
				}
			}
			Monitor.Exit(this);
		}

		bool CheckId(byte id) {
			switch ((CommandId)id)
            {
			    case CommandId.AtCommandResponse: goto case CommandId.RouteRequest;
			    case CommandId.ModemStatus: goto case CommandId.RouteRequest;
			    case CommandId.TxStatus: goto case CommandId.RouteRequest;
			    case CommandId.RxPacket: goto case CommandId.RouteRequest;
			    case CommandId.ExplicitRxPacket: goto case CommandId.RouteRequest;
			    case CommandId.DataSampleRx: goto case CommandId.RouteRequest;
			    case CommandId.SensorRead: goto case CommandId.RouteRequest;
			    case CommandId.NodeIdentify: goto case CommandId.RouteRequest;
			    case CommandId.RemoteCommandResponse: goto case CommandId.RouteRequest;
			    case CommandId.FirmwareUpdateStatus: goto case CommandId.RouteRequest;
			    case CommandId.RouteRecord: goto case CommandId.RouteRequest;
			    case CommandId.RouteRequest:
				    return true;
			    default:
				    return false;
			}
		}

		Frame EnsureReadFrame(Frame frame)
        {
            var buffer = frame.Buffer;
			for (;;)
            {
				// Find a potential frame
				while (buffer[0] != 0x7e)
					port.GuaranteedRead(buffer, 0, 1);

				// Read the length
				port.GuaranteedRead(buffer, 0, 2);
				var length = buffer[0] << 8 | buffer[1];

				// Read & check the command ID
				port.GuaranteedRead(buffer, 0, 1);
                var id = buffer[0];
                if (!CheckId(id))
					continue;

				// Read the packet, verify 
				port.GuaranteedRead(buffer, 0, length);

				byte ck = id;
				for (var i = 0; i < length; i++)
					ck += buffer[i];
                if (ck == 0xff) {
                    frame.Length = length - 1;
                    frame.CommandId = (CommandId)id;
                    break;
                }
			}
            return frame;
		}

		void Reader()
        {
            while (state != State.Disposed)
            {
                release.WaitOne();
                while (state == State.Open)
                {
                    frame = EnsureReadFrame(frame);
                    if (FrameReceived != null)
                        FrameReceived(frame);
                }
            }
		}

        public void WriteFrame(Frame frame) {
            var length = frame.Length + 1;
            writeBuffer[0] = 0x7e;
            writeBuffer[1] = (byte)(length >> 8);
            writeBuffer[2] = (byte)length;
            writeBuffer[3] = (byte)frame.CommandId;

            var ck = writeBuffer[3];
            var i = 0;
            for (; i < frame.Length; i++) {
                var d = frame.Buffer[i];
                ck += d;
                writeBuffer[i + 4] = d;
            }
            writeBuffer[i + 4] = (byte)(0xff - ck);

            port.Write(writeBuffer, 0, length + 4);
        }
	}
}
