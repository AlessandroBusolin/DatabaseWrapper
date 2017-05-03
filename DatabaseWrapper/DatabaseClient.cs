﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql;
using MySql.Data.MySqlClient;

namespace DatabaseWrapper
{
    public class DatabaseClient
    {
        #region Constructors

        /// <summary>
        /// Create an instance of the database client.
        /// </summary>
        /// <param name="dbType">The type of database.</param>
        /// <param name="serverIp">The IP address or hostname of the database server.</param>
        /// <param name="serverPort">The TCP port of the database server.</param>
        /// <param name="username">The username to use when authenticating with the database server.</param>
        /// <param name="password">The password to use when authenticating with the database server.</param>
        /// <param name="instance">The instance on the database server (for use with Microsoft SQL Server).</param>
        /// <param name="database">The name of the database with which to connect.</param>
        public DatabaseClient(
            DbTypes dbType,
            string serverIp,
            int serverPort,
            string username,
            string password,
            string instance,
            string database)
        {
            //
            // MsSql, MySql, and PostgreSql will use server IP, port, username, password, database
            // Sqlite will use just database and it should refer to the database file
            //
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < 0) throw new ArgumentOutOfRangeException(nameof(serverPort));
            if (String.IsNullOrEmpty(database)) throw new ArgumentNullException(nameof(database));
            
            DbType = dbType;
            ServerIp = serverIp;
            ServerPort = serverPort;
            Username = username;
            Password = password;
            Instance = instance;
            Database = database;

            if (!PopulateConnectionString())
            {
                throw new Exception("Unable to build connection string");
            }

            if (!LoadTableNames())
            {
                throw new Exception("Unable to load table names");
            }

            if (!LoadTableDetails())
            {
                throw new Exception("Unable to load table details from " + ServerIp + ":" + ServerPort + " " + Instance + " " + Database + " using username " + Username);
            }
        }

