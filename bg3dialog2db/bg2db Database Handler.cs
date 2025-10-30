using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static bg3dialog2db.Json;

namespace bg3dialog2db
{
    public static class BgSQLite //SQLite handler functions
    {
        const string dbFilename = "BG3 New Database.db";
        const string dbConnectionString = $"Data Source={dbFilename}";

        static SqliteConnection dbConnection = new(dbConnectionString);
        public static SqliteCommand dbCommand = new();
        static bool dbLive = false;
        static bool dbInitialized = false;

        public static void Close() //Close if open
        {
            if(dbLive)
            {
                dbConnection.Close();
                dbLive = false;
            }
        }

        public static bool Exists()
        { 
           return File.Exists(dbFilename);
        }

        public static bool Initialized()
        {
            return dbInitialized;
        }

        public static void Delete()
        {
            File.Delete(dbFilename); //System.IO.IOException TODO build soft catch
        }

        public static string Age()
        {
            if (!Exists())
            {
                return "(Database: Not Found)";
            }

            DateTime fd = File.GetCreationTime(dbFilename);
            return "(Database Created: " + fd.ToString() + ")";
        }

        public static void Open() //Open if not already open
        {
            if (!dbLive)
            {
                dbConnection.Open();
                dbCommand.Connection = dbConnection;
                dbLive = true;
            }
        }

        public static void NQ(string payload) //Prareterless Non-Query
        {
            Open();
            dbCommand.Parameters.Clear();
            dbCommand.CommandText = payload;
            dbCommand.ExecuteNonQuery();
        }

        public static void Make()
        {
            //TODO log warning if already made
            Close();
            Delete();
            Open();

            NQ("CREATE TABLE Categories (" +
                "UUID TEXT, " +
                "Name TEXT, " +
                "Value TEXT, " +
                "Source TEXT)");
            NQ("CREATE UNIQUE INDEX CategoryIDX ON Categories(UUID)");

            NQ("CREATE TABLE Flags (" +
                "UUID TEXT, " +
                "Name TEXT, " +
                "Description TEXT, " +
                "Usage INTEGER, " +
                "Source TEXT)");
            NQ("CREATE UNIQUE INDEX FlagIDX ON Flags(UUID)");

            NQ("CREATE TABLE Tags (" +
                "UUID TEXT, " +
                "Name TEXT, " +
                "DisplayName TEXT, " +
                "DisplayDescription TEXT, " +
                "Icon TEXT, " +
                "Description TEXT, " +
                "Source TEXT)");
            NQ("CREATE UNIQUE INDEX TagIDX ON Tags(UUID)");

            dbInitialized = true;
        }

        //Tags
        //QuestFlags
        //QuestGroupFlags?
        //Reactions
        //DC
        //Names Merged
        //Names Origin
        //(combine to Names?)
        //Speaker Groups
        //Aud



    }
}