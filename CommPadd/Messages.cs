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
using Data;
using System.Linq;
using System.Xml.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using System.Threading;

namespace CommPadd
{
	public class MessageRef : IMessage {
		public int Id { get; set; }
		public string SourceType { get; set; }
		public int SourceId { get; set; }
		public DateTime PublishTime { get; set; }
		public bool IsRead { get; set; }
		public string From { get; set; }
		public string Subject { get; set; }
	}

	public class Message : IMessage {
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed]
		public string SourceType { get; set; }
		[Indexed]
		public int SourceId { get; set; }
		[Indexed]
		public string RawId { get; set; }
		[Indexed]
		public DateTime PublishTime { get; set; }
		public bool IsRead { get; set; }
		public string BodyHtml { get; set; }
		public string From { get; set; }
		public string Subject { get; set; }
		public string MediaUrl { get; set; }
		public string Url { get; set; }
		
		public bool HasMedia { 
			get {
				return (MediaUrl.Length > 0);
			} 
		}
		public bool HasSubject { get { return Subject.Length > 0; } }
		
		public MessageRef Reference {
			get {
				return new MessageRef() {
					Id = Id,
					SourceType = SourceType,
					SourceId = SourceId,
					PublishTime = PublishTime,
					IsRead = IsRead,
					From = From,
					Subject = Subject
				};
			}
		}
		
		public Message() {
			SourceType = "";
			RawId = "";
			BodyHtml = "";
			From = "";
			Subject = "";
			MediaUrl = "";
			Url = "";
			IsRead = false;
		}
		
		
	}
	
	public interface IMessage {
		int Id { get; }
		int SourceId { get; }
		string SourceType { get; }
		string Subject { get; }
	}
	
	public static class MessageEx {
		public static SourceRef GetSourceReference(this IMessage m) {
			return new SourceRef() {
				Id = m.SourceId,
				SourceType = m.SourceType
			};
		}
		
		public static Source GetSource(this IMessage m, Source[] srcs) {
			foreach (var s in srcs) {
				if (s.Id == m.SourceId && s.GetType().Name == m.SourceType) {
					return s;					
				}
			}
			return null;
		}
		public static string GetTextSummary(this IMessage m) {
			return m.Subject;
		}
	}
	
	public static class StringEx {
		static char[] WS = new char[] { ' ', '\t', '\n', '\r', '\b' };
		
		public static string[] GetWords(this string s) {
			return s.Split(WS, StringSplitOptions.RemoveEmptyEntries);
		}
		public static string TruncateChars(this string s, int maxLength) {
			if (s.Length < maxLength) return s;
			else return s.Substring(0, maxLength);
		}
		public static string TruncateWords(this string s, int maxLength) {
			var b = new StringBuilder();
			var words = s.GetWords();
			var head = "";
			var i = 0;
			while ((i < words.Length) && ((b.Length + head.Length + words[i].Length) <= maxLength)) {
				b.Append(head);
				b.Append(words[i]);
				head = " ";
				i++;				
			}
			return b.ToString();
		}
	}
}
