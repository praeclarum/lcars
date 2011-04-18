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
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using System.Data;
using System.Text;

namespace Data
{
	/// <summary>
	/// Represents an open connection to a SQLite database.
	/// </summary>
	public class DbConnection : IDisposable
	{
		bool _open;
		Dictionary<string, TableMapping> _mappings = null;
		Dictionary<string, TableMapping> _tables = null;

		IDbConnection _conn;

		public bool Trace { get; set; }

		public Dbi Dbi { get; private set; }


		/// <summary>
		/// Constructs a new PostgresConnection and opens a SQLite database specified by databasePath.
		/// </summary>
		/// <param name="databasePath">
		/// Specifies the path to the database file.
		/// </param>
		public DbConnection (IDbConnection conn)
		{
			_conn = conn;
			if (conn.State == ConnectionState.Closed) {
				conn.Open ();
			}
			Dbi = Data.Dbi.GetForConnection (conn);
			_open = true;
		}
		
		public void Close() {
			if (_open) {
				try {
					_conn.Close();
				}
				catch (Exception) {
				}
			}
		}

		/// <summary>
		/// Returns the mappings from types to tables that the connection
		/// currently understands.
		/// </summary>
		public IEnumerable<TableMapping> TableMappings {
			get {
				if (_tables == null) {
					return Enumerable.Empty<TableMapping> ();
				} else {
					return _tables.Values;
				}
			}
		}

		/// <summary>
		/// Retrieves the mapping that is automatically generated for the given type.
		/// </summary>
		/// <param name="type">
		/// The type whose mapping to the database is returned.
		/// </param>
		/// <returns>
		/// The mapping represents the schema of the columns of the database and contains 
		/// methods to set and get properties of objects.
		/// </returns>
		public TableMapping GetMapping (Type type)
		{
			if (_mappings == null) {
				_mappings = new Dictionary<string, TableMapping> ();
			}
			TableMapping map;
			if (!_mappings.TryGetValue (type.FullName, out map)) {
				map = new TableMapping (type);
				_mappings[type.FullName] = map;
			}
			return map;
		}

		/// <summary>
		/// Executes a "create table if not exists" on the database. It also
		/// creates any specified indexes on the columns of the table. It uses
		/// a schema automatically generated from the specified type. You can
		/// later access this schema by calling GetMapping.
		/// </summary>
		/// <returns>
		/// The number of entries added to the database schema.
		/// </returns>
		public bool CreateTable<T> ()
		{
			return CreateTable(typeof(T));
		}
		public bool CreateTable (Type ty)
		{	
			if (_tables == null) {
				_tables = new Dictionary<string, TableMapping> ();
			}
			TableMapping map;
			if (!_tables.TryGetValue (ty.FullName, out map)) {
				map = GetMapping (ty);
				_tables.Add (ty.FullName, map);
			}
			var query = "create table " + Dbi.Quote (map.TableName) + "(\n";
			
			var decls = map.Columns.Select (p => Dbi.GetColumnDeclSql (p));
			var decl = string.Join (",\n", decls.ToArray ());
			query += decl;
			query += ")";
			
			var created = false;
			try {
				created = Execute (query) > 0;
			} catch (Exception ex) {
				if (Trace) {
					Console.WriteLine (ex.Message);
				}
			}
			
			foreach (var p in map.Columns.Where (x => x.IsIndexed)) {
				var indexName = map.TableName + "_" + p.Name;
				var q = string.Format ("create index {0} on {1}({2})", Dbi.Quote (indexName), Dbi.Quote (map.TableName), Dbi.Quote (p.Name));
				try {
					Execute (q);
				} catch (Exception ex) {
					if (Trace) {
						Console.WriteLine (ex.Message);
					}
				}
			}
			
			return created;
		}

		/// <summary>
		/// Creates a new IDbCommand given the command text with arguments. Place a '?'
		/// in the command text for each of the arguments.
		/// </summary>
		/// <param name="cmdText">
		/// The fully escaped SQL.
		/// </param>
		/// <param name="args">
		/// Arguments to substitute for the occurences of '?' in the command text.
		/// </param>
		/// <returns>
		/// A <see cref="IDbCommand"/>
		/// </returns>
		public IDbCommand CreateCommand (string cmdText, params object[] ps)
		{
			if (!_open) {
				throw new ApplicationException ("Cannot create commands from unopened database");
			} else {
				if (Trace) {
					Console.WriteLine (cmdText);
				}
				var cmd = _conn.CreateCommand ();
				var sb = new StringBuilder ();
				int p = 0;
				foreach (var ch in cmdText) {
					if (ch == '?') {
						sb.Append ("@p" + p);
						p++;
					} else {
						sb.Append (ch);
					}
				}
				cmd.CommandText = sb.ToString ();
				p = 0;
				foreach (var o in ps) {
					var pa = cmd.CreateParameter ();
					pa.ParameterName = "p" + p;
					pa.Value = o;
					cmd.Parameters.Add (pa);
					p++;
				}
				return cmd;
			}
		}

		/// <summary>
		/// Creates a IDbCommand given the command text (SQL) with arguments. Place a '?'
		/// in the command text for each of the arguments and then executes that command.
		/// Use this method instead of Query when you don't expect rows back. Such cases include
		/// INSERTs, UPDATEs, and DELETEs.
		/// You can set the Trace or TimeExecution properties of the connection
		/// to profile execution.
		/// </summary>
		/// <param name="query">
		/// The fully escaped SQL.
		/// </param>
		/// <param name="args">
		/// Arguments to substitute for the occurences of '?' in the query.
		/// </param>
		/// <returns>
		/// The number of rows modified in the database as a result of this execution.
		/// </returns>
		public int Execute (string query, params object[] args)
		{
			using (var cmd = CreateCommand (query, args)) {
				return cmd.ExecuteNonQuery ();
			}
		}

		/// <summary>
		/// Creates a IDbCommand given the command text (SQL) with arguments. Place a '?'
		/// in the command text for each of the arguments and then executes that command.
		/// It returns each row of the result using the mapping automatically generated for
		/// the given type.
		/// </summary>
		/// <param name="query">
		/// The fully escaped SQL.
		/// </param>
		/// <param name="args">
		/// Arguments to substitute for the occurences of '?' in the query.
		/// </param>
		/// <returns>
		/// An enumerable with one result for each row returned by the query.
		/// </returns>
		public IEnumerable<T> Query<T> (string query, params object[] args) where T : new()
		{
			var cmd = CreateCommand (query, args);
			return ExecuteQuery<T> (cmd);
		}
		
		public IEnumerable<T> Query<T>(SqlQuery sql) where T : new() {
			return Query<T>(sql.SqlText, sql.Arguments);
		}

		/// <summary>
		/// Creates a IDbCommand given the command text (SQL) with arguments. Place a '?'
		/// in the command text for each of the arguments and then executes that command.
		/// It returns each row of the result using the specified mapping. This function is
		/// only used by libraries in order to query the database via introspection. It is
		/// normally not used.
		/// </summary>
		/// <param name="map">
		/// A <see cref="TableMapping"/> to use to convert the resulting rows
		/// into objects.
		/// </param>
		/// <param name="query">
		/// The fully escaped SQL.
		/// </param>
		/// <param name="args">
		/// Arguments to substitute for the occurences of '?' in the query.
		/// </param>
		/// <returns>
		/// An enumerable with one result for each row returned by the query.
		/// </returns>
		public IEnumerable<object> Query (TableMapping map, string query, params object[] args)
		{
			var cmd = CreateCommand (query, args);
			return ExecuteQuery (map, cmd);
		}

		public IEnumerable<T> ExecuteQuery<T> (IDbCommand cmd) where T : new()
		{
			return ExecuteQuery (GetMapping (typeof(T)), cmd).Cast<T> ();
		}

