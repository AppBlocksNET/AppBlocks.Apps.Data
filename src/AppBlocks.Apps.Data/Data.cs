using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;

namespace AppBlocks.Apps
{
    public class Data : IApp
    {
        private static string Namespace => typeof(Data).Namespace;

        public enum DatabaseType
        {
            Access,
            SqlServer,
            Oracle
            // any other data source type
        }

        public enum ParameterType
        {
            Integer,
            Char,
            VarChar
            // define a common parameter type set
        }

        public List<Dictionary<string, object>> App(Dictionary<string, object> parameters = null)
        {
            var results = new List<Dictionary<string, object>>();

            var source = parameters != null && parameters.Count > 0 && parameters.Keys.Contains("source") ? parameters["source"].ToString() : null;

            if (string.IsNullOrEmpty(source))
            {
                source = "select * from items order by created";
                //return results;
            }

            var commandString = source;
            //var connectionString = parameters != null && parameters.ContainsKey("connectionString") ? parameters.TryGetValue([connectionString") ? "";
            var connectionString = "";
            if (parameters != null && parameters.ContainsKey("connectionString"))
                connectionString = parameters["connectionString"].ToString();

            if (string.IsNullOrEmpty(connectionString) || !connectionString.Contains(";"))
            {
                //var connect = TypeResolver.Resolve<IConnect>("AppBlocks.Connect.AppSettings.Provider");
                //if (connect != null)
                //{
                if (string.IsNullOrEmpty(connectionString))
                {
                    //connectionString = provider.GetSetting("AppBlocks.Settings.ConnectionString", "AppBlocks");
                    connectionString = GetConnectionString(new[] { connectionString });
                }
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                return results;//default(T); //null;
            }

            try
            {
                using (var dbConnection = CreateConnection(connectionString))
                {
                    using (var dbCommand = CreateCommand(commandString, dbConnection))
                    {
                        if (parameters != null)
                        {
                            foreach (var p in parameters.Where(p => !p.Key.EndsWith(".Property.Connect.Source") && p.Key != "connectionString"))
                            {
                                dbCommand.Parameters.Add(new SqlParameter(p.Key, p.Value.ToString() != "null" ? p.Value : DBNull.Value));
                            }
                        }
                        var dataReader = dbCommand.ExecuteReader();

                        var resultsList = new List<Dictionary<string, object>>();
                        var resultsDictionary = new Dictionary<string, object>();

                        while (dataReader.Read())
                        {
                            if (dataReader.FieldCount == 1)
                            {
                                var fieldName = dataReader.GetName(0);
                                if (string.IsNullOrEmpty(fieldName)) fieldName = "results";
                                resultsDictionary.Add(fieldName, dataReader.GetValue(0).ToString());
                            }
                            else
                            {
                                if (dataReader.FieldCount == 2 &&
                                    ((dataReader.GetName(0) == "Name" &&
                                    dataReader.GetName(1) == "Data") || (dataReader.GetName(0) == "Id" &&
                                    dataReader.GetName(1) == "Name")))
                                {
                                    resultsDictionary.Add(dataReader.GetValue(0).ToString(), dataReader.GetValue(1).ToString());
                                }
                                else
                                {
                                    var fieldsDictionary = new Dictionary<string, object>();
                                    for (var i = 0; i < dataReader.FieldCount; i++)
                                    {
                                        fieldsDictionary.Add(dataReader.GetName(i), dataReader.GetValue(i).ToString());
                                    }
                                    resultsList.Add(fieldsDictionary);
                                }
                            }
                        }

                        if (resultsList.Count > 0)
                        {
                            results = resultsList;// Convert<T>(resultsList);
                        }
                        else
                        {


                            switch (resultsDictionary.Count)
                            {
                                case 0:
                                    var commandStringLower = commandString.ToLower();
                                    //if (!commandStringLower.StartsWith("select") && !commandStringLower.Contains("count"))
                                    if (commandStringLower.Contains("count") || commandStringLower.Contains("delete") ||
                                        commandStringLower.Contains("update"))
                                    {
                                        // results.Add(new List<Dictionary<<string, object>>("results", dataReader.RecordsAffected));
                                    }
                                    else
                                    {
                                        //bad mambo jambo
                                    }
                                    break;
                                case 1:
                                    if (resultsDictionary.Count == 1)
                                    {
                                        //var fieldsForResults = new List<string> { "", "Data" };
                                        results.Add(new Dictionary<string, object>
                                        {
                                            { "results", resultsDictionary.FirstOrDefault().Value }
                                        });
                                        //var fieldName = resultsDictionary.Keys.First();
                                        //results.Add(fieldsForResults.Contains(fieldName)
                                        //    ? resultsDictionary[fieldName]
                                        //    : resultsDictionary);
                                    }
                                    else
                                    {
                                        //var resultsRow = resultsDictionary["0"].ChangeType<Dictionary<string,object>>();
                                        //var resultsRow =
                                        //    resultsDictionary.FirstOrDefault();
                                        //results = resultsRow;
                                        results.Add(resultsDictionary);
                                    }
                                    break;
                                default:
                                    results.Add(resultsDictionary);
                                    break;
                            }
                        }
                    }
                    if (dbConnection.State == ConnectionState.Open) dbConnection.Close();
                }
            }
            catch (Exception exception)
            {
                results.Add(new Dictionary<string, object>
                {
                    { "Error", $"Error in {Namespace}.App({source},{connectionString}):{exception}" }
                });
                //results = Convert<T>($"Error in {Namespace}.GetResults<{typeof(T)}>({source},{Convert<string>(parameters)}): {Interfaces.Log.DefaultProvider.GetExceptionMessage(exception)}");
            }

            return results;
        }

