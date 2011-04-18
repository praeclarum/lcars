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
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.AudioToolbox;
using MonoTouch.AVFoundation;

namespace CommPadd
{
	public static class Sounds {
		
		static Dictionary<string,AVAudioPlayer> players = new Dictionary<string, AVAudioPlayer>();
		
		public static void PlayBeep() { Play("207"); }
		public static void PlayStartUp() { Play("201r"); }
		public static void PlayPendingCommand() { Play("218"); }
		public static void PlayConfirmPendingCommand() { Play("207"); }
		public static void PlayDataRetrieval() { Play("213"); }
		public static void PlayDataFound() { Play("214r"); }
		public static void PlayNotAllowed() { Play("225"); }
		public static void PlayNotActive() { Play("210"); }
		public static void PlayNewMessage() { Play("222"); }
		
		public static bool IsMuted { get; private set; }
		
		public static UIState UI { get; set; }
		
		public static void ToggleMute() {
			IsMuted = !IsMuted;
		}
		
		static Sounds() {
			IsMuted = false;
		}
		
		static string SoundPath(string name) {
			return "Sounds/" + name + ".caf";
		}
		
		static void Play(string name) {

			if (IsMuted) return;

			AVAudioPlayer p;
			if (!players.TryGetValue(name, out p)) {
				
				p = AVAudioPlayer.FromUrl(NSUrl.FromFilename(SoundPath(name)));
				
			}
			
			if (p != null) {
				
				if (!p.Playing) {
					p.Play();
				}
				
			}
		}		
		
	}
}