		IEnumerable<object> ExecuteQuery (TableMapping map, IDbCommand cmd)
		{
			using (var reader = cmd.ExecuteReader ()) {
				
				var cols = new TableMapping.Column[reader.FieldCount];
				for (int i = 0; i < cols.Length; i++) {
					var name = reader.GetName (i);
					cols[i] = map.FindColumn (name);
				}
				
				while (reader.Read ()) {
					var obj = Activator.CreateInstance (map.MappedType);
					for (int i = 0; i < cols.Length; i++) {
						if (cols[i] == null)
							continue;
						object val = null;
						if (cols[i].ColumnType.IsEnum) {
							val = Convert.ChangeType (reader.GetValue (i), typeof(int));
						}
						else if (cols[i].ColumnType == typeof(TimeSpan)) {
							val = TimeSpan.FromSeconds(Convert.ToDouble(reader.GetValue(i)));
						}
						else if (cols[i].ColumnType == typeof(Guid)) {
							val = reader.GetValue(i);
							if (val is byte[]) {
								val = new Guid((byte[])val);
							}
						}
						else {
							val = Convert.ChangeType (reader.GetValue (i), cols[i].ColumnType);
						}
						cols[i].SetValue (obj, val);
					}
					yield return obj;
				}
				
			}
		}

		/// <summary>
		/// Returns a queryable interface to the table represented by the given type.
		/// </summary>
		/// <returns>
		/// A queryable object that is able to translate Where, OrderBy, and Take
		/// queries into native SQL.
		/// </returns>
		public TableQuery<T> Table<T> () where T : new()
		{
			return new TableQuery<T> (GetMapping(typeof(T)),
			                          query => Query<T>(query.Compile(Dbi)));
		}
		

		/// <summary>
		/// Attempts to retrieve an object with the given primary key from the table
		/// associated with the specified type. Use of this method requires that
		/// the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
		/// </summary>
		/// <param name="pk">
		/// The primary key.
		/// </param>
		/// <returns>
		/// The object with the given primary key. Throws a not found exception
		/// if the object is not found.
		/// </returns>
		public T Get<T> (object pk) where T : new()
		{
			var map = GetMapping (typeof(T));
			string query = string.Format ("select * from {0} where {1} = ?", Dbi.Quote (map.TableName), Dbi.Quote (map.PK.Name));
			return Query<T> (query, pk).First ();
		}

		/// <summary>
		/// Inserts all specified objects.
		/// </summary>
		/// <param name="objects">
		/// An <see cref="IEnumerable<T>"/> of the objects to insert.
		/// </param>
		/// <returns>
		/// The number of rows added to the table.
		/// </returns>
		public int InsertAll<T> (IEnumerable<T> objects)
		{
			var c = 0;
			foreach (var r in objects) {
				c += Insert (r);
			}
			return c;
		}

		/// <summary>
		/// Inserts the given object and retrieves its 
		/// auto incremented primary key if it has one.
		/// </summary>
		/// <param name="obj">
		/// The object to insert.
		/// </param>
		/// <returns>
		/// The number of rows added to the table.
		/// </returns>
		public int Insert (object obj)
		{
			if (obj == null) {
				return 0;
			}
			
			var map = GetMapping (obj.GetType ());
			var vals = from c in map.InsertColumns
				select c.GetValue (obj);
			
			var count = Execute (Dbi.GetInsertSql (map), vals.ToArray ());
			
			if (map.ContainsAutoIncPK) {
				object id = null;
				using (var cmd = _conn.CreateCommand ()) {
					cmd.CommandText = Dbi.GetLastInsertIdSql ();
					id = cmd.ExecuteScalar ();
				}
				
				map.SetAutoIncPK (obj, Convert.ToInt64 (id));
			}
			
			return count;
		}

		/// <summary>
		/// Updates all of the columns of a table using the specified object
		/// except for its primary key.
		/// The object is required to have a primary key.
		/// </summary>
		/// <param name="obj">
		/// The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
		/// </param>
		/// <returns>
		/// The number of rows updated.
		/// </returns>
		public int Update (object obj)
		{
			if (obj == null) {
				return 0;
			}
			
			var map = GetMapping (obj.GetType ());
			
			var pk = map.PK;
			
			if (pk == null) {
				throw new NotSupportedException ("Cannot update " + map.TableName + ": it has no PK");
			}
			
			var cols = from p in map.Columns
				where p != pk
				select p;
			var vals = from c in cols
				select c.GetValue (obj);
			var ps = new List<object> (vals);
			ps.Add (pk.GetValue (obj));
			var q = string.Format ("update {0} set {1} where {2} = ? ", Dbi.Quote (map.TableName), string.Join (",", (from c in cols
				select Dbi.Quote (c.Name) + " = ? ").ToArray ()), pk.Name);
			return Execute (q, ps.ToArray ());
		}

		public int UpdatePK (object obj, object newPK)
		{
			if (obj == null) {
				return 0;
			}
			
			var map = GetMapping (obj.GetType ());
			
			var pk = map.PK;
			
			if (pk == null) {
				throw new NotSupportedException ("Cannot update " + map.TableName + ": it has no PK");
			}
			
			var q = string.Format ("update {0} set {1} = ? where {2} = ? ", Dbi.Quote (map.TableName), Dbi.Quote (pk.Name), Dbi.Quote (pk.Name));
			var c = Execute (q, newPK, pk.GetValue (obj));
			pk.SetValue (obj, newPK);
			return c;
		}

		/// <summary>
		/// Deletes the given object from the database using its primary key.
		/// </summary>
		/// <param name="obj">
		/// The object to delete. It must have a primary key designated using the PrimaryKeyAttribute.
		/// </param>
		/// <returns>
		/// The number of rows deleted.
		/// </returns>
		public int Delete<T> (T obj)
		{
			var map = GetMapping (obj.GetType ());
			var pk = map.PK;
			if (pk == null) {
				throw new NotSupportedException ("Cannot delete " + map.TableName + ": it has no PK");
			}
			var q = string.Format ("delete from {0} where {1} = ?", Dbi.Quote (map.TableName), Dbi.Quote (pk.Name));
			return Execute (q, pk.GetValue (obj));
		}

		public void Dispose ()
		{
			_conn.Dispose ();
		}
	}

	public class PrimaryKeyAttribute : Attribute
	{
	}
	public class AutoIncrementAttribute : Attribute
	{
	}
	public class IndexedAttribute : Attribute
	{
	}
	public class MaxLengthAttribute : Attribute
	{
		public int Value { get; private set; }
		public MaxLengthAttribute (int length)
		{
			Value = length;
		}
	}

	public class TableMapping
	{
		public const int DefaultMaxStringLength = 140;

		public Type MappedType { get; private set; }
		public string TableName { get; private set; }

		public Column[] Columns { get; private set; }
		public Column PK { get; private set; }
		Column _autoPk = null;
		Column[] _insertColumns = null;

		public TableMapping (Type type)
		{
			MappedType = type;
			TableName = MappedType.Name;
			var props = MappedType.GetProperties ();
			var cols = new List<Column> ();
			foreach (var p in props) {
				if (p.CanWrite) {
					if (p.PropertyType.IsSubclassOf (typeof(DbConnection))) {
					} else {
						cols.Add (new PropColumn (p));
					}
				}
			}
			Columns = cols.ToArray ();
			foreach (var c in Columns) {
				if (c.IsAutoInc && c.IsPK) {
					_autoPk = c;
				}
				if (c.IsPK) {
					PK = c;
				}
			}
		}

		public bool ContainsAutoIncPK {
			get { return _autoPk != null; }
		}

		public void SetAutoIncPK (object obj, long id)
		{
			if (_autoPk != null) {
				_autoPk.SetValue (obj, Convert.ChangeType (id, _autoPk.ColumnType));
			}
		}

		public Column[] InsertColumns {
			get {
				if (_insertColumns == null) {
					_insertColumns = Columns.Where (c => !c.IsAutoInc).ToArray ();
				}
				return _insertColumns;
			}
		}
		public Column FindColumn (string name)
		{
			var exact = Columns.Where (c => c.Name == name).FirstOrDefault ();
			return exact;
		}

		public abstract class Column
		{
			public string Name { get; protected set; }
			public Type ColumnType { get; protected set; }
			public bool IsAutoInc { get; protected set; }
			public bool IsPK { get; protected set; }
			public bool IsIndexed { get; protected set; }
			public bool IsNullable { get; protected set; }
			public int MaxStringLength { get; protected set; }
			public abstract void SetValue (object obj, object val);
			public abstract object GetValue (object obj);
		}
		public class PropColumn : Column
		{
			PropertyInfo _prop;
			public PropColumn (PropertyInfo prop)
			{
				_prop = prop;
				Name = prop.Name;
				ColumnType = prop.PropertyType;
				IsAutoInc = TableMapping.IsAutoInc (prop);
				IsPK = TableMapping.IsPK (prop);
				IsIndexed = TableMapping.IsIndexed (prop);
				IsNullable = !IsPK;
				MaxStringLength = TableMapping.MaxStringLength (prop);
			}
			public override void SetValue (object obj, object val)
			{
				_prop.SetValue (obj, val, null);
			}
			public override object GetValue (object obj)
			{
				return _prop.GetValue (obj, null);
			}
		}
		public static bool IsPK (MemberInfo p)
		{
			var attrs = p.GetCustomAttributes (typeof(PrimaryKeyAttribute), true);
			return attrs.Length > 0;
		}

		public static bool IsAutoInc (MemberInfo p)
		{
			var attrs = p.GetCustomAttributes (typeof(AutoIncrementAttribute), true);
			return attrs.Length > 0;
		}

		public static bool IsIndexed (MemberInfo p)
		{
			var attrs = p.GetCustomAttributes (typeof(IndexedAttribute), true);
			return attrs.Length > 0;
		}

		public static int MaxStringLength (MemberInfo p)
		{
			var attrs = p.GetCustomAttributes (typeof(MaxLengthAttribute), true);
			if (attrs.Length > 0) {
				return ((MaxLengthAttribute)attrs[0]).Value;
			} else {
				return DefaultMaxStringLength;
			}
		}
	}

	public class MySqlDbi : Dbi
	{
		public override void InitConnection (IDbConnection conn)
		{
		}

		public override string Quote (string identifier)
		{
			return "`" + identifier + "`";
		}

		public override string GetColumnDeclSql (TableMapping.Column p)
		{
			string decl = Quote (p.Name) + " " + GetColumnSqlType (p) + " ";
			
			if (p.IsPK) {
				decl += "primary key ";
			}
			if (p.IsAutoInc) {
				decl += "auto_increment ";
			}
			if (!p.IsNullable) {
				decl += "not null ";
			}
			
			return decl;
		}

		public override string GetColumnSqlType (TableMapping.Column p)
		{
			var clrType = p.ColumnType;
			if (clrType == typeof(Boolean) || clrType == typeof(Byte) || clrType == typeof(UInt16) || clrType == typeof(SByte) || clrType == typeof(Int16) || clrType == typeof(Int32)) {
				return "integer";
			} else if (clrType == typeof(UInt32) || clrType == typeof(Int64)) {
				return "bigint";
			} else if (clrType == typeof(Single) || clrType == typeof(Double) || clrType == typeof(Decimal)) {
				return "float";
			} else if (clrType == typeof(String)) {
				int len = p.MaxStringLength;
				return "varchar(" + len + ")";
			} else if (clrType == typeof(DateTime)) {
				return "datetime";
			} else if (clrType.IsEnum) {
				return "integer";
			} else {
				throw new NotSupportedException ("Don't know about " + clrType);
			}
		}

		public override string GetLastInsertIdSql ()
		{
			return "select last_insert_id()";
		}
	}

	public class SqliteDbi : Dbi
	{
		public override void InitConnection (IDbConnection conn)
		{
		}

		public override string Quote (string identifier)
		{
			return "\"" + identifier + "\"";
		}

		public override string GetColumnDeclSql (TableMapping.Column p)
		{
			string decl = Quote (p.Name) + " " + GetColumnSqlType (p) + " ";
			
			if (p.IsPK) {
				decl += "primary key ";
			}
			if (p.IsAutoInc) {
				decl += "autoincrement ";
			}
			if (!p.IsNullable) {
				decl += "not null ";
			}
			
			return decl;
		}


		public override string GetColumnSqlType (TableMapping.Column p)
		{
			var clrType = p.ColumnType;
			if (clrType == typeof(Boolean) || clrType == typeof(Byte) || clrType == typeof(UInt16) || clrType == typeof(SByte) || clrType == typeof(Int16) || clrType == typeof(Int32)) {
				return "integer";
			} else if (clrType == typeof(UInt32) || clrType == typeof(Int64)) {
				return "bigint";
			} else if (clrType == typeof(Single) || clrType == typeof(Double) || clrType == typeof(Decimal)) {
				return "float";
			} else if (clrType == typeof(String)) {
				int len = p.MaxStringLength;
				return "varchar(" + len + ")";
			} else if (clrType == typeof(DateTime)) {
				return "datetime";
			} else if (clrType == typeof(TimeSpan)) {
				return "float";
			} else if (clrType == typeof(Guid)) {
				return "guid";
			} else if (clrType.IsEnum) {
				return "integer";
			} else {
				throw new NotSupportedException ("Don't know about " + clrType);
			}
		}

		public override string GetLastInsertIdSql ()
		{
			return "select last_insert_rowid()";
		}
		
	}

	public abstract class Dbi
	{
		public abstract void InitConnection (IDbConnection conn);

		public abstract string Quote (string identifier);

		public abstract string GetColumnDeclSql (TableMapping.Column p);

		public abstract string GetColumnSqlType (TableMapping.Column p);

		public abstract string GetLastInsertIdSql ();

		public virtual string GetInsertSql (TableMapping map)
		{
			var cols = map.InsertColumns;
			return string.Format ("insert into {0}({1}) values ({2})", Quote (map.TableName), string.Join (",", (from c in cols
				select Quote (c.Name)).ToArray ()), string.Join (",", (from c in cols
				select "?").ToArray ()));
		}

		public static Dbi GetForConnection (IDbConnection conn)
		{
			var tn = conn.GetType ().Name;
			
			Dbi r = null;
			
			if (tn == "SqliteConnection") {
				r = new SqliteDbi ();
			} else {
				r = new MySqlDbi ();
			}
			r.InitConnection (conn);
			return r;
		}
	}

	public class TableQuery<T> : IEnumerable<T> where T : new()
	{
		//public DbConnection Connection { get; private set; }
		//public Dbi Dbi { get; private set; }
		public TableMapping Table { get; private set; }
		public Func<TableQuery<T>,IEnumerable<T>> Execute { get; set; }

		Expression _where;
		List<Ordering> _orderBys;
		int? _limit;
		int? _offset;

		class Ordering
		{
			public string ColumnName { get; set; }
			public bool Ascending { get; set; }
		}

		public TableQuery (TableMapping table, Func<TableQuery<T>,IEnumerable<T>> exe)
		{
			//Connection = conn;
			//Dbi = Connection.Dbi;
			Table = table;
			Execute = exe;
		}

		public TableQuery<T> Clone ()
		{
			var q = new TableQuery<T> (Table, Execute);
			q._where = _where;
			if (_orderBys != null) {
				q._orderBys = new List<Ordering> (_orderBys);
			}
			q._limit = _limit;
			q._offset = _offset;
			return q;
		}

		public TableQuery<T> Where (Expression<Func<T, bool>> predExpr)
		{
			if (predExpr.NodeType == ExpressionType.Lambda) {
				var lambda = (LambdaExpression)predExpr;
				var pred = lambda.Body;
				var q = Clone ();
				q.AddWhere (pred);
				return q;
			} else {
				throw new NotSupportedException ("Must be a predicate");
			}
		}

		public TableQuery<T> Take (int n)
		{
			var q = Clone ();
			q._limit = n;
			return q;
		}

		public TableQuery<T> Skip (int n)
		{
			var q = Clone ();
			q._offset = n;
			return q;
		}

		public TableQuery<T> OrderBy<U> (Expression<Func<T, U>> orderExpr)
		{
			return AddOrderBy<U> (orderExpr, true);
		}