        public static string GetConnectionString(string[] args)
        {
            var connectionStringId = args != null && args.Length > 0 && args[0] != null && !string.IsNullOrEmpty(args[0]) ? args[0] : "AppBlocks"; //If this fails, we try DefaultConnection

            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = configurationBuilder.Build();
            var connectionString = connectionStringId.IndexOf("=") != -1 ? connectionStringId : configuration.GetConnectionString(connectionStringId);
            ////$"Server=.\\;Database={typeof(AppBlocksDbContext).Namespace};Trusted_Connection=True;MultipleActiveResultSets=true;Application Name=AppBlocks.Web.Dev"

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("ConnectionString");
            }

            return connectionString;
        }

        public static IDbConnection CreateConnection(string connectionString, DatabaseType dbtype = DatabaseType.SqlServer)
        {
            if (string.IsNullOrEmpty(connectionString)) return null;

            IDbConnection dbConnection;

            switch (dbtype)
            {
                //case DatabaseType.Access:
                //    dbConnection = new OleDbConnection(connectionString);
                //    break;
                case DatabaseType.SqlServer:
                    dbConnection = new SqlConnection(connectionString);
                    break;
                //case DatabaseType.Oracle:
                //    cnn = new OracleConnection(connectionString);
                //    break;
                default:
                    dbConnection = new SqlConnection(connectionString);
                    break;
            }

            return dbConnection;
        }

        public static IDbCommand CreateCommand(string commandText, IDbConnection dbConnection, DatabaseType databaseType = DatabaseType.SqlServer)
        {
            IDbCommand dbCommand;
            switch (databaseType)
            {
                //case DatabaseType.Access:
                //    dbCommand = new OleDbCommand(commandText, (OleDbConnection)dbConnection);
                //    break;
                //case DatabaseType.SqlServer:
                //    cmd = new SqlCommand(commandText, (SqlConnection)cnn);
                //    break;
                //case DatabaseType.Oracle:
                //    cmd = new OracleCommand(commandText, (OracleConnection)cnn);
                //    break;
                default:
                    dbCommand = new SqlCommand(commandText, (SqlConnection)dbConnection);
                    break;
            }
            dbCommand.Connection.Open();
            return dbCommand;
        }

        public static DbDataAdapter CreateAdapter(IDbCommand cmd, DatabaseType dbtype = DatabaseType.SqlServer)
        {
            DbDataAdapter dbDataAdapter;
            switch (dbtype)
            {
                //case DatabaseType.Access:
                //    dbDataAdapter = new OleDbDataAdapter((OleDbCommand)cmd);
                //    break;
                //case DatabaseType.SqlServer:
                //    da = new SqlDataAdapter((SqlCommand)cmd);
                //    break;
                //case DatabaseType.Oracle:
                //    da = new OracleDataAdapter((OracleCommand)cmd);
                //    break;
                default:
                    dbDataAdapter = new SqlDataAdapter((SqlCommand)cmd);
                    break;
            }

            return dbDataAdapter;
        }

        ////TODO: Cache this sucker
        //private static string GetConnectionString(string connectionString)
        //{
        //    var results = string.Empty;
        //    var configConnectionString = ConfigurationManager.ConnectionStrings[connectionString];
        //    if (configConnectionString != null)
        //    {
        //        results = ConfigurationManager.ConnectionStrings["AppBlocks"].ConnectionString;
        //    }
        //    //connectionString = Settings.GetSetting(connectionString ?? "Db", defaultConnectionString, Namespace);
        //    if (string.IsNullOrEmpty(connectionString))
        //    {
        //        results = Settings.GetSetting("AppBlocks.Settings.ConnectionString.Default.Details");
        //    }
        //    return results;
        //}
    }
}