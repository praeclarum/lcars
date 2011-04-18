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
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace CommPadd
{

	public class Repo : IDisposable {
		DbConnection db;
		
		public static Repo Foreground;
		
		public static void CreateForeground() {
			Foreground = new Repo();
			Foreground.CreateTables();
		}
		
		public Repo() {			
			var conn = new Mono.Data.Sqlite.SqliteConnection("Data Source=" + Path);
			db = new DbConnection(conn);
		}
		
		static string Path;
		static Repo() {
			Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			Path = System.IO.Path.Combine(Path, "CommPadd.sqlite");
			Console.WriteLine (Path);
		}
		
		public void AddOrActivateSource(Source source) {
			var sourceType = source.GetType();
			var srcs = GetSources(sourceType);
			var q = from s in srcs where s.Matches(source) select s;
			var prev = q.FirstOrDefault();
			
			if (prev == null) {
				Insert(source);
			}
			else {
				source.Id = prev.Id;
				source.IsActive = true;
				Update(source);
			}
		}
		
		public void AddSource(Source source) {
			Insert(source);
		}
		
		public TableQuery<T> Table<T>() where T : new() {
			return db.Table<T>();
		}
		
		void CreateTables() {
			foreach (var t in SourceTypes.All) {
				db.CreateTable(t);
			}
			db.CreateTable<Message>();
			db.CreateTable<UIState>();
			db.CreateTable<ShareMessage>();
			db.CreateTable<ShareInfo>();
			db.CreateTable<GoogleReaderConfig>();
			db.CreateTable<TwitterOAuthTokens>();
		}
		
		public IEnumerable<object> GetAll(Type type) {
			return db.Query(db.GetMapping(type), "select * from " + db.Dbi.Quote(type.Name));
		}
		public void Insert(object o) {
			db.Insert(o);
		}
		public void Update(object o) {
			db.Update(o);
		}
		
		class JustId {			
			public int Id { get; set; }
		}
		
		public void RemoveSource(Source s) {
			s.IsActive = false;
			Update(s);
			var ids = db.Query<JustId>("select Id from Message where SourceId = ? and SourceType = ?", s.Id, s.GetType().Name).ToArray();
			foreach (var id in ids) {
				db.Execute("delete from Message where Id = ?", id.Id);
			}			
		}
		
		public Source[] GetActiveSources() {
			var sources = new List<Source>();
			foreach (var t in SourceTypes.All) {
				var os = GetActiveSources(t);
				sources.AddRange(os);
			}
			return sources.ToArray();
		}
				
		public Source GetActiveSource(string typeName, int id) {
			var q = from s in GetActiveSources(SourceTypes.Get(typeName))
					where s.Id == id
					select s;
			return q.FirstOrDefault();
		}
		
		public Source[] GetActiveSources(Type type) {
			return GetAll(type)
					.Cast<Source>()
					.Where(s => s.IsActive)
					.OrderBy(s => s.GetDistinguisher())
					.ToArray();
		}
		
		public Source[] GetSources(Type type) {
			return GetAll(type)
					.Cast<Source>()
					.OrderBy(s => s.GetDistinguisher())
					.ToArray();
		}
		
		public Type[] GetActiveSourceTypes() {
			var q = from t in SourceTypes.All
					let os = GetActiveSources(t)
					where os.Length > 0
					select t;
			return q.ToArray();
		}
		
		public void GetReadStatus(Type sourceType) {
		}
		
		public void Dispose() {
			db.Close();
		}

		public MessageRef[] GetRecentMessages (CommPadd.Source src)
		{
			var st = src.GetType().Name;
			var sql = @"select ""Id"",""Subject"",""PublishTime"",""SourceType"",""SourceId"",""IsRead"",""From"" from Message where SourceType=? and SourceId=? order by PublishTime desc limit 200";
			return db.Query<MessageRef>(sql, st, src.Id).ToArray();
		}

		public Message Resolve (MessageRef mref)
		{
			return Table<Message>().Where(m => m.Id == mref.Id).FirstOrDefault();
		}
		
		public Source Resolve (SourceRef sref)
		{
			return GetActiveSource(sref.SourceType, sref.Id);
		}
	}	
}