        /// <summary>
        /// Create an instance of the database client.
        /// </summary>
        /// <param name="dbType">The type of database.</param>
        /// <param name="serverIp">The IP address or hostname of the database server.</param>
        /// <param name="serverPort">The TCP port of the database server.</param>
        /// <param name="username">The username to use when authenticating with the database server.</param>
        /// <param name="password">The password to use when authenticating with the database server.</param>
        /// <param name="instance">The instance on the database server (for use with Microsoft SQL Server).</param>
        /// <param name="database">The name of the database with which to connect.</param>
        public DatabaseClient(
            string dbType,
            string serverIp,
            int serverPort,
            string username,
            string password,
            string instance,
            string database)
        {
            //
            // MsSql, MySql, and PostgreSql will use server IP, port, username, password, database
            // Sqlite will use just database and it should refer to the database file
            //
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < 0) throw new ArgumentOutOfRangeException(nameof(serverPort));
            if (String.IsNullOrEmpty(database)) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrEmpty(dbType)) throw new ArgumentNullException(nameof(dbType));

            switch (dbType.ToLower())
            {
                case "mssql":
                    DbType = DbTypes.MsSql;
                    break;

                case "mysql":
                    DbType = DbTypes.MySql;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(dbType));
            }

            ServerIp = serverIp;
            ServerPort = serverPort;
            Username = username;
            Password = password;
            Instance = instance;
            Database = database;

            if (!PopulateConnectionString())
            {
                throw new Exception("Unable to build connection string");
            }

            if (!LoadTableNames())
            {
                throw new Exception("Unable to load table names");
            }

            if (!LoadTableDetails())
            {
                throw new Exception("Unable to load table details from " + ServerIp + ":" + ServerPort + " " + Instance + " " + Database + " using username " + Username);
            }
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// The connection string used to connect to the database server.
        /// </summary>
        public string ConnectionString;

        /// <summary>
        /// Enable or disable console logging of raw queries generated by the library.
        /// </summary>
        public bool DebugRawQuery = false;

        /// <summary>
        /// Enable or disable console logging of returned row counts for successful queries run by the library.
        /// </summary>
        public bool DebugResultRowCount = false;

        #endregion

        #region Private-Members

        private DbTypes DbType;
        private string ServerIp;
        private int ServerPort;
        private string Username;
        private string Password;
        private string Instance;
        private string Database;

        private readonly object LoadingTablesLock = new object();
        private ConcurrentList<string> TableNames = new ConcurrentList<string>();
        private ConcurrentDictionary<string, List<Column>> TableDetails = new ConcurrentDictionary<string, List<Column>>();

        Random random = new Random();

        #endregion

        #region Public-Instance-Methods

        /// <summary>
        /// List all tables in the database.
        /// </summary>
        /// <returns></returns>
        public List<string> ListTables()
        {
            List<string> ret = new List<string>();
            if (TableNames != null && TableNames.Count > 0)
            {
                foreach (string curr in TableNames)
                {
                    ret.Add(curr);
                }
            }
            return ret;
        }

        /// <summary>
        /// Show the columns and column metadata from a specific table.
        /// </summary>
        /// <param name="tableName">The table to view.</param>
        /// <returns>A list of column objects.</returns>
        public List<Column> DescribeTable(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            List<Column> details;
            if (TableDetails.TryGetValue(tableName, out details))
            {
                return details;
            }
            else
            {
                throw new Exception("Table " + tableName + " is not in the tables list");
            }
        }

        /// <summary>
        /// Retrieve the name of the primary key column from a specific table.
        /// </summary>
        /// <param name="tableName">The table of which you want the primary key.</param>
        /// <returns>A string containing the column name.</returns>
        public string GetPrimaryKeyColumn(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            List<Column> details;
            if (TableDetails.TryGetValue(tableName, out details))
            {
                if (details != null && details.Count > 0)
                {
                    foreach (Column c in details)
                    {
                        if (c.IsPrimaryKey) return c.Name;
                    }
                }

                throw new Exception("Unable to find primary key for table " + tableName);
            }
            else
            {
                throw new Exception("Table " + tableName + " is not in the tables list");
            }
        }

        /// <summary>
        /// Retrieve a list of the names of columns from within a specific table.
        /// </summary>
        /// <param name="tableName">The table of which ou want to retrieve the list of columns.</param>
        /// <returns>A list of strings containing the column names.</returns>
        public List<string> GetColumnNames(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            List<Column> details;
            List<string> columnNames = new List<string>();

            if (TableDetails.TryGetValue(tableName, out details))
            {
                if (details != null && details.Count > 0)
                {
                    foreach (Column c in details)
                    {
                        columnNames.Add(c.Name);
                    }

                    return columnNames;
                }

                throw new Exception("Unable to find primary key for table " + tableName);
            }
            else
            {
                throw new Exception("Table " + tableName + " is not in the tables list");
            }
        }

        /// <summary>
        /// Returns a DataTable containing at most one row with data from the specified table where the specified column contains the specified value.  Should only be used on key or unique fields.
        /// </summary>
        /// <param name="tableName">The table from which you wish to SELECT.</param>
        /// <param name="columnName">The column containing key or unique fields where a match is desired.</param>
        /// <param name="value">The value to match in the key or unique field column.  This should be an object that can be cast to a string value.</param>
        /// <returns>A DataTable containing at most one row.</returns>
        public DataTable GetUniqueObjectById(string tableName, string columnName, object value)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (String.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (value == null) throw new ArgumentNullException(nameof(value));

            Expression e = new Expression
            {
                LeftTerm = columnName,
                Operator = Operators.Equals,
                RightTerm = value.ToString()
            };

            return Select(tableName, null, 1, null, e, null);
        }

        /// <summary>
        /// Execute a SELECT query.
        /// </summary>
        /// <param name="tableName">The table from which you wish to SELECT.</param>
        /// <param name="indexStart">The starting index for retrieval; used for pagination in conjunction with maxResults and orderByClause.  orderByClause example: ORDER BY created DESC.</param>
        /// <param name="maxResults">The maximum number of results to retrieve.</param>
        /// <param name="returnFields">The fields you wish to have returned.  Null returns all.</param>
        /// <param name="filter">The expression containing the SELECT filter (i.e. WHERE clause data).</param>
        /// <param name="orderByClause">Specify an ORDER BY clause if desired.</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable Select(string tableName, int? indexStart, int? maxResults, List<string> returnFields, Expression filter, string orderByClause)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            string innerQuery = "";
            string outerQuery = "";
            string whereClause = "";
            DataTable result;
            List<Column> tableDetails = DescribeTable(tableName);

            switch (DbType)
            {
                case DbTypes.MsSql:
                    #region MsSql

                    //
                    // select
                    //
                    if (indexStart != null)
                    {
                        if (String.IsNullOrEmpty(orderByClause)) throw new ArgumentNullException(nameof(orderByClause));
                        innerQuery = "SELECT ROW_NUMBER() OVER ( " + orderByClause + " ) AS __row_num__, ";
                    }
                    else
                    {
                        if (maxResults > 0) innerQuery += "SELECT TOP " + maxResults + " ";
                        else innerQuery += "SELECT ";
                    }
                    
                    //
                    // fields
                    //
                    if (returnFields == null || returnFields.Count < 1) innerQuery += "* ";
                    else
                    {
                        int fieldsAdded = 0;
                        foreach (string curr in returnFields)
                        {
                            if (fieldsAdded == 0)
                            {
                                innerQuery += SanitizeString(curr);
                                fieldsAdded++;
                            }
                            else
                            {
                                innerQuery += "," + SanitizeString(curr);
                                fieldsAdded++;
                            }
                        }
                    }
                    innerQuery += " ";

                    //
                    // table
                    //
                    innerQuery += "FROM " + tableName + " ";

                    //
                    // expressions
                    //
                    if (filter != null)
                    {
                        whereClause = filter.ToWhereClause(DbType);
                    }
                    if (!String.IsNullOrEmpty(whereClause))
                    {
                        innerQuery += "WHERE " + whereClause + " ";
                    }

                    // 
                    // order clause
                    //
                    if (indexStart == null)
                    {
                        if (!String.IsNullOrEmpty(orderByClause)) innerQuery += orderByClause + " ";
                    }

                    // 
                    // wrap in outer query
                    //
                    if (indexStart != null)
                    {
                        if (indexStart == 0)
                        {
                            outerQuery = "SELECT * FROM (" + innerQuery + ") AS row_constrained_result WHERE __row_num__ > " + indexStart + " ";
                            if (maxResults > 0) outerQuery += "AND __row_num__ <= " + (indexStart + maxResults) + " ";
                            outerQuery += "ORDER BY __row_num__ ";
                        }
                        else
                        {
                            outerQuery = "SELECT * FROM (" + innerQuery + ") AS row_constrained_result WHERE __row_num__ >= " + indexStart + " ";
                            if (maxResults > 0) outerQuery += "AND __row_num__ < " + (indexStart + maxResults) + " ";
                            outerQuery += "ORDER BY __row_num__ ";
                        }
                    }
                    else
                    {
                        outerQuery = innerQuery;
                    }
                    break;

                    #endregion

                case DbTypes.MySql:
                    #region MySql

                    //
                    // SELECT
                    //
                    outerQuery += "SELECT ";

                    //
                    // fields
                    //
                    if (returnFields == null || returnFields.Count < 1) outerQuery += "* ";
                    else
                    {
                        int fieldsAdded = 0;
                        foreach (string curr in returnFields)
                        {
                            if (fieldsAdded == 0)
                            {
                                outerQuery += SanitizeString(curr);
                                fieldsAdded++;
                            }
                            else
                            {
                                outerQuery += "," + SanitizeString(curr);
                                fieldsAdded++;
                            }
                        }
                    }
                    outerQuery += " ";

                    //
                    // table
                    //
                    outerQuery += "FROM " + tableName + " ";

                    //
                    // expressions
                    //
                    if (filter != null)
                    {
                        whereClause = filter.ToWhereClause(DbType);
                    }
                    if (!String.IsNullOrEmpty(whereClause))
                    {
                        outerQuery += "WHERE " + whereClause + " ";
                    }

                    // 
                    // order clause
                    //
                    if (!String.IsNullOrEmpty(orderByClause)) outerQuery += orderByClause + " ";

                    //
                    // limit
                    //
                    if (maxResults > 0)
                    {
                        if (indexStart != null && indexStart >= 0)
                        {
                            outerQuery += "LIMIT " + indexStart + "," + maxResults;
                        }
                        else
                        {
                            outerQuery += "LIMIT " + maxResults;
                        }
                    }

                    break;

                    #endregion
            }

            result = RawQuery(outerQuery);
            return result;
        }

        /// <summary>
        /// Execute an INSERT query.
        /// </summary>
        /// <param name="tableName">The table in which you wish to INSERT.</param>
        /// <param name="keyValuePairs">The key-value pairs for the row you wish to INSERT.</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable Insert(string tableName, Dictionary<string, object> keyValuePairs)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (keyValuePairs == null || keyValuePairs.Count < 1) throw new ArgumentNullException(nameof(keyValuePairs));

            string keys = "";
            string values = "";
            string query = "";
            int insertedId = 0;
            string retrievalQuery = "";
            DataTable result;
            List<Column> tableDetails = DescribeTable(tableName);
            List<string> columnNames = GetColumnNames(tableName);
            string primaryKeyColumn = GetPrimaryKeyColumn(tableName);

            #region Build-Key-Value-Pairs

            int added = 0;
            foreach (KeyValuePair<string, object> curr in keyValuePairs)
            {
                if (String.IsNullOrEmpty(curr.Key)) continue;
                if (!columnNames.Contains(curr.Key))
                {
                    throw new ArgumentException("Column " + curr.Key + " does not exist in table " + tableName);
                }

                if (added == 0)
                {
                    keys += curr.Key;
                    if (curr.Value != null)
                    {
                        if (curr.Value is DateTime || curr.Value is DateTime?)
                        {
                            switch (DbType)
                            {
                                case DbTypes.MsSql:
                                    values += "'" + DbTimestamp(DbTypes.MsSql, (DateTime)curr.Value) + "'";
                                    break;

                                case DbTypes.MySql:
                                    values += "'" + DbTimestamp(DbTypes.MySql, (DateTime)curr.Value) + "'";
                                    break;
                            }
                        }
                        else
                        {
                            if (Helper.IsExtendedCharacters(curr.Value.ToString()))
                            {
                                values += "N'" + SanitizeString(curr.Value.ToString()) + "'";
                            }
                            else
                            {
                                values += "'" + SanitizeString(curr.Value.ToString()) + "'";
                            }
                        }
                    }
                    else values += "null";
                }
                else
                {
                    keys += "," + curr.Key;
                    if (curr.Value != null)
                    {
                        if (curr.Value is DateTime || curr.Value is DateTime?)
                        {
                            switch (DbType)
                            {
                                case DbTypes.MsSql:
                                    values += ",'" + DbTimestamp(DbTypes.MsSql, (DateTime)curr.Value) + "'";
                                    break;

                                case DbTypes.MySql:
                                    values += ",'" + DbTimestamp(DbTypes.MySql, (DateTime)curr.Value) + "'";
                                    break;
                            }
                        }
                        else
                        {
                            if (Helper.IsExtendedCharacters(curr.Value.ToString()))
                            {
                                values += ",N'" + SanitizeString(curr.Value.ToString()) + "'";
                            }
                            else
                            {
                                values += ",'" + SanitizeString(curr.Value.ToString()) + "'";
                            }
                        }

                    }
                    else values += ",null";
                }
                added++;
            }

            #endregion

            #region Build-INSERT-Query-and-Submit

            switch (DbType)
            {
                case DbTypes.MsSql:
                    #region MsSql

                    query += "INSERT INTO " + tableName + " WITH (ROWLOCK) ";
                    query += "(" + keys + ") ";
                    query += "OUTPUT INSERTED.* ";
                    query += "VALUES ";
                    query += "(" + values + ") ";
                    break;

                    #endregion

                case DbTypes.MySql:
                    #region MySql

                    //
                    // insert into
                    //
                    query += "START TRANSACTION; ";
                    query += "INSERT INTO " + tableName + " ";
                    query += "(" + keys + ") ";
                    query += "VALUES ";
                    query += "(" + values + "); ";
                    query += "SELECT LAST_INSERT_ID() AS id; ";
                    query += "COMMIT; ";
                    break;

                    #endregion
            }

            result = RawQuery(query);

            #endregion

            #region Post-Retrieval

            switch (DbType)
            {
                case DbTypes.MsSql:
                    #region MsSql

                    //
                    // built into the query
                    //
                    break;

                    #endregion

                case DbTypes.MySql:
                    #region MySql

                    if (!Helper.DataTableIsNullOrEmpty(result))
                    {
                        bool idFound = false;

                        foreach (DataRow curr in result.Rows)
                        {
                            if (Int32.TryParse(curr["id"].ToString(), out insertedId))
                            {
                                idFound = true;
                                break;
                            }
                        }

                        if (!idFound)
                        {
                            result = null;
                        }
                        else
                        {
                            retrievalQuery = "SELECT * FROM " + tableName + " WHERE " + primaryKeyColumn + "=" + insertedId;
                            result = RawQuery(retrievalQuery);
                        }
                    }
                    break;

                    #endregion
            }

            #endregion

            return result;
        }

        /// <summary>
        /// Execute an UPDATE query.
        /// </summary>
        /// <param name="tableName">The table in which you wish to UPDATE.</param>
        /// <param name="keyValuePairs">The key-value pairs for the data you wish to UPDATE.</param>
        /// <param name="filter">The expression containing the UPDATE filter (i.e. WHERE clause data).</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable Update(string tableName, Dictionary<string, object> keyValuePairs, Expression filter)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (keyValuePairs == null || keyValuePairs.Count < 1) throw new ArgumentNullException(nameof(keyValuePairs));

            string query = "";
            string keyValueClause = "";
            DataTable result;
            List<Column> tableDetails = DescribeTable(tableName);
            List<string> columnNames = GetColumnNames(tableName);
            string primaryKeyColumn = GetPrimaryKeyColumn(tableName);

            #region Build-Key-Value-Clause

            int added = 0;
            foreach (KeyValuePair<string, object> curr in keyValuePairs)
            {
                if (String.IsNullOrEmpty(curr.Key)) continue;
                if (!columnNames.Contains(curr.Key))
                {
                    throw new ArgumentException("Column " + curr.Key + " does not exist in table " + tableName);
                }

                if (added == 0)
                {
                    if (curr.Value != null)
                    {
                        if (curr.Value is DateTime || curr.Value is DateTime?)
                        {
                            switch (DbType)
                            {
                                case DbTypes.MsSql:
                                    keyValueClause += curr.Key + "='" + DbTimestamp(DbTypes.MsSql, (DateTime)curr.Value) + "'";
                                    break;

                                case DbTypes.MySql:
                                    keyValueClause += curr.Key + "='" + DbTimestamp(DbTypes.MySql, (DateTime)curr.Value) + "'";
                                    break;
                            }
                        }
                        else
                        {
                            if (Helper.IsExtendedCharacters(curr.Value.ToString()))
                            {
                                keyValueClause += curr.Key + "=N'" + SanitizeString(curr.Value.ToString()) + "'";
                            }
                            else
                            {
                                keyValueClause += curr.Key + "='" + SanitizeString(curr.Value.ToString()) + "'";
                            }
                        }
                    }
                    else
                    {
                        keyValueClause += curr.Key + "= null";
                    }
                }
                else
                {
                    if (curr.Value != null)
                    {
                        if (curr.Value is DateTime || curr.Value is DateTime?)
                        {
                            switch (DbType)
                            {
                                case DbTypes.MsSql:
                                    keyValueClause += "," + curr.Key + "='" + DbTimestamp(DbTypes.MsSql, (DateTime)curr.Value) + "'";
                                    break;

                                case DbTypes.MySql:
                                    keyValueClause += "," + curr.Key + "='" + DbTimestamp(DbTypes.MySql, (DateTime)curr.Value) + "'";
                                    break;
                            }
                        }
                        else
                        {
                            if (Helper.IsExtendedCharacters(curr.Value.ToString()))
                            {
                                keyValueClause += "," + curr.Key + "=N'" + SanitizeString(curr.Value.ToString()) + "'";
                            }
                            else
                            {
                                keyValueClause += "," + curr.Key + "='" + SanitizeString(curr.Value.ToString()) + "'";
                            }
                        }
                    }
                    else
                    {
                        keyValueClause += "," + curr.Key + "= null";
                    }
                }
                added++;
            }

            #endregion

            #region Build-UPDATE-Query-and-Submit

            switch (DbType)
            {
                case DbTypes.MsSql:
                    #region MsSql

                    query += "UPDATE " + tableName + " WITH (ROWLOCK) SET ";
                    query += keyValueClause + " ";
                    query += "OUTPUT INSERTED.* ";
                    if (filter != null) query += "WHERE " + filter.ToWhereClause(DbType) + " ";
                    
                    break;

                    #endregion

                case DbTypes.MySql:
                    #region MySql

                    query += "UPDATE " + tableName + " SET ";
                    query += keyValueClause + " ";
                    if (filter != null) query += "WHERE " + filter.ToWhereClause(DbType) + " ";
                    break;

                    #endregion
            }

            result = RawQuery(query);

            #endregion
            
            return result;
        }

        /// <summary>
        /// Execute a DELETE query.
        /// </summary>
        /// <param name="tableName">The table in which you wish to DELETE.</param>
        /// <param name="filter">The expression containing the DELETE filter (i.e. WHERE clause data).</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable Delete(string tableName, Expression filter)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            string query = "";
            DataTable result;
            List<Column> tableDetails = DescribeTable(tableName);
            List<string> columnNames = GetColumnNames(tableName);
            string primaryKeyColumn = GetPrimaryKeyColumn(tableName);
            
            #region Build-DELETE-Query-and-Submit

            switch (DbType)
            {
                case DbTypes.MsSql:
                    #region MsSql

                    query += "DELETE FROM " + tableName + " WITH (ROWLOCK) ";
                    if (filter != null) query += "WHERE " + filter.ToWhereClause(DbType) + " ";
                    break;

                #endregion

                case DbTypes.MySql:
                    #region MySql

                    query += "DELETE FROM " + tableName + " ";
                    if (filter != null) query += "WHERE " + filter.ToWhereClause(DbType) + " ";
                    break;

                    #endregion
            }

            result = RawQuery(query);

            #endregion

            return result;
        }

        /// <summary>
        /// Empties a table completely.
        /// </summary>
        /// <param name="tableName">The table you wish to TRUNCATE.</param>
        public void Truncate(string tableName)
        {
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName)); 

            string query = "";
            DataTable result;

            switch (DbType)
            {
                case DbTypes.MsSql:
                    #region MsSql

                    query += "TRUNCATE TABLE " + tableName;
                    break;

                #endregion

                case DbTypes.MySql:
                    #region MySql

                    query += "TRUNCATE TABLE " + tableName;
                    break;

                    #endregion
            }

            result = RawQuery(query);

            return;
        }

        /// <summary>
        /// Execute a query.
        /// </summary>
        /// <param name="query">Database query defined outside of the database client.</param>
        /// <returns>A DataTable containing the results.</returns>
        public DataTable RawQuery(string query)
        {
            if (String.IsNullOrEmpty(query)) throw new ArgumentNullException(query);
            DataTable result = new DataTable();

            if (DebugRawQuery) Console.WriteLine("RawQuery: " + query);

            switch (DbType)
            {
                case DbTypes.MsSql:
                    using (SqlConnection conn = new SqlConnection(ConnectionString))
                    {
                        conn.Open();
                        SqlDataAdapter sda = new SqlDataAdapter(query, conn);
                        sda.Fill(result);
                        conn.Dispose();
                        conn.Close();
                    }
                    break;

                case DbTypes.MySql:
                    using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                    {
                        conn.Open();
                        MySqlCommand cmd = new MySqlCommand();
                        cmd.Connection = conn;
                        cmd.CommandText = query;
                        MySqlDataAdapter sda = new MySqlDataAdapter(cmd);
                        DataSet ds = new DataSet();
                        sda.Fill(ds);
                        if (ds != null)
                        {
                            if (ds.Tables != null)
                            {
                                if (ds.Tables.Count > 0)
                                {
                                    result = ds.Tables[0];
                                }
                            }
                        }
                        conn.Close();
                    }
                    break;
            }

            if (DebugResultRowCount)
            {
                if (result != null) Console.WriteLine("RawQuery: returning " + result.Rows.Count + " row(s)");
                else Console.WriteLine("RawQery: returning null");
            }
            return result;
        }

        /// <summary>
        /// Create a string timestamp from the given DateTime for the database of the instance type.
        /// </summary>
        /// <param name="ts">DateTime.</param>
        /// <returns>A string with timestamp formatted for the database of the instance type.</returns>
        public string Timestamp(DateTime ts)
        {
            switch (DbType)
            {
                case DbTypes.MsSql:
                    return ts.ToString("MM/dd/yyyy hh:mm:ss.fffffff tt");

                case DbTypes.MySql:
                    return ts.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

                default:
                    return null;
            }
        }

        #endregion

        #region Private-Instance-Methods

        private bool LoadTableNames()
        {
            lock (LoadingTablesLock)
            {
                string query = "";
                DataTable result = new DataTable();

                #region Build-Query

                switch (DbType)
                {
                    case DbTypes.MsSql:
                        query = "SELECT TABLE_NAME FROM " + Database + ".INFORMATION_SCHEMA.Tables WHERE TABLE_TYPE = 'BASE TABLE'";
                        break;

                    case DbTypes.MySql:
                        query = "SHOW TABLES";
                        break;
                }

                #endregion

                #region Process-Results

                result = RawQuery(query);
                List<string> tableNames = new List<string>();

                if (result != null && result.Rows.Count > 0)
                {
                    switch (DbType)
                    {
                        case DbTypes.MsSql:
                            foreach (DataRow curr in result.Rows)
                            {
                                tableNames.Add(curr["TABLE_NAME"].ToString());
                            }
                            break;

                        case DbTypes.MySql:
                            foreach (DataRow curr in result.Rows)
                            {
                                tableNames.Add(curr["Tables_in_" + Database].ToString());
                            }
                            break;
                    }
                }

                if (tableNames != null && tableNames.Count > 0)
                {
                    TableNames = new ConcurrentList<string>();
                    foreach (string curr in tableNames)
                    {
                        TableNames.Add(curr);
                    }
                }

                #endregion

                return true;
            }
        }

        private bool LoadTableDetails()
        {
            lock (LoadingTablesLock)
            {
                string query = "";
                DataTable result = new DataTable();
                Dictionary<string, List<Column>> tableDetails = new Dictionary<string, List<Column>>();

                foreach (string currTable in TableNames)
                {
                    #region Gather-Schema

                    List<Column> columns = new List<Column>();

                    switch (DbType)
                    {
                        case DbTypes.MsSql:
                            query =
                                "SELECT " +
                                "  col.TABLE_NAME, col.COLUMN_NAME, col.IS_NULLABLE, col.DATA_TYPE, col.CHARACTER_MAXIMUM_LENGTH, con.CONSTRAINT_NAME " +
                                "FROM INFORMATION_SCHEMA.COLUMNS col " +
                                "LEFT JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE con ON con.COLUMN_NAME = col.COLUMN_NAME " +
                                "WHERE col.TABLE_NAME='" + currTable + "' " +
                                "AND col.TABLE_CATALOG='" + Database + "'";
                            break;

                        case DbTypes.MySql:
                            query = "SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE " +
                                "TABLE_NAME='" + currTable + "' " +
                                "AND TABLE_SCHEMA='" + Database + "'";
                            break;
                    }

                    #endregion

                    #region Process-Schema

                    result = RawQuery(query);
                    if (result != null && result.Rows.Count > 0)
                    {
                        foreach (DataRow currColumn in result.Rows)
                        {
                            #region Process-Each-Column

                            /*
                            public bool IsPrimaryKey;
                            public string Name;
                            public string DataType;
                            public int? MaxLength;
                            public bool Nullable;
                            */
                            Column tempColumn = new Column();
                            int maxLength = 0;

                            switch (DbType)
                            {
                                case DbTypes.MsSql:
                                    tempColumn.Name = currColumn["COLUMN_NAME"].ToString();
                                    if (currColumn["CONSTRAINT_NAME"].ToString().StartsWith("PK_")) tempColumn.IsPrimaryKey = true;
                                    else tempColumn.IsPrimaryKey = false;
                                    tempColumn.DataType = currColumn["DATA_TYPE"].ToString();
                                    if (!Int32.TryParse(currColumn["CHARACTER_MAXIMUM_LENGTH"].ToString(), out maxLength)) { tempColumn.MaxLength = null; }
                                    else tempColumn.MaxLength = maxLength;
                                    if (String.Compare(currColumn["IS_NULLABLE"].ToString(), "YES") == 0) tempColumn.Nullable = true;
                                    else tempColumn.Nullable = false;
                                    break;

                                case DbTypes.MySql:
                                    tempColumn.Name = currColumn["COLUMN_NAME"].ToString();
                                    if (String.Compare(currColumn["COLUMN_KEY"].ToString(), "PRI") == 0) tempColumn.IsPrimaryKey = true;
                                    else tempColumn.IsPrimaryKey = false;
                                    tempColumn.DataType = currColumn["DATA_TYPE"].ToString();
                                    if (!Int32.TryParse(currColumn["CHARACTER_MAXIMUM_LENGTH"].ToString(), out maxLength)) { tempColumn.MaxLength = null; }
                                    else tempColumn.MaxLength = maxLength;
                                    if (String.Compare(currColumn["IS_NULLABLE"].ToString(), "YES") == 0) tempColumn.Nullable = true;
                                    else tempColumn.Nullable = false;
                                    break;
                            }

                            columns.Add(tempColumn);

                            #endregion
                        }

                        tableDetails.Add(currTable, columns);
                    }

                    #endregion
                }

                #region Replace-Table-Details

                TableDetails = new ConcurrentDictionary<string, List<Column>>();
                foreach (KeyValuePair<string, List<Column>> curr in tableDetails)
                {
                    TableDetails.TryAdd(curr.Key, curr.Value);
                }

                #endregion

                return true;
            }
        }

        private bool PopulateConnectionString()
        {
            ConnectionString = "";

            switch (DbType)
            {
                case DbTypes.MsSql:
                    //
                    // http://www.connectionstrings.com/sql-server/
                    //
                    if (String.IsNullOrEmpty(Username) && String.IsNullOrEmpty(Password))
                    {
                        ConnectionString += "Data Source=" + ServerIp;
                        if (!String.IsNullOrEmpty(Instance)) ConnectionString += "\\" + Instance + "; ";
                        else ConnectionString += "; ";
                        ConnectionString += "Integrated Security=SSPI; ";
                        ConnectionString += "Initial Catalog=" + Database + "; ";                            
                    }
                    else
                    {
                        if (ServerPort > 0)
                        {
                            if (String.IsNullOrEmpty(Instance)) ConnectionString += "Server=" + ServerIp + "," + ServerPort + "; ";
                            else ConnectionString += "Server=" + ServerIp + "\\" + Instance + "," + ServerPort + "; ";
                        }
                        else
                        {
                            if (String.IsNullOrEmpty(Instance)) ConnectionString += "Server=" + ServerIp + "; ";
                            else ConnectionString += "Server=" + ServerIp + "\\" + Instance + "; ";
                        }

                        ConnectionString += "Database=" + Database + "; ";
                        if (!String.IsNullOrEmpty(Username)) ConnectionString += "User ID=" + Username + "; ";
                        if (!String.IsNullOrEmpty(Password)) ConnectionString += "Password=" + Password + "; ";
                    }
                    break;

                case DbTypes.MySql:
                    //
                    // http://www.connectionstrings.com/mysql/
                    //
                    // MySQL does not use 'Instance'
                    ConnectionString += "Server=" + ServerIp + "; ";
                    if (ServerPort > 0) ConnectionString += "Port=" + ServerPort + "; ";
                    ConnectionString += "Database=" + Database + "; ";
                    if (!String.IsNullOrEmpty(Username)) ConnectionString += "Uid=" + Username + "; ";
                    if (!String.IsNullOrEmpty(Password)) ConnectionString += "Pwd=" + Password + "; ";
                    break;
            }

            return true;
        }

        private string SanitizeString(string s)
        {
            if (String.IsNullOrEmpty(s)) return String.Empty;
            string ret = "";
            int doubleDash = 0;
            int openComment = 0;
            int closeComment = 0;

            switch (DbType)
            {
                case DbTypes.MsSql:
                    #region MsSql

                    //
                    // null, below ASCII range, above ASCII range
                    //
                    for (int i = 0; i < s.Length; i++)
                    {
                        if (((int)(s[i]) == 10) ||      // Preserve carriage return
                            ((int)(s[i]) == 13))        // and line feed
                        {
                            ret += s[i];
                        }
                        else if ((int)(s[i]) < 32)
                        {
                            continue;
                        }
                        else
                        {
                            ret += s[i];
                        }
                    }

                    //
                    // double dash
                    //
                    doubleDash = 0;
                    while (true)
                    {
                        doubleDash = ret.IndexOf("--");
                        if (doubleDash < 0)
                        {
                            break;
                        }
                        else
                        {
                            ret = ret.Remove(doubleDash, 2);
                        }
                    }

                    //
                    // open comment
                    // 
                    openComment = 0;
                    while (true)
                    {
                        openComment = ret.IndexOf("/*");
                        if (openComment < 0) break;
                        else 
                        {
                            ret = ret.Remove(openComment, 2);
                        }
                    }

                    //
                    // close comment
                    //
                    closeComment = 0;
                    while (true)
                    {
                        closeComment = ret.IndexOf("*/");
                        if (closeComment < 0) break;
                        else
                        {
                            ret = ret.Remove(closeComment, 2);
                        }
                    }

                    //
                    // in-string replacement
                    //
                    ret = ret.Replace("'", "''");
                    break;

                    #endregion

                case DbTypes.MySql:
                    #region MySql

                    ret = MySqlHelper.EscapeString(s);
                    break;

                    #endregion
            }

            return ret;
        }

        #endregion

        #region Public-Static-Methods

        /// <summary>
        /// Convert a DateTime to a string formatted for the specified database type.
        /// </summary>
        /// <param name="dbType">The type of database.</param>
        /// <param name="dt">The timestamp.</param>
        /// <returns>A string formatted for use with the specified database.</returns>
        public static string DbTimestamp(DbTypes dbType, DateTime ts)
        {
            switch (dbType)
            {
                case DbTypes.MsSql:
                    return ts.ToString("MM/dd/yyyy hh:mm:ss.fffffff tt");

                case DbTypes.MySql:
                    return ts.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

                default:
                    return null;
            }
        }

        /// <summary>
        /// Convert a DateTime to a string formatted for the specified database type.
        /// </summary>
        /// <param name="dbType">The type of database.</param>
        /// <param name="dt">The timestamp.</param>
        /// <returns>A string formatted for use with the specified database.</returns>
        public static string DbTimestamp(string dbType, DateTime ts)
        {
            if (String.IsNullOrEmpty(dbType)) throw new ArgumentNullException(nameof(dbType));
            switch (dbType.ToLower())
            {
                case "mssql":
                    return DbTimestamp(DbTypes.MsSql, ts);

                case "mysql":
                    return DbTimestamp(DbTypes.MySql, ts);

                default:
                    throw new ArgumentOutOfRangeException(nameof(dbType));
            }
        }

        #endregion
    }
}
