/*=========================================================================
 *  MapKit Version 2.1
 *  
 *  Lightweight data mapper framework
 *  
 *  Created by : Ariyanto
 *  Dec 2014
 *  
 *  Last Update : Aug 2016
 * 
 *  Under Apache Licence
 *  
 *  neonerdy@gmail.com
 *  
 * =========================================================================
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Configuration;

namespace MapKit
{
    //Attribute

    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; set; }
        public bool IsEntityRef { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class IdAttribute : ColumnAttribute
    {

    }


    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        public string Name { get; set; }
    }


    public class AttributeInfo
    {

        public static string GetTableName(Type entity)
        {
            string tableName = string.Empty;

            try
            {
                TableAttribute[] tables = (TableAttribute[])entity.GetCustomAttributes(typeof(TableAttribute), true);
                if (tables.Length > 0)
                {
                    if (tables[0].Name == null)
                    {
                        tableName = entity.Name;
                    }
                    else
                    {
                        tableName = tables[0].Name;
                    }
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message.ToString());
            }
            if (tableName.Equals(""))
            {
                throw new Exception("Table name in " + entity + " is empty");
            }
            return tableName;
        }
    }





    public interface IDataMapper<T>
    {
        T Map(IDataReader rdr);
    }

    public interface IRegistry : IDisposable
    {
        void Configure();
    }

    public interface IEntityManager : IDisposable
    {
        DataSource DataSource { get; }
        IDataReader ExecuteReader(string sql);
        int ExecuteNonQuery(string sql);
        object ExecuteScalar(string sql);
        T ExecuteObject<T>(string sql);
        List<T> ExecuteList<T>(string sql);
    }


    public class DataSource
    {
        private string provider;
        private string connectionString;

        public DataSource()
        {
        }


        public DataSource(string provider, string connectionString)
        {
            this.provider = provider;
            this.connectionString = connectionString;
        }

        public string Provider
        {
            get { return provider; }
        }

        public string ConnectionString
        {
            get { return connectionString; }
        }

    }



    public class ConnectionFactory
    {
        public static DbConnection CreateConnection(DataSource dataSource)
        {
            if (dataSource == null)
                throw new Exception("DataSource is null");

            DbConnection conn = null;
            try
            {
                DbProviderFactory factory = DbProviderFactories.GetFactory(dataSource.Provider);
                conn = factory.CreateConnection();
                conn.ConnectionString = dataSource.ConnectionString;
                conn.Open();
            }
            catch (DbException dbEx)
            {
                throw new Exception("Failed to connect to database server!");
            }
            catch (Exception ex)
            {
                throw;
            }
            return conn;
        }
    }



    public class Kit
    {
        private static Dictionary<object, Type> repositories = new Dictionary<object, Type>();
        private static object[] ctorDependency;
        private static object syncLock = new object();

        public static void RegisterObject<T>()
        {
            lock (syncLock)
            {
                if (!repositories.ContainsKey(typeof(T)))
                {
                    repositories.Add(typeof(T), typeof(T));
                }
            }
        }


        public static void RegisterObject<T>(object[] ctorDependency)
        {
            if (ctorDependency == null)
                throw new Exception("RegisterObject : Constructor depedency is null");

            lock (syncLock)
            {
                if (!repositories.ContainsKey(typeof(T)))
                {
                    repositories.Add(typeof(T), typeof(T));
                    Kit.ctorDependency = ctorDependency;
                }
            }
        }



        public static T GetObject<T>()
        {
            if (repositories[typeof(T)] == null)
                throw new Exception("GetObject : Type isn't already register yet");

            if (ctorDependency == null)
                throw new Exception("GetObject : Constructor depedency is null");

            T instance = default(T);
            try
            {
                if (ctorDependency != null)
                {
                    instance = (T)Activator.CreateInstance(repositories[typeof(T)], ctorDependency);
                }
                else
                {
                    instance = (T)Activator.CreateInstance(repositories[typeof(T)]);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("GetObject : Can't instantiate object", ex);
            }

            return instance;
        }


        public static void AddRegistry(IRegistry registry)
        {
            registry.Configure();
        }

    }



    public class EntityManager : IEntityManager, IDisposable
    {
        private DbConnection conn;
        private DataSource dataSource;




        public EntityManager(DataSource dataSource)
        {
            if (dataSource == null)
                throw new Exception("Data Source is null");

            try
            {
                this.dataSource = dataSource;

                if (conn == null)
                {
                    conn = ConnectionFactory.CreateConnection(dataSource);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        public DataSource DataSource
        {
            get { return dataSource; }
        }


        public IDataReader ExecuteReader(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                throw new Exception("Sql is empty");

            DbDataReader reader = null;
            DbCommand cmd = null;

            try
            {
                cmd = conn.CreateCommand();
                cmd.Connection = conn;
                cmd.CommandText = sql;
                reader = cmd.ExecuteReader();
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                if (cmd != null) cmd.Dispose();
            }
            return reader;
        }



        public int ExecuteNonQuery(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                throw new Exception("Sql is empty");

            int result = 0;
            DbCommand cmd = null;
            try
            {
                cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = sql;
                result = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                if (cmd != null) cmd.Dispose();
            }

            return result; ;
        }

        public object ExecuteScalar(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                throw new Exception("Sql is empty");

            object result = null;
            DbCommand cmd = null;

            try
            {
                cmd = conn.CreateCommand();
                cmd.Connection = conn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = sql;
                result = cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                if (cmd != null) cmd.Dispose();
            }
            return result; ;
        }


        public T MapRow<T>(IDataReader rdr, Type entity)
        {
            T instance = (T)Activator.CreateInstance(entity, true);

            PropertyInfo[] properties = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < properties.Length; i++)
            {
                ColumnAttribute[] columns = (ColumnAttribute[])properties[i].GetCustomAttributes(typeof(ColumnAttribute), true);

                if (columns.Length > 0)
                {
                    if (columns[0].Name == null)
                    {
                        object value = rdr[properties[i].Name];
                        properties[i].SetValue(instance, value, null);
                    }
                    else
                    {
                        if (rdr[columns[0].Name].GetType() != typeof(DBNull))
                        {
                            object value = rdr[columns[0].Name];
                            properties[i].SetValue(instance, value, null);
                        }
                    }
                }
            }
            return instance;
        }


        public T ExecuteObject<T>(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                throw new Exception("Sql is empty");


            Type entity = typeof(T);
            T obj = default(T);

            IDataReader rdr = null;
            DbCommand cmd = null;

            try
            {
                cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    obj = MapRow<T>(rdr, entity);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                if (cmd != null) cmd.Dispose();
                if (rdr != null) rdr.Close();
            }

            return obj;
        }



        public List<T> ExecuteList<T>(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                throw new Exception("Sql is empty");


            Type entity = typeof(T);

            List<T> list = new List<T>();
            IDataReader rdr = null;
            DbCommand cmd = null;

            try
            {
                cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                rdr = cmd.ExecuteReader();

                while (rdr.Read())
                {
                    object obj = MapRow<T>(rdr, entity);
                    list.Add((T)obj);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                if (cmd != null) cmd.Dispose();
                if (rdr != null) rdr.Close();
            }
            return list;
        }

        public void Dispose()
        {
            if (conn != null)
            {
                conn.Close();
                conn.Dispose();
            }
        }




    }



    public class EntityManagerFactory
    {
        public static IEntityManager CreateInstance(DataSource dataSource)
        {
            return new EntityManager(dataSource);
        }
    }



    public class Query
    {
        private StringBuilder q = new StringBuilder();
        private string tableName;
        private string columns;
        private string[] fields;

        public Query()
        {
            q.Append("SELECT");
        }

        public Query Select(string[] fields)
        {
            this.fields = fields;
            return this;
        }

        public Query From(string tableName)
        {
            this.tableName = tableName;
            return this;
        }


        public Query Where(string columnName)
        {
            q.Append(" WHERE " + columnName);
            return this;
        }

        public Query Equal(object value)
        {
            if (value.GetType() == typeof(string) || value.GetType() == typeof(Guid))
            {
                q.Append(" = '" + value + "'");
            }
            else
            {
                q.Append(" = " + value);
            }
            return this;
        }


        public Query Insert(object[] values)
        {
            string sql = "INSERT INTO " + this.tableName + " (";

            for (int i = 0; i <= this.fields.Length - 1; i++)
            {
                if (i == this.fields.Length - 1)
                {
                    sql = sql + this.fields[i] + ")";
                }
                else
                {
                    sql = sql + this.fields[i] + ",";
                }
            }

            sql = sql + " VALUES (";

            for (int i = 0; i <= values.Length - 1; i++)
            {              

                if (i == values.Length - 1)
                {
                    if (values[i].GetType() == typeof(string) || values[i].GetType() == typeof(char) ||
                     values[i].GetType() == typeof(Guid) ||
                     values[i].GetType() == typeof(Boolean))
                    {
                        sql = sql + "'" + values[i] + "')";
                    }
                    else if (values[i].GetType() == typeof(DateTime))
                    {
                        DateTime dt = DateTime.Now;
                        string dtFormat = "yyyy-MM-dd HH:MM:ss";
                                                
                        sql = sql + "'" + dt.ToString(dtFormat) + "')";
                    }
                    else
                    {
                        sql = sql + values[i] + ")";
                    }
                }
                else
                {
                    if (values[i].GetType() == typeof(string) || values[i].GetType() == typeof(char) ||
                     values[i].GetType() == typeof(Guid) ||
                     values[i].GetType() == typeof(Boolean))
                    {
                        sql = sql + "'" + values[i] + "',";
                    }
                    else if (values[i].GetType() == typeof(DateTime))
                    {
                        DateTime dt = DateTime.Now;
                        string dtFormat = "yyyy-MM-dd HH:MM:ss";

                        sql = sql + "'" + dt.ToString(dtFormat) + "',";
                    }

                    else
                    {
                        sql = sql + values[i] + ",";
                    }
                }
            }

            q.Remove(0, 6);
            q.Append(sql);

            return this;
        }



        public Query Update(object[] values)
        {
            string sql = "UPDATE " + tableName + " SET ";

            for (int i = 0; i <= this.fields.Length - 1; i++)
            {
                if (i == fields.Length - 1)
                {
                    if (values[i].GetType() == typeof(string) || values[i].GetType() == typeof(char) ||
                        values[i].GetType() == typeof(DateTime) || values[i].GetType() == typeof(Guid) ||
                        values[i].GetType() == typeof(Boolean))
                    {
                        sql = sql + this.fields[i] + "='" + values[i] + "'";
                    }
                    else
                    {
                        sql = sql + this.fields[i] + "=" + values[i] + "";
                    }
                }
                else
                {
                    if (values[i].GetType() == typeof(string) || values[i].GetType() == typeof(char) ||
                       values[i].GetType() == typeof(Guid) ||
                       values[i].GetType() == typeof(Boolean))
                    {
                        sql = sql + this.fields[i] + "='" + values[i] + "',";
                    }
                    else if (values[i].GetType() == typeof(DateTime))
                    {
                        DateTime dt = DateTime.Now;
                        string dtFormat = "yyyy-MM-dd HH:MM:ss";

                        sql = sql + "'" + dt.ToString(dtFormat) + "')";
                    }
                    else
                    {
                        sql = sql + this.fields[i] + "=" + values[i] + ",";
                    }
                }
            }

            q.Remove(0, 6);
            q.Append(sql);

            return this;
        }


        public Query Delete()
        {
            string sql = "DELETE FROM " + this.tableName;

            //q.Remove(0, 14 + this.tableName.Length);
            q.Append(sql);

            return this;
        }

        public string ToSql()
        {
            return q.ToString();
        }


    }


    public enum ActionType
    {
        Save, Update
    }

    public class DbHelper
    {
        private static DataSource dataSource;

        private static string provider = "";
        private static string connStr = "";

        public DbHelper(DataSource dataSource)
        {
            dataSource = dataSource;
        }

        public DbHelper()
        {
            if (provider == "" && connStr == string.Empty)
            {
                provider = ConfigurationManager.AppSettings["Provider"];
                connStr = ConfigurationManager.AppSettings["ConnectionString"];

                dataSource = new DataSource(provider, connStr);
            }
        }


        public IDataReader ExecuteReader(string sql)
        {
            IDataReader rdr = null;

            var em = EntityManagerFactory.CreateInstance(dataSource);
            rdr = em.ExecuteReader(sql);

            return rdr;
        }


        public void ExecuteNonQuery(string sql)
        {
            using (var em = EntityManagerFactory.CreateInstance(dataSource))
            {
                em.ExecuteNonQuery(sql);
            }
        }


        public object ExecuteScalar(string sql)
        {
            using (var em = EntityManagerFactory.CreateInstance(dataSource))
            {
                return em.ExecuteScalar(sql);
            }
        }



        public T GetById<T,P>(P id)
        {
            Type entity = typeof(T);
            T obj = default(T);
           
            string tableName = AttributeInfo.GetTableName(entity);

            try
            {
                using (var em = EntityManagerFactory.CreateInstance(dataSource))
                {
                    string sql = "SELECT * FROM " + tableName + " WHERE " + GetQueryClause(em.DataSource, entity, id);
                    obj = em.ExecuteObject<T>(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }

            return obj;
        }

      
     


        public List<T> GetAll<T>()
        {
            Type entity = typeof(T);
            List<T> list = new List<T>();
            string tableName = AttributeInfo.GetTableName(entity);

            try
            {
                using (var em = EntityManagerFactory.CreateInstance(dataSource))
                {
                    string sql = "SELECT * FROM " + tableName;
                    list = em.ExecuteList<T>(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }

            return list;
        }


        public List<T> GetAll<T>(string orderClause)
        {
            Type entity = typeof(T);
            List<T> list = new List<T>();
            string tableName = AttributeInfo.GetTableName(entity);

            try
            {
                using (var em = EntityManagerFactory.CreateInstance(dataSource))
                {
                    string sql = "SELECT * FROM " + tableName + " " + orderClause;
                    list = em.ExecuteList<T>(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }

            return list;
        }

                        

        public List<T> GetByCriteria<T>(string criteria)
        {
            Type entity = typeof(T);
            List<T> list = new List<T>();
            string tableName = AttributeInfo.GetTableName(entity);


            try
            {
                using (var em = EntityManagerFactory.CreateInstance(dataSource))
                {
                    string sql = "SELECT * FROM " + tableName + " WHERE " + criteria;
                    list = em.ExecuteList<T>(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }

            return list;
        }


        public List<T> GetByQuery<T>(string sql)
        {
            List<T> list = new List<T>();

            try
            {
                using (var em = EntityManagerFactory.CreateInstance(dataSource))
                {
                    list = em.ExecuteList<T>(sql);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }

            return list;
        }


        public void Save<T>(object obj)
        {
            List<string> columns;
            List<object> values;

            GenerateSql(obj, out columns, out values);
            SaveOrUpdate<T>(ActionType.Save, columns.ToArray(), values.ToArray());

        }

        public void Update<T>(object obj)
        {

            List<string> columns;
            List<object> values;

            GenerateSql(obj, out columns, out values);
            SaveOrUpdate<T>(ActionType.Update, columns.ToArray(), values.ToArray());
        }



        public void Delete<T,P>(P id)
        {
            Type entity = typeof(T);
            string tableName = AttributeInfo.GetTableName(entity);

            try
            {
                using (var em = EntityManagerFactory.CreateInstance(dataSource))
                {
                    var q = new Query().From(tableName).Delete().Where(GetQueryClause(em.DataSource, entity, id));
                    string sql = q.ToSql().Substring(6);

                    em.ExecuteNonQuery(sql);
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }
                
    
        private static void GenerateSql(object obj, out List<string> columns, out List<object> values)
        {
            PropertyInfo[] propertyInfos = obj.GetType().GetProperties();

            columns = new List<string>();
            values = new List<object>();

            int i = 0;
            foreach (var pi in propertyInfos)
            {
                ColumnAttribute[] columnAttribute = (ColumnAttribute[])pi.GetCustomAttributes(typeof(ColumnAttribute), true);

                if (columnAttribute.Length > 0)
                {
                    if (columnAttribute[0].IsEntityRef == false)
                    {
                        if (columnAttribute[0].Name == null)
                        {
                            columns.Add(pi.Name);
                        }
                        else
                        {
                            columns.Add(columnAttribute[0].Name);
                        }
                   
                        values.Add(pi.GetValue(obj, null));
                                           
                        i++;
                    }
                }
            }
        }


        private void SaveOrUpdate<T>(ActionType actionType, string[] columns, object[] values)
        {
            Type entity = typeof(T);
            List<T> list = new List<T>();
            string tableName = AttributeInfo.GetTableName(entity);

            try
            {
                using (var em = EntityManagerFactory.CreateInstance(dataSource))
                {
                    Query q = null;

                    if (actionType == ActionType.Save)
                    {
                        q = new Query().Select(columns).From(tableName).Insert(values);
                    }
                    else
                    {
                        string id = values[0].ToString();

                        q = new Query().Select(columns).From(tableName).Update(values)
                            .Where(GetQueryClause(em.DataSource, entity, id));
                    }

                    string sql = q.ToSql();

                    em.ExecuteNonQuery(sql); 
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }



        private string GetQueryClause(DataSource dataSource, Type entity, object keyValue)
        {
            PropertyInfo[] properties = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            string clause = "";

            for (int i = 0; i < properties.Length; i++)
            {
                IdAttribute[] keys = (IdAttribute[])properties[i].GetCustomAttributes(typeof(IdAttribute), false);
                if (keys.Length > 0)
                {
                    string id = string.Empty;
                    if (keys[0].Name == null)
                    {
                        id = properties[i].Name;
                    }
                    else
                    {
                        id = keys[0].Name;
                    }

                    if (dataSource.Provider == "System.Data.OleDb")
                    {
                        if (keyValue.GetType() == typeof(Guid)) 
                        {
                            clause = id + " = '{" + keyValue.ToString() + "}'";
                        }
                        else if (keyValue.GetType() == typeof(string))
                        {
                            clause = id + " = '" + keyValue.ToString() + "'";
                        }
                        else
                        {
                            clause = id + " = " + keyValue;
                        }
                    }
                    else if (dataSource.Provider == "System.Data.SqlClient"
                        || dataSource.Provider == "MySql.Data.MySqlClient")
                    {
                        if (keyValue.GetType() == typeof(Guid) || keyValue.GetType() == typeof(string))
                        {
                            clause = id + " = '" + keyValue + "'";
                        }
                        else
                        {
                            clause = id + " = " + keyValue;
                        }
                    }

                    break;
                }
            }
            return clause;
        }



    }


}