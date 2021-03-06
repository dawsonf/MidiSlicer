﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace M
{
	/// <summary>
	/// Indicates the state of the MIDI stream
	/// </summary>
#if MIDILIB
	public
#endif
	enum MidiStreamState
	{
		/// <summary>
		/// The stream is closed
		/// </summary>
		Closed = -1,
		/// <summary>
		/// The stream is paused
		/// </summary>
		Paused = 0,
		/// <summary>
		/// The stream is stopped
		/// </summary>
		Stopped = 1,
		/// <summary>
		/// The stream is playing
		/// </summary>
		Started = 2
	}
	/// <summary>
	/// Represents a MIDI stream
	/// </summary>
#if MIDILIB
	public
#endif
	class MidiStream : IDisposable
	{
		#region Win32
		delegate void MidiOutProc(IntPtr handle, int msg, int instance, int param1, int param2);
		delegate void TimerProc(IntPtr handle, int msg, int instance, int param1, int param2);
		[DllImport("kernel32.dll", SetLastError = false)]
		static extern void CopyMemory(IntPtr dest, IntPtr src, int count);
		[DllImport("winmm.dll")]
		static extern int midiStreamOpen(ref IntPtr handle, ref int deviceID, int cMidi,
			MidiOutProc proc, int instance, int flags);
		[DllImport("winmm.dll")]
		static extern int midiStreamProperty(IntPtr handle, ref MIDIPROPTEMPO tempo, int dwProperty);
		[DllImport("winmm.dll")]
		static extern int midiStreamProperty(IntPtr handle, ref MIDIPROPTIMEDIV timeDiv, int dwProperty);
		[DllImport("winmm.dll")]
		static extern int midiStreamOut(IntPtr handle, ref MIDIHDR lpMidiOutHdr, int uSize);
		[DllImport("winmm.dll")]
		static extern int midiStreamClose(IntPtr handle);
		[DllImport("winmm.dll")]
		static extern int midiStreamRestart(IntPtr handle);
		[DllImport("winmm.dll")]
		static extern int midiStreamPause(IntPtr handle);
		[DllImport("winmm.dll")]
		static extern int midiStreamStop(IntPtr handle);

		[DllImport("winmm.dll")]
		static extern int midiOutPrepareHeader(IntPtr hMidiOut, ref MIDIHDR lpMidiOutHdr, int uSize);
		[DllImport("winmm.dll")]
		static extern int midiOutUnprepareHeader(IntPtr hMidiOut, ref MIDIHDR lpMidiOutHdr, int uSize);
		
		[DllImport("winmm.dll")]
		static extern int midiStreamPosition(IntPtr handle, ref MMTIME lpMMTime, int uSize);
		[DllImport("winmm.dll")]
		static extern int midiOutShortMsg(IntPtr handle, int message);
		[DllImport("winmm.dll")]
		static extern int midiOutLongMsg(IntPtr hMidiOut, ref MIDIHDR lpMidiOutHdr, int uSize);
		[DllImport("winmm.dll")]
		static extern int midiOutGetVolume(IntPtr handle, out int volume);
		[DllImport("winmm.dll")]
		static extern int midiOutSetVolume(IntPtr handle, int volume);
		[DllImport("winmm.dll")]
		static extern int midiOutReset(IntPtr handle);
		[DllImport("winmm.dll")]
		static extern int midiOutGetErrorText(int errCode,
		   StringBuilder message, int sizeOfMessage);
		[DllImport("winmm.dll")]
		static extern IntPtr timeSetEvent(int delay, int resolution, TimerProc handler, IntPtr user, int eventType);
		[DllImport("winmm.dll")]
		static extern int timeKillEvent(IntPtr handle);
		[DllImport("winmm.dll")]
		static extern int timeBeginPeriod(int msec);
		[DllImport("winmm.dll")]
		static extern int timeEndPeriod(int msec);
		
		[StructLayout(LayoutKind.Sequential)]
		private struct MIDIHDR
		{
			public IntPtr lpData;          // offset  0- 3
			public uint dwBufferLength;  // offset  4- 7
			public uint dwBytesRecorded; // offset  8-11
			public IntPtr dwUser;          // offset 12-15
			public uint dwFlags;         // offset 16-19
			public IntPtr lpNext;          // offset 20-23
			public IntPtr reserved;        // offset 24-27
			public uint dwOffset;        // offset 28-31
			public IntPtr dwReserved0;
			public IntPtr dwReserved1;
			public IntPtr dwReserved2;
			public IntPtr dwReserved3;
			public IntPtr dwReserved4;
			public IntPtr dwReserved5;
			public IntPtr dwReserved6;
			public IntPtr dwReserved7;
		}
		[StructLayout(LayoutKind.Sequential)]
		private struct MIDIPROPTIMEDIV
		{
			public int cbStruct;
			public int dwTimeDiv;
		}
		[StructLayout(LayoutKind.Sequential)]
		private struct MIDIPROPTEMPO
		{
			public int cbStruct;
			public int dwTempo;
		}
		[StructLayout(LayoutKind.Explicit)]
		private struct MMTIME
		{
			[FieldOffset(0)] public int wType;
			[FieldOffset(4)] public int ms;
			[FieldOffset(4)] public int sample;
			[FieldOffset(4)] public int cb;
			[FieldOffset(4)] public int ticks;
			[FieldOffset(4)] public byte smpteHour;
			[FieldOffset(5)] public byte smpteMin;
			[FieldOffset(6)] public byte smpteSec;
			[FieldOffset(7)] public byte smpteFrame;
			[FieldOffset(8)] public byte smpteFps;
			[FieldOffset(9)] public byte smpteDummy;
			[FieldOffset(10)] public byte pad0;
			[FieldOffset(11)] public byte pad1;
			[FieldOffset(4)] public int midiSongPtrPos;
		}
		[StructLayout(LayoutKind.Sequential)]
		private struct MIDIEVENT
		{
			public int dwDeltaTime;
			public int dwStreamId;
			public int dwEvent;
		}
		const int MEVT_TEMPO = 0x01;
		const int MEVT_NOP = 0x02;
		const int CALLBACK_FUNCTION = 196608;
		const int MOM_OPEN = 0x3C7;
		const int MOM_CLOSE = 0x3C8;
		const int MOM_DONE = 0x3C9;
		const int TIME_MS = 0x0001;
		const int TIME_BYTES = 0x0004;
		const int TIME_SMPTE = 0x0008;
		const int TIME_MIDI = 0x0010;
		const int TIME_TICKS = 0x0020;
		const int MIDIPROP_SET = unchecked((int)0x80000000);
		const int MIDIPROP_GET = 0x40000000;
		const int MIDIPROP_TIMEDIV = 1;
		const int MIDIPROP_TEMPO = 2;
		const int MHDR_DONE = 1;
		const int MHDR_PREPARED = 2;
		const int MEVT_F_LONG = unchecked((int)0x80000000);
		const int TIME_ONESHOT = 0;
		const int TIME_PERIODIC = 1;
		#endregion
		const int _SendBufferSize = 64 * 1024 - 64;

		int _deviceIndex;
		IntPtr _handle;
		IntPtr _timerHandle;
		MidiOutProc _outCallback;
		TimerProc _timerCallback;
		MIDIHDR _sendHeader;
		IntPtr _sendEventBuffer;
		int _sendQueuePosition;
		int _tempoSyncMessageCount;
		int _tempoSyncMessagesSentCount;
		// must be an int to use interlocked
		// 0=false, nonzero = true
		int _tempoSyncEnabled;

		List<MidiEvent> _sendQueue;
		MidiStreamState _state = MidiStreamState.Closed;
		internal MidiStream(int deviceIndex)
		{
			if (0>deviceIndex)
				throw new ArgumentOutOfRangeException("deviceIndex");
			_deviceIndex = deviceIndex;
			_handle = IntPtr.Zero;
			_sendHeader= default(MIDIHDR);
			_sendEventBuffer= IntPtr.Zero;
			_sendQueuePosition = 0;
			_outCallback = new MidiOutProc(_MidiOutProc);
			_timerCallback = new TimerProc(_TimerProc);
			_tempoSyncEnabled = 0;
			_tempoSyncMessageCount = 100;
			_tempoSyncMessagesSentCount = 0;
		}
		
		/// <summary>
		/// Raised when a Send() operation has completed. This only applies to sending MidiEvent items
		/// </summary>
		public event EventHandler SendComplete;
		/// <summary>
		/// Raised when the stream is opened
		/// </summary>
		public event EventHandler Opened;
		/// <summary>
		/// Raised when the stream is closed
		/// </summary>
		public event EventHandler Closed;
		/// <summary>
		/// Indicates the state of the MIDI stream
		/// </summary>
		public MidiStreamState State => _state;
		/// <summary>
		/// Indicates whether or not the stream attempts to synchronize the remote device's tempo
		/// </summary>
		public bool TempoSynchronizationEnabled {
			get {
				return 0 != _tempoSyncMessageCount;
			} 
			set {
				if(value)
				{
					if(MidiStreamState.Started==_state)
					{
						var tmp = Tempo;
						var spb = 60/tmp;
						var ms = unchecked((int)(Math.Round((1000 * spb)/24)));
						_RestartTimer(ms);
					}
					Interlocked.Exchange(ref _tempoSyncEnabled, 1);
					return;
				}
				Interlocked.Exchange(ref _tempoSyncEnabled, 0);
				Interlocked.Exchange(ref _tempoSyncMessagesSentCount, 0);
				_DisposeTimer();

			}
		}
		/// <summary>
		/// Indicates the number of time clock sync messages to send when the tempo is changed. 0 indicates continuous synchronization
		/// </summary>
		public int TempoSynchronizationMessageCount {
			get {
				return _tempoSyncMessageCount;
			}
			set {
				Interlocked.Exchange(ref _tempoSyncMessageCount, value);
			}
		}
		/// <summary>
		/// Opens the stream
		/// </summary>
		public void Open()
		{
			if (IntPtr.Zero!= _handle)
				throw new InvalidOperationException("The device is already open");
			_sendEventBuffer = Marshal.AllocHGlobal(64 * 1024);
			var di = _deviceIndex;
			var h = IntPtr.Zero;
			_CheckOutResult(midiStreamOpen(ref h, ref di, 1, _outCallback, 0, CALLBACK_FUNCTION));
			Interlocked.Exchange(ref _handle, h);
			_state = MidiStreamState.Paused;
		}
		/// <summary>
		/// Closes the stream
		/// </summary>
		public void Close()
		{
			_DisposeTimer();
			if (IntPtr.Zero != _handle) {
				Stop();
				Reset();
				_CheckOutResult(midiStreamClose(_handle));
				Interlocked.Exchange(ref _handle , IntPtr.Zero);
				Marshal.FreeHGlobal(_sendEventBuffer);
				_sendEventBuffer = IntPtr.Zero;
				GC.SuppressFinalize(this);
				_state = MidiStreamState.Closed;
			}
		}
		/// <summary>
		/// Sends MIDI events to the stream
		/// </summary>
		/// <param name="events">The events to send</param>
		public void Send(params MidiEvent[] events)
			=> Send((IEnumerable<MidiEvent>)events);
		
		/// <summary>
		/// Sends a MIDI event to the stream
		/// </summary>
		/// <param name="events">The events to send</param>
		public void Send(IEnumerable<MidiEvent> events) {
			if (null != _sendQueue)
			{
				
				throw new InvalidOperationException("The device is already sending");
			}
			
			var list = new List<MidiEvent>(128);
			// break out sysex messages into parts
			foreach(var @event in events)
			{
				// sysex
				if(0xF0==@event.Message.Status)
				{
					var data = (@event.Message as MidiMessageSysex).Data;
					if (null == data)
						return;
					if (254 < data.Length)
					{
						var len = 254;
						for (var i = 0; i < data.Length; i += len)
						{
							if (data.Length <= i + len)
							{
								len = data.Length - i;
							}
							var buf = new byte[len];
							if (0 == i)
							{
								Array.Copy(data, 0, buf, 0, len);
								list.Add(new MidiEvent(@event.Position, new MidiMessageSysex(buf)));
							}
							else
							{
								Array.Copy(data, i, buf, 0, len);
								list.Add(new MidiEvent(@event.Position, new MidiMessageSysexPart(buf)));
							}
						}
					}
					else
					{
						list.Add(@event);
					}
				} else
					list.Add(@event);
			}
			Interlocked.Exchange(ref _sendQueue, list);
			Interlocked.Exchange(ref _sendQueuePosition , 0);
			_SendBlock();
		}
		void _SendBlock()
		{
			if (null == _sendQueue)
				return;
			if (IntPtr.Zero == _handle)
				throw new InvalidOperationException("The stream is closed.");
			
			if (IntPtr.Zero != Interlocked.CompareExchange(ref _sendHeader.lpData, _sendEventBuffer, IntPtr.Zero))
				throw new InvalidOperationException("The stream is busy playing.");

			int baseEventSize = Marshal.SizeOf(typeof(MIDIEVENT));
			int blockSize = 0;
			IntPtr eventPointer = _sendEventBuffer;
			var ofs = 0;
			var ptrOfs = 0;
			for(;_sendQueuePosition<_sendQueue.Count;Interlocked.Exchange(ref _sendQueuePosition,_sendQueuePosition+1))
			{
				var @event = _sendQueue[_sendQueuePosition];
				if (0x00 != @event.Message.Status && 0xF0 != (@event.Message.Status & 0xF0))
				{
					if (_SendBufferSize < blockSize+baseEventSize)
						break;
					blockSize += baseEventSize;
					var se = new MIDIEVENT();
					se.dwDeltaTime = @event.Position + ofs;
					se.dwStreamId = 0;
					se.dwEvent = MidiUtility.PackMessage(@event.Message);
					var gch = GCHandle.Alloc(se, GCHandleType.Pinned);
					CopyMemory(new IntPtr(ptrOfs + eventPointer.ToInt64()), gch.AddrOfPinnedObject(), Marshal.SizeOf(typeof(MIDIEVENT)));
					gch.Free();
					ptrOfs += baseEventSize;
					ofs = 0;
				}
				else if (0xFF == @event.Message.Status)
				{
					var mm = @event.Message as MidiMessageMeta;
					if (0x51 == mm.Data1) // tempo
					{
						if (_SendBufferSize < blockSize+baseEventSize)
							break;
						blockSize += baseEventSize;
						var se = new MIDIEVENT();
						se.dwDeltaTime = @event.Position + ofs;
						se.dwStreamId = 0;
						se.dwEvent = (mm.Data[0] << 16) | (mm.Data[1] << 8) | mm.Data[2] | (MEVT_TEMPO << 24);
						var gch = GCHandle.Alloc(se, GCHandleType.Pinned);
						CopyMemory(new IntPtr(ptrOfs + eventPointer.ToInt64()), gch.AddrOfPinnedObject(), Marshal.SizeOf(typeof(MIDIEVENT)));
						gch.Free();
						ptrOfs += baseEventSize;
						ofs = 0;
					}
					else if (0x2f == mm.Data1) // end track 
					{
						if (_SendBufferSize < blockSize+baseEventSize)
							break;
						blockSize += baseEventSize;
						
						// add a NOP message to it just to pad our output in case we're looping
						var se = new MIDIEVENT();
						se.dwDeltaTime = @event.Position + ofs;
						se.dwStreamId = 0;
						se.dwEvent = (MEVT_NOP << 24);
						var gch = GCHandle.Alloc(se, GCHandleType.Pinned);
						CopyMemory(new IntPtr(ptrOfs + eventPointer.ToInt64()), gch.AddrOfPinnedObject(), Marshal.SizeOf(typeof(MIDIEVENT)));
						gch.Free();
						ptrOfs += baseEventSize;
						ofs = 0;
					}
					else
						ofs = @event.Position;
				}
				else // sysex or sysex part
				{
					byte[] data;
					if (0 == @event.Message.Status)
						data = (@event.Message as MidiMessageSysexPart).Data;
					else
						data = MidiUtility.ToMessageBytes(@event.Message);


					var dl = data.Length;
					if (0 != (dl % 4))
						dl += 4 - (dl % 4);
					if (_SendBufferSize < blockSize+baseEventSize+dl)
						break;

					blockSize += baseEventSize + dl;
					
					var se = new MIDIEVENT();
					se.dwDeltaTime = @event.Position + ofs;
					se.dwStreamId = 0;
					se.dwEvent = MEVT_F_LONG | data.Length;
					var gch = GCHandle.Alloc(se, GCHandleType.Pinned);
					CopyMemory(new IntPtr(ptrOfs + eventPointer.ToInt64()), gch.AddrOfPinnedObject(), Marshal.SizeOf(typeof(MIDIEVENT)));
					gch.Free();
					ptrOfs += baseEventSize;
					Marshal.Copy(data, 0, new IntPtr(ptrOfs + eventPointer.ToInt64()), data.Length);

					ptrOfs += dl;
					ofs = 0;
				}
			} 
			_sendHeader = default(MIDIHDR);
			_sendHeader.dwBufferLength = _sendHeader.dwBytesRecorded = unchecked((uint)blockSize);
			_sendHeader.lpData = _sendEventBuffer;
			int headerSize = Marshal.SizeOf(typeof(MIDIHDR));
			_CheckOutResult(midiOutPrepareHeader(_handle, ref _sendHeader, headerSize));
			_CheckOutResult(midiStreamOut(_handle, ref _sendHeader, headerSize));
			
		}
		/// <summary>
		/// Sends events directly to the stream
		/// </summary>
		/// <param name="events">The events to send</param>
		public void SendDirect(params MidiEvent[] events)
			=> SendDirect((IEnumerable<MidiEvent>)events);
		/// <summary>
		/// Sends events directly to the event queue without buffering
		/// </summary>
		/// <param name="events">The events to send</param>
		/// <remarks>The total size of the events must be less than 64kb</remarks>
		public void SendDirect(IEnumerable<MidiEvent> events)
		{
			if (null == events)
				throw new ArgumentNullException("events");
			if (IntPtr.Zero == _handle)
				throw new InvalidOperationException("The stream is closed.");
			if (IntPtr.Zero != _sendHeader.lpData)
				throw new InvalidOperationException("The stream is busy playing.");
			int baseEventSize = Marshal.SizeOf(typeof(MIDIEVENT));
			int blockSize = 0;
			IntPtr eventPointer = _sendEventBuffer;
			var ofs = 0;
			var ptrOfs = 0;
			var hasEvents = false;
			foreach (var @event in events)
			{
				hasEvents = true;
				if (0xF0 != (@event.Message.Status & 0xF0))
				{
					blockSize += baseEventSize;
					if (_SendBufferSize <= blockSize)
						throw new ArgumentException("There are too many events in the event buffer - maximum size must be 64k", "events");
					var se = new MIDIEVENT();
					se.dwDeltaTime = @event.Position + ofs;
					se.dwStreamId = 0;
					se.dwEvent = MidiUtility.PackMessage(@event.Message);
					Marshal.StructureToPtr(se, new IntPtr(ptrOfs + eventPointer.ToInt64()), false);
					ptrOfs += baseEventSize;
					ofs = 0;
				}
				else if (0xFF == @event.Message.Status)
				{
					var mm = @event.Message as MidiMessageMeta;
					if (0x51 == mm.Data1) // tempo
					{
						blockSize += baseEventSize;
						if (_SendBufferSize <= blockSize)
							throw new ArgumentException("There are too many events in the event buffer - maximum size must be 64k", "events");

						var se = new MIDIEVENT();
						se.dwDeltaTime = @event.Position + ofs;
						se.dwStreamId = 0;
						se.dwEvent = (mm.Data[0] << 16) | (mm.Data[1] << 8) | mm.Data[2] | (MEVT_TEMPO << 24);
						Marshal.StructureToPtr(se, new IntPtr(ptrOfs + eventPointer.ToInt64()), false);
						ptrOfs += baseEventSize;
						ofs = 0;
						// TODO: This signal is sent too early. It should really wait until after the
						// MEVT_TEMPO message is processed by the driver, but i have no easy way to
						// do that. All we can do is hope, here
						Interlocked.Exchange(ref _tempoSyncMessagesSentCount, 0);
					}
					else if (0x2f == mm.Data1) // end track 
					{
						blockSize += baseEventSize;
						if (_SendBufferSize <= blockSize)
							throw new ArgumentException("There are too many events in the event buffer - maximum size must be 64k", "events");

						// add a NOP message to it just to pad our output in case we're looping
						var se = new MIDIEVENT();
						se.dwDeltaTime = @event.Position + ofs;
						se.dwStreamId = 0;
						se.dwEvent = (MEVT_NOP << 24);
						Marshal.StructureToPtr(se, new IntPtr(ptrOfs + eventPointer.ToInt64()), false);
						ptrOfs += baseEventSize;
						ofs = 0;
					} else
						ofs = @event.Position;
				}
				else // sysex
				{
					var msx = @event.Message as MidiMessageSysex;
					var dl = msx.Data.Length + 1;
					if (0 != (dl % 4))
					{
						dl += 4 - (dl % 4);
					}
					blockSize += baseEventSize+dl;
					if (_SendBufferSize <= blockSize)
						throw new ArgumentException("There are too many events in the event buffer - maximum size must be 64k", "events");

					var se = new MIDIEVENT();
					se.dwDeltaTime = @event.Position + ofs;
					se.dwStreamId = 0;
					se.dwEvent = MEVT_F_LONG | (msx.Data.Length + 1);
					Marshal.StructureToPtr(se, new IntPtr(ptrOfs + eventPointer.ToInt64()), false);
					ptrOfs += baseEventSize;
					Marshal.WriteByte(new IntPtr(ptrOfs + eventPointer.ToInt64()), msx.Status);
					Marshal.Copy(msx.Data,0,new IntPtr(ptrOfs + eventPointer.ToInt64()+1), msx.Data.Length);
					
					ptrOfs += dl;
					ofs = 0;
				}			
			}
			if (hasEvents)
			{
				_sendHeader = default(MIDIHDR);
				Interlocked.Exchange(ref _sendHeader.lpData, eventPointer);
				_sendHeader.dwBufferLength = _sendHeader.dwBytesRecorded = unchecked((uint)blockSize);
				_sendEventBuffer = eventPointer;
				int headerSize = Marshal.SizeOf(typeof(MIDIHDR));
				_CheckOutResult(midiOutPrepareHeader(_handle, ref _sendHeader, headerSize));
				_CheckOutResult(midiStreamOut(_handle, ref _sendHeader, headerSize));
			}

		}
		/// <summary>
		/// Sends a message immediately to the device
		/// </summary>
		/// <param name="message">The message to send</param>
		/// <remarks>The message is not queued. Tempo change messages are not honored.</remarks>
		public void Send(MidiMessage message)
		{
			if (IntPtr.Zero == _handle)
				throw new InvalidOperationException("The device is closed.");
			if (null == message)
				throw new ArgumentNullException("message");
			if (0xF0 == (message.Status & 0xF0))
			{
				if (0xF != message.Channel)
				{
					var data = MidiUtility.ToMessageBytes(message);
					if (null == data)
						return;
					if (254 < data.Length)
					{
						var len = 254;
						for (var i = 0; i < data.Length; i += len)
						{
							if (data.Length <= i + len)
							{
								len = data.Length - i;
							}
							_SendRaw(data, i, len);

						}
					}
					else
						_SendRaw(data, 0, data.Length);
				}
			}
			else
			{
				_CheckOutResult(midiOutShortMsg(_handle, MidiUtility.PackMessage(message)));
			}
		}
		void _SendRaw(byte[] data, int startIndex, int length)
		{
			var hdrSize = Marshal.SizeOf(typeof(MIDIHDR));
			var hdr = new MIDIHDR();
			var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
			try
			{
				hdr.lpData = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + startIndex);
				hdr.dwBufferLength = hdr.dwBytesRecorded = (uint)(length);
				hdr.dwFlags = 0;
				_CheckOutResult(midiOutPrepareHeader(_handle, ref hdr, hdrSize));
				while ((hdr.dwFlags & MHDR_PREPARED) != MHDR_PREPARED)
				{
					Thread.Sleep(1);
				}
				_CheckOutResult(midiOutLongMsg(_handle, ref hdr, hdrSize));
				while ((hdr.dwFlags & MHDR_DONE) != MHDR_DONE)
				{
					Thread.Sleep(1);
				}
				_CheckOutResult(midiOutUnprepareHeader(_handle, ref hdr, hdrSize));
			}
			finally
			{
				handle.Free();

			}
		}
		void IDisposable.Dispose()
		{
			Close();
		}
		/// <summary>
		/// Destroys this instance
		/// </summary>
		~MidiStream()
		{
			Close();
		}
		void _RestartTimer(int ms)
		{
			if (0 >= ms)
				throw new ArgumentOutOfRangeException("ms");
			_DisposeTimer();
			var h = timeSetEvent(ms, 0, _timerCallback, IntPtr.Zero,TIME_ONESHOT);
			if (IntPtr.Zero == h)
				throw new Exception("Could not create multimedia timer");
			Interlocked.Exchange(ref _timerHandle, h);
		}
		void _DisposeTimer()
		{
			if(null!=_timerHandle)
			{
				timeKillEvent(_timerHandle);
				Interlocked.Exchange(ref _timerHandle,IntPtr.Zero);
			}
		}
		void _TimerProc(IntPtr handle, int msg, int user, int param1, int param2)
		{
			if (IntPtr.Zero!=_handle && _timerHandle==handle && 0!=_tempoSyncEnabled)
			{
				if (0==_tempoSyncMessageCount || _tempoSyncMessagesSentCount < _tempoSyncMessageCount)
				{
					// quickly send a time sync message
					midiOutShortMsg(_handle, 0xF8);
					Interlocked.Increment(ref _tempoSyncMessagesSentCount);
				}
				var tmp = Tempo;
				var spb = 60 / tmp;
				var ms = unchecked((int)(Math.Round((1000 * spb) / 24)));
				_RestartTimer(ms);
				
			}
		}
		void _MidiOutProc(IntPtr handle, int msg, int instance, int param1, int param2)
		{
			switch(msg)
			{
				case MOM_OPEN:
					Opened?.Invoke(this, EventArgs.Empty);
					break;
				case MOM_CLOSE:
					Closed?.Invoke(this, EventArgs.Empty);
					break;
				case MOM_DONE:

					if (IntPtr.Zero != _sendHeader.lpData)
					{
						_CheckOutResult(midiOutUnprepareHeader(_handle, ref _sendHeader, Marshal.SizeOf(typeof(MIDIHDR))));
						Interlocked.Exchange(ref _sendHeader.lpData,IntPtr.Zero);
						Interlocked.Exchange(ref _sendQueuePosition, 0);
						Interlocked.Exchange(ref _sendQueue, null);
					}

					if(null==_sendQueue)
						SendComplete?.Invoke(this, EventArgs.Empty);
					else
					{
						_SendBlock();
					}
					break;
				
			}
		}
		/// <summary>
		/// Starts the stream
		/// </summary>
		public void Start()
		{
			if (IntPtr.Zero == _handle)
				throw new InvalidOperationException("The stream is closed.");
			switch(_state)
			{
				case MidiStreamState.Paused:
				case MidiStreamState.Stopped:
					
					var tmp = Tempo;
					var spb = 60 / tmp;
					var ms = unchecked((int)(Math.Round((1000 * spb) / 24)));
					Interlocked.Exchange(ref _tempoSyncMessagesSentCount, 0);
					_RestartTimer(ms);
					
					_CheckOutResult(midiStreamRestart(_handle));
					_state = MidiStreamState.Started;
					break;		
			}
		}
		/// <summary>
		/// Stops the stream
		/// </summary>
		public void Stop()
		{
			if (IntPtr.Zero == _handle)
				throw new InvalidOperationException("The stream is closed.");
			switch (_state)
			{
				case MidiStreamState.Paused:
				case MidiStreamState.Started:
					_DisposeTimer();
					_CheckOutResult(midiStreamStop(_handle));
					Interlocked.Exchange(ref _tempoSyncMessagesSentCount, 0);
					_state = MidiStreamState.Stopped;
					
					Interlocked.Exchange(ref _sendQueuePosition, 0);
					
					if(null!=_sendQueue)
					{
						Interlocked.Exchange(ref _sendQueue, null);
					}
					break;
			}
		}
		/// <summary>
		/// Pauses the stream
		/// </summary>
		public void Pause()
		{
			if (IntPtr.Zero == _handle)
				throw new InvalidOperationException("The stream is closed.");
			switch (_state)
			{
				case MidiStreamState.Started:
					_CheckOutResult(midiStreamPause(_handle));
					_state = MidiStreamState.Paused;
					Interlocked.Exchange(ref _tempoSyncMessagesSentCount, 0);
					break;
			}
		}
		/// <summary>
		/// Resets the MIDI output.
		/// </summary>
		/// <remarks>Terminates any sysex messages and sends note offs to all channels, as well as turning off the sustain controller for each channel</remarks>
		public void Reset()
		{
			if (IntPtr.Zero == _handle)
				throw new InvalidOperationException("The stream is closed.");
			_CheckOutResult(midiOutReset(_handle));
		}
		/// <summary>
		/// Indicates the position in ticks
		/// </summary>
		public int PositionTicks {
			get {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				switch (_state)
				{
					case MidiStreamState.Started:
					case MidiStreamState.Paused:
						MMTIME mm = new MMTIME();
						mm.wType = TIME_TICKS;
						_CheckOutResult(midiStreamPosition(_handle, ref mm, Marshal.SizeOf(typeof(MMTIME))));
						if (TIME_TICKS != mm.wType)
							throw new NotSupportedException("The position format is not supported.");
						return mm.ticks;
					default:
						return 0;
				}
			}
		}
		/// <summary>
		/// Indicates the position in milliseconds
		/// </summary>
		public int PositionMilliseconds {
			get {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				switch (_state)
				{
					case MidiStreamState.Started:
					case MidiStreamState.Paused:
						MMTIME mm = new MMTIME();
						mm.wType = TIME_MS;
						_CheckOutResult(midiStreamPosition(_handle, ref mm, Marshal.SizeOf(typeof(MMTIME))));
						if (TIME_MS != mm.wType)
							throw new NotSupportedException("The position format is not supported.");
						return mm.ms;
					default:
						return 0;
				}
			}
		}
		/// <summary>
		/// Indicates the song pointer position
		/// </summary>
		public int PositionSongPointer {
			get {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				switch (_state)
				{
					case MidiStreamState.Started:
					case MidiStreamState.Paused:
						MMTIME mm = new MMTIME();
						mm.wType = TIME_MIDI;
						_CheckOutResult(midiStreamPosition(_handle, ref mm, Marshal.SizeOf(typeof(MMTIME))));
						if (TIME_MIDI != mm.wType)
							throw new NotSupportedException("The position format is not supported.");
						return mm.midiSongPtrPos;
					default:
						return 0;
				}
			}
		}
		/// <summary>
		/// Indicates the position in bytes
		/// </summary>
		public int PositionBytes {
			get {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				switch (_state)
				{
					case MidiStreamState.Started:
					case MidiStreamState.Paused:
						MMTIME mm = new MMTIME();
						mm.wType = TIME_BYTES;
						_CheckOutResult(midiStreamPosition(_handle, ref mm, Marshal.SizeOf(typeof(MMTIME))));
						if (TIME_BYTES != mm.wType)
							throw new NotSupportedException("The position format is not supported.");
						return mm.cb;
					default:
						return 0;
				}
			}
		}
		/// <summary>
		/// Indicates the position in SMPTE format
		/// </summary>
		public MidiSmpteTime PositionSmpte {
			get {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				switch (_state)
				{
					case MidiStreamState.Started:
					case MidiStreamState.Paused:
						MMTIME mm = new MMTIME();
						mm.wType = TIME_SMPTE;
						_CheckOutResult(midiStreamPosition(_handle, ref mm, Marshal.SizeOf(typeof(MMTIME))));
						if (TIME_SMPTE != mm.wType)
							throw new NotSupportedException("The position format is not supported.");
						return new MidiSmpteTime(new TimeSpan(0, mm.smpteHour, mm.smpteMin, mm.smpteSec, 0), mm.smpteFrame, mm.smpteFps);
					default:
						return default(MidiSmpteTime);
				}
			}
		}
		/// <summary>
		/// Indicates the MicroTempo of the stream
		/// </summary>
		public int MicroTempo {
			get {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				var t = new MIDIPROPTEMPO();
				t.cbStruct = Marshal.SizeOf(typeof(MIDIPROPTEMPO));
				_CheckOutResult(midiStreamProperty(_handle, ref t, MIDIPROP_GET | MIDIPROP_TEMPO));
				return unchecked(t.dwTempo);
			}
			set {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				Interlocked.Exchange(ref _tempoSyncMessagesSentCount, 0);
				var t = new MIDIPROPTEMPO();
				t.cbStruct = Marshal.SizeOf(typeof(MIDIPROPTEMPO));
				t.dwTempo= value;
				_CheckOutResult(midiStreamProperty(_handle, ref t, MIDIPROP_SET | MIDIPROP_TEMPO));
			}
		}
		/// <summary>
		/// Indicates the Tempo of the stream
		/// </summary>
		public double Tempo {
			get {
				return MidiUtility.MicroTempoToTempo(MicroTempo);
			}
			set {
				MicroTempo = MidiUtility.TempoToMicroTempo(value);
			}
		}
		/// <summary>
		/// Indicates the TimeBase of the stream
		/// </summary>
		public short TimeBase {
			get {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				var tb = new MIDIPROPTIMEDIV();
				tb.cbStruct = Marshal.SizeOf(typeof(MIDIPROPTIMEDIV));
				_CheckOutResult(midiStreamProperty(_handle, ref tb, MIDIPROP_GET | MIDIPROP_TIMEDIV));
				return unchecked((short)tb.dwTimeDiv);
			}
			set {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The stream is closed.");
				var tb = new MIDIPROPTIMEDIV();
				tb.cbStruct = Marshal.SizeOf(typeof(MIDIPROPTIMEDIV));
				tb.dwTimeDiv = value;
				_CheckOutResult(midiStreamProperty(_handle, ref tb, MIDIPROP_SET | MIDIPROP_TIMEDIV));
			}
		}
		/// <summary>
		/// Indicates the volume of the device
		/// </summary>
		public MidiVolume Volume {
			get {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The device is closed.");
				int vol;
				_CheckOutResult(midiOutGetVolume(_handle, out vol));
				return new MidiVolume(unchecked((byte)(vol & 0xFF)), unchecked((byte)(vol >> 8)));
			}
			set {
				if (IntPtr.Zero == _handle)
					throw new InvalidOperationException("The device is closed.");
				_CheckOutResult(midiOutSetVolume(_handle, value.Right << 8 | value.Left));
			}
		}
		static string _GetMidiOutErrorMessage(int errorCode)
		{
			var result = new StringBuilder(256);
			midiOutGetErrorText(errorCode, result, result.Capacity);
			return result.ToString();
		}
		[System.Diagnostics.DebuggerNonUserCode()]
		static void _CheckOutResult(int errorCode)
		{
			if (0 != errorCode)
				throw new Exception(_GetMidiOutErrorMessage(errorCode));
		}
	}
}