		public TableQuery<T> OrderByDescending<U> (Expression<Func<T, U>> orderExpr)
		{
			return AddOrderBy<U> (orderExpr, false);
		}

		private TableQuery<T> AddOrderBy<U> (Expression<Func<T, U>> orderExpr, bool asc)
		{
			if (orderExpr.NodeType == ExpressionType.Lambda) {
				var lambda = (LambdaExpression)orderExpr;
				var mem = lambda.Body as MemberExpression;
				if (mem != null && (mem.Expression.NodeType == ExpressionType.Parameter)) {
					var q = Clone ();
					if (q._orderBys == null) {
						q._orderBys = new List<Ordering> ();
					}
					q._orderBys.Add (new Ordering { ColumnName = mem.Member.Name, Ascending = asc });
					return q;
				} else {
					throw new NotSupportedException ("Order By does not support: " + orderExpr);
				}
			} else {
				throw new NotSupportedException ("Must be a predicate");
			}
		}

		private void AddWhere (Expression pred)
		{
			if (_where == null) {
				_where = pred;
			} else {
				_where = Expression.AndAlso (_where, pred);
			}
		}
		
		Dbi _dbi = null;
		string Quote(string s) {
			return _dbi.Quote(s);
		}

		public SqlQuery Compile (Dbi dbi)
		{
			_dbi = dbi;
			var cmdText = "select * from " + Quote (Table.TableName);
			var args = new List<object> ();
			if (_where != null) {
				var w = CompileExpr (_where, args);
				cmdText += " where " + w.CommandText;
			}
			if ((_orderBys != null) && (_orderBys.Count > 0)) {
				var t = string.Join (", ", _orderBys.Select (o => Quote (o.ColumnName) + (o.Ascending ? "" : " desc")).ToArray ());
				cmdText += " order by " + t;
			}
			if (_limit.HasValue) {
				cmdText += " limit " + _limit.Value;
			}
			if (_offset.HasValue) {
				cmdText += " offset " + _limit.Value;
			}
			return new SqlQuery (typeof(T).Name, cmdText, args.ToArray ());
		}

		class CompileResult
		{
			public string CommandText { get; set; }
			public object Value { get; set; }
		}

		CompileResult CompileExpr (Expression expr, List<object> queryArgs)
		{
			if (expr == null) {
				throw new NotSupportedException("Expression is NULL");
			} else if (expr is BinaryExpression) {
				var bin = (BinaryExpression)expr;
				
				var leftr = CompileExpr (bin.Left, queryArgs);
				var rightr = CompileExpr (bin.Right, queryArgs);
				
				var text = "(" + leftr.CommandText + " " + GetSqlName (bin) + " " + rightr.CommandText + ")";
				return new CompileResult { CommandText = text };
			} else if (expr.NodeType == ExpressionType.Call) {
				
				var call = (MethodCallExpression)expr;
				var args = new CompileResult[call.Arguments.Count];
				
				for (var i = 0; i < args.Length; i++) {
					args[i] = CompileExpr (call.Arguments[i], queryArgs);
				}
				
				var sqlCall = "";
				
				if (call.Method.Name == "Like" && args.Length == 2) {
					sqlCall = "(" + args[0].CommandText + " like " + args[1].CommandText + ")";
				}
				else {
					sqlCall = call.Method.Name.ToLower () + "(" + string.Join (",", args.Select (a => a.CommandText).ToArray ()) + ")";
				}
				return new CompileResult { CommandText = sqlCall };
				
			} else if (expr.NodeType == ExpressionType.Constant) {
				var c = (ConstantExpression)expr;
				queryArgs.Add (c.Value);
				//Console.WriteLine ("++ constant: {0}", c.Value);
				return new CompileResult { CommandText = "?", Value = c.Value };
			} else if (expr.NodeType == ExpressionType.Convert) {
				var u = (UnaryExpression)expr;
				var ty = u.Type;
				var valr = CompileExpr (u.Operand, queryArgs);
				return new CompileResult { CommandText = valr.CommandText, Value = valr.Value != null ? Convert.ChangeType (valr.Value, ty) : null };
			} else if (expr.NodeType == ExpressionType.MemberAccess) {
				var mem = (MemberExpression)expr;

				if (mem.Expression != null && mem.Expression.NodeType == ExpressionType.Parameter) {
					//
					// This is a column of our table, output just the column name
					//
					return new CompileResult { CommandText = Quote (mem.Member.Name) };
				} else {
					object obj = null;
					if (mem.Expression != null) {
						var r = CompileExpr (mem.Expression, queryArgs);
						if (r.Value == null) {
							throw new NotSupportedException ("Member access failed to compile expression");
						}
						if (r.CommandText == "?") {
							queryArgs.RemoveAt (queryArgs.Count - 1);
						}
						obj = r.Value;
					}
					
					if (mem.Member.MemberType == MemberTypes.Property) {
						var m = (PropertyInfo)mem.Member;
						var val = m.GetValue (obj, null);
						queryArgs.Add (val);
						//Console.WriteLine ("++ property: {0}", val);
						return new CompileResult { CommandText = "?", Value = val };
					} else if (mem.Member.MemberType == MemberTypes.Field) {
						var m = (FieldInfo)mem.Member;
						var val = m.GetValue (obj);
						queryArgs.Add (val);
						//Console.WriteLine ("++ field: {0}", val);
						return new CompileResult { CommandText = "?", Value = val };
					} else {
						throw new NotSupportedException ("MemberExpr: " + mem.Member.MemberType.ToString ());
					}
				}
			}
			throw new NotSupportedException ("Cannot compile: " + expr.NodeType.ToString ());
		}

