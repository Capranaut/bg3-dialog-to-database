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
        static SqliteCommand dbCommand = new();
        static bool dbLive = false;
        static bool dbInitialized = false;
        static bool dbLocked;

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

        public static void Clear()
        {
            dbCommand.Parameters.Clear();
        }

        public static void LoadCom(string payload)
        {
            Clear();
            dbCommand.CommandText = payload;
        }
        public static void LoadNull(string key)
        {
            dbCommand.Parameters.Add(new SqliteParameter($"@{key}", DBNull.Value));
        }

        public static void LoadParam(string key)
        {
            LoadNull(key);
        }

        public static void LoadParam<T>(string key, T val)
        {
            dbCommand.Parameters.Add(new SqliteParameter($"@{key}", val));
        }

        /*public static void LoadParam(string key, string val)
        {
            dbCommand.Parameters.Add(new SqliteParameter($"@{key}", val));
        }

        public static void LoadParam(string key, int val)
        {
            dbCommand.Parameters.Add(new SqliteParameter($"@{key}", val));
        }

        public static void LoadParam(string key, bool val)
        {
            dbCommand.Parameters.Add(new SqliteParameter($"@{key}", val));
        }*/

        public static void ExecuteNonQuery()
        {
            dbCommand.ExecuteNonQuery();
            Clear();
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

        public static void ExecuteCommand(string payload) //Prareterless Non-Query
        {
            Clear();
            LoadCom(payload);
            ExecuteNonQuery();
        }

        public static void Begin()
        {
            if (!dbLocked)
            {
                ExecuteCommand("begin");
                dbLocked = true;
            }
        }

        public static void End()
        {
            if(dbLocked)
            {
                ExecuteCommand("end");
                dbLocked = false;
            }
        }

        public static void Make()
        {
            //TODO log warning if already made
            Close();
            Delete();
            Open();

            ExecuteCommand("CREATE TABLE Meta (" +
                "UUID TEXT, " +
                "id TEXT, " +
                "value TEXT, " +
                "Source TEXT)");
            //ExecuteCommand("CREATE UNIQUE INDEX CategoryIDX ON Categories(UUID)");

            ExecuteCommand("CREATE TABLE Flags (" +
                "UUID TEXT, " +
                "Name TEXT, " +
                "Description TEXT, " +
                "Usage INTEGER, " +
                "Source TEXT)");
            ExecuteCommand("CREATE UNIQUE INDEX FlagIDX ON Flags(UUID)");

            ExecuteCommand("CREATE TABLE Tags (" +
                "UUID TEXT, " +
                "Name TEXT, " +
                "DisplayName TEXT, " +
                "DisplayDescription TEXT, " +
                "Icon TEXT, " +
                "Description TEXT, " +
                "Source TEXT)");
            ExecuteCommand("CREATE UNIQUE INDEX TagIDX ON Tags(UUID)");

            ExecuteCommand("CREATE TABLE Localization (" +
                "UUID TEXT, " +
                "Line TEXT)");
            ExecuteCommand("CREATE UNIQUE INDEX LocIDX ON Localization(UUID)");

            ExecuteCommand("CREATE TABLE Quests (" +
                "UUID TEXT, " +
                "QuestID TEXT, " +
                "QuestTitle TEXT, " +
                "CategoryID TEXT, " +
                "ParentQuestID TEXT, " +
                "QuestVisiblity BOOL, " +
                "QuestRewardTarget INT, " +
                "SortingPriority INT, " +
                "Source TEXT)");
            ExecuteCommand("CREATE UNIQUE INDEX QuestIDX ON Quests(UUID)");

            ExecuteCommand("CREATE TABLE QuestSteps (" +
                "UUID TEXT, " +
                "QuestUUID TEXT, " +
                "Achievement TEXT, " +
                "Description TEXT, " +
                "DevComment TEXT, " +
                "DialogFlagGUID TEXT, " +
                "ExperienceReward TEXT, " +
                "ID TEXT, " +
                "LevelOverride INT, " +
                "Objective TEXT, " +
                "QuestRewardCount INT, " +
                "QuestRewardLevel INT, " +
                "QuestTitleOverride TEXT, " +
                "ReputationGain INT, " +
                "RewardAdditionalGold TEXT, " +
                "RewardAdditionalOwnerGUID TEXT, " +
                "RewardAdditionalOwnerLevelName TEXT, " +
                "RewardAdditionalOwnerName TEXT, " +
                "RewardAdditionalTreasureTable TEXT, " +
                "StatTriggerGUID TEXT, " +
                "UnlockDisable INT, " +
                "Source TEXT)");
            ExecuteCommand("CREATE UNIQUE INDEX QuestStepIDX ON QuestSteps(UUID)");

            dbInitialized = true;
        }
    }
}