//
// Copyright (c) 2009-2011 Krueger Systems, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using MonoTouch.AVFoundation;
using MonoTouch.AudioToolbox;
using MonoTouch.Foundation;
using System.IO;
using System.Threading;
using MonoTouch.UIKit;
using System.Net;

namespace CommPadd
{


	public class AudioPlayerController : IDisposable
	{
		public string Url { get; private set; }
		
		AVAudioPlayer _player = null;
		
		static string _localPath = "";
		
		string _error = "";
		
		bool _playRequested = false;
		bool _stopDownloading = false;
		
		Thread _worker;
		
		AudioPlayerController (string url)
		{
			Url = url;
			try {
				_localPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				_localPath = Path.Combine(_localPath, "AudioBuffer.mp3");
				if (File.Exists(_localPath)) {
					File.Delete(_localPath);
				}
				StartDownloading();
			}
			catch (Exception ex) {
				_player = null;
				Console.WriteLine ("! " + ex.ToString());
			}
		}
		
		public bool IsPlaying {
			get {
				return (_player != null) && (_player.Playing);
			}
		}
		
		public static void Kill() {
			var i = Inst;
			if (i != null) {
				Inst = null;
				i._stopDownloading = true;
				Thread.Sleep(10);
				i._worker.Abort();
				i.Dispose();
				if (File.Exists(_localPath)) {
					File.Delete(_localPath);
				}				
			}
		}
		
		double? Progress;
		
		public string GetStatus(bool large) {
			if (_error != "") {
				return _error;
			}
			else if (_player != null) {
				if (_player.Playing) {
					var dur = TimeSpan.FromSeconds(_player.CurrentTime);
					var r = "";
					if (dur.TotalHours >= 1) {
						r = string.Format("{0}:{1:00}:{2:00}", dur.Hours, dur.Minutes, dur.Seconds);
					}
					else {
						r = string.Format("{0}:{1:00}", dur.Minutes, dur.Seconds);
					}
					if (large) {
						if (Progress.HasValue) {
							r += " (" + (int)(Progress.Value*100 + 0.5) + "%)";
						}
					}
					return r;
				}
				else {
					return "PAUSED";
				}
			}
			else if (_playRequested) {
				return "BUFFERING";
			}
			else {
				return "ERROR";
			}
		}
		
		public static AudioPlayerController Inst { get; private set; }
		
		public static AudioPlayerController Get(string url) {
			if (Inst == null || Inst.Url != url) {
				if (Inst != null) {
					Inst._stopDownloading = true;
					Inst.Stop();
					Inst.Dispose();
				}
				Inst = new AudioPlayerController(url);
			}
			
			return Inst;
		}
		
		void StartDownloading() {
			
			UIApplication.SharedApplication.IdleTimerDisabled = true;
			
			_worker = new Thread((ThreadStart)delegate {				
				var buffer = new byte[16 * 1024];
				
				var total = 0L;
				var downloadComplete = false;
				var retry = true;
				
				var contentLength = 0L;
				
				var minPlayBufferSize = 300000L;
					
				while (!downloadComplete && !_stopDownloading && retry) {
					Stream fout = null;

					try {
						
						//
						// Generate the request
						//
						System.Net.WebRequest req = null;
						
						if (total != 0) {
							try {
								total = new FileInfo(_localPath).Length;
							}
							catch (Exception) {
								total = 0;
							}
						}
						
						if (total == 0) {
							Console.WriteLine ("Requesting from the beginning");
							req = Http.NewRequest(Url);
						}
						else {
							Console.WriteLine ("Oh snap! We lost it at {0}, trying to resume", total);
							req = Http.NewRequest(Url);
							total = 0;
						}
						req.Timeout = 30000;
						
						//
						// Try to get the stream
						//
						using (var resp = req.GetResponse()) {
							
							contentLength = resp.ContentLength;
							
							using (var s = resp.GetResponseStream()) {
								
								//s.ReadTimeout

								//
								// We got the stream!
								//								
								if (total == 0) {
									fout = new FileStream(_localPath,
						                                 FileMode.Create,
						                                 FileAccess.Write,
						                                 FileShare.Read);
								}
								else {
									fout = new FileStream(_localPath,
						                                 FileMode.Append,
						                                 FileAccess.Write,
						                                 FileShare.Read);
								}								
						
								//
								// Keep downloading
								//
								var n = 1;
								
								while (n > 0 && !_stopDownloading) {
									n = s.Read(buffer, 0, buffer.Length);
									if (n > 0) {
										fout.Write(buffer, 0, n);
										fout.Flush();
										
										total += n;
										
										if (contentLength > 0) {
											Progress = (double)total/(double)contentLength;
										}
										
										if (_playRequested && _player == null && total > minPlayBufferSize) {
											_playRequested = false;										
											App.RunUI(delegate {
												StartPlaying();
											});
										}
									}
									else if (n == 0) {
										downloadComplete = true;
									}
									_error = "";
								}
							}
						}						
					}
					catch (WebException webex) {
						Console.WriteLine ("SOME WEB EXCEPTION==");
						Console.WriteLine (webex.Status);
						Console.WriteLine (webex);
						Console.WriteLine ("====================");
						_error = "NET ERR";
						downloadComplete = false;
						retry = CanResumeFromException(webex);
						System.Threading.Thread.Sleep(TimeSpan.FromSeconds(3));
					}
					finally {
						Progress = null;
						if (fout != null) {
							fout.Close();
						}
					}					
				}				
				
				App.RunUI(delegate {
					UIApplication.SharedApplication.IdleTimerDisabled = IsPlaying;
				});
			});
			_worker.Start();
		}
		
		static bool CanResumeFromException(WebException ex) {
			if (ex.Status == WebExceptionStatus.NameResolutionFailure) {
				return true;
			}
			else {
				return false;
			}
		}
		
		void StartPlaying() {
			if (_player == null) {
				NSError err;
				var url = NSUrl.FromFilename(_localPath);
				_player = AVAudioPlayer.FromUrl(url, out err);
				if (_player == null && err != null) {
					_error = "AUDIO ERR";
					Console.WriteLine ("! PLAY: {0}", err.LocalizedDescription);
				}
			}
			if (_player != null) {
				_player.Play();
			}
		}

		public void Stop ()
		{
			if (_player != null) {
				UIApplication.SharedApplication.IdleTimerDisabled = false;
				_player.Stop();
			}
		}
		
		public void Pause ()
		{
			if (_player != null) {
				_player.Pause();
			}
		}

		public void Play ()
		{
			if (_player != null) {
				_player.Play();
			}
			else {
				_playRequested = true;
			}
		}
		
		public void Dispose() {
			if (_player != null) {
					
				_player.Dispose();
				_player = null;
			}
		}
	}
}