		string GetSqlName (Expression expr)
		{
			var n = expr.NodeType;
			if (n == ExpressionType.GreaterThan)
				return ">";
			if (n == ExpressionType.GreaterThanOrEqual)
				return ">=";
			if (n == ExpressionType.LessThan)
				return "<";
			if (n == ExpressionType.LessThanOrEqual)
				return "<=";
			if (n == ExpressionType.And)
				return "and";
			if (n == ExpressionType.AndAlso)
				return "and";
			if (n == ExpressionType.Or)
				return "or";
			if (n == ExpressionType.OrElse)
				return "or";
			if (n == ExpressionType.Equal)
				return "=";
			if (n == ExpressionType.NotEqual)
				return "!=";
			if (n == ExpressionType.Subtract)
				return "-";
			if (n == ExpressionType.Add)
				return "+";
			else
				throw new System.NotSupportedException ("Cannot get SQL for: " + n.ToString ());
		}

		public IEnumerator<T> GetEnumerator ()
		{
			return Execute(this).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}
		
	}

	public class SqlQuery
	{
		public string TypeName { get; private set; }
		public string SqlText { get; private set; }
		public object[] Arguments { get; private set; }

		public SqlQuery ()
		{
			TypeName = "";
			SqlText = "";
			Arguments = new object[0];
		}

		public SqlQuery (string typeName, string commandText, object[] args)
		{
			TypeName = typeName;
			SqlText = commandText;
			Arguments = args;
		}

		public override bool Equals (object obj)
		{
			var q = obj as SqlQuery;
			if (q == null)
				return false;
			if (TypeName != q.TypeName)
				return false;
			if (SqlText != q.SqlText)
				return false;
			if (Arguments.Length != q.Arguments.Length)
				return false;
			for (int i = 0; i < Arguments.Length; i++) {
				if (!Arguments[i].Equals (q.Arguments[i]))
					return false;
			}
			return true;
		}

		public override int GetHashCode ()
		{
			var hash = TypeName.GetHashCode ();
			hash += SqlText.GetHashCode ();
			if (Arguments.Length > 0) {
				hash += Arguments[0].GetHashCode ();
			}
			return hash;
		}
	}
	
	public static class StringQ {
		public static bool Like(this string source, string query) {
			var re = new System.Text.RegularExpressions.Regex(query.Replace("%",".*?"));
			return re.Match(source).Success;
		}
	}
}
