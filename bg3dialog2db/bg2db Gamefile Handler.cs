using LSLib.LS;
using LSLib.LS.Enums;
using LSLib.LS.Story;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static bg3dialog2db.Json;

namespace bg3dialog2db
{
    static public class Que //Database insertion queuing functions
    {
        
        //Null check (also flattens all 0 UUID and larian empty messages to null)
        static bool Forbidden(string ValueString)
        {
            if (ValueString == null)
            {
                return true;
            }

            foreach (string BadString in Params.NullStringArray)
            {
                if (ValueString == BadString)
                {
                    return true;
                }
            }

            return false;
        }

        //Cue a null value for database insertion
        static void AddNull(SqliteCommand DbCom, string QueKey)
        {
            DbCom.Parameters.Add(new SqliteParameter($"@{QueKey}", DBNull.Value));
        }

        //Cue a kev-value pair for database insertion after null check and optional int conversion
        static public void AddValue(SqliteCommand DbCom, string QueKey, string QueVal, bool QueInt = false)
        {
            if (Forbidden(QueVal))
            {
                AddNull(DbCom, QueKey);
            }
            else if (QueInt)
            {
                DbCom.Parameters.Add(new SqliteParameter($"@{QueKey}", int.Parse(QueVal)));
            }
            else
            {
                DbCom.Parameters.Add(new SqliteParameter($"@{QueKey}", QueVal));
            }
        }

        //Cue a key-value pair for database insertion from an XML node attribute after null check and optional int conversion
        static public void PropertyValue(SqliteCommand DbCom, Dictionary<string, string> QueNodeDict, string QueAttributeName, bool QueInt = false)
        {
            if (QueNodeDict.TryGetValue(QueAttributeName, out string QueAttributeValue))
            {
                AddValue(DbCom, QueAttributeName, QueAttributeValue, QueInt);
                return;
            }
            AddNull(DbCom, QueAttributeName);
        }

        //Cue a batch of key-value pairs for database insertion from XML node attributes after null check - assumes all strings
        static public void PropertyValueStrings(SqliteCommand DbCom, Dictionary<string, string> QueNodeDict, string[] QueAttributes)
        {
            foreach (string QueAttribute in QueAttributes)
            {
                PropertyValue(DbCom, QueNodeDict, QueAttribute);
            }
        }
    }

    static public class Parse //Larain Gamefile Parser
    {
        static bool NodeIs(XmlNode Node, String TargetID)//Private check function. If it returns false in this context, somebody fucked up
        {
            return (Node.Attributes.GetNamedItem("id").Value == TargetID);
        }

        static public void IngestTags(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode TagNode = Fetch.Node(pakDoc);
        }

        static public void IngestFlags(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode FlagNode = Fetch.Node(pakDoc);
            if (!NodeIs(FlagNode, "Flags"))
            {
                throw new ArgumentException("Node passed to Parse.Flags is not a Flags node");
            }
            DbCom.Parameters.Clear();
            DbCom.CommandText = "INSERT or REPLACE INTO Flags VALUES (@UUID,@Name,@Description,@Usage,@Source)";
            Dictionary<string, string> FlagDict = Fetch.Properties(FlagNode);
            Que.PropertyValueStrings(DbCom, FlagDict, new string[] { "UUID", "Name", "Description" });  
            Que.PropertyValue(DbCom, FlagDict, "Usage", true);
            Que.AddValue(DbCom, "Source", pakFile.Name);
            DbCom.ExecuteNonQuery();
        }

        static public void IngestQuests(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode QuestNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestReactions(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode ReactNode = Fetch.Node(pakDoc, "/save/region/node/children/node");
        }

        static public void IngestDC(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode DiffNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestNamesMerged(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode NameMerdNode = Fetch.Node(pakDoc);
        }

        static public void IngestNamesOrigins(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode NameOriNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestSpeakerGroups(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.LSX_XML(pakFile);
            XmlNode SpeakGroupNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestAudio(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            XmlDocument pakDoc = Fetch.LSX_XML(pakFile);
            XmlNode AudNode = Fetch.Node(pakDoc, "/save/region/node/children/node/children/node/children");
        }

        static public void IngestSwitch(PackagedFileInfo pakFile, SqliteCommand DbCom)
        {
            if (PakTest.IsTag(pakFile))
                IngestTags(pakFile, DbCom);
            else if (PakTest.IsFlag(pakFile))
                IngestFlags(pakFile, DbCom);
            else if (PakTest.IsQuest(pakFile))
                IngestQuests(pakFile, DbCom);
            else if (PakTest.IsReaction(pakFile))
                IngestReactions(pakFile, DbCom);
            else if (PakTest.IsDC(pakFile))
                IngestDC(pakFile, DbCom);
            else if (PakTest.IsMerged(pakFile))
                IngestNamesMerged(pakFile, DbCom);
            else if (PakTest.IsOrigin(pakFile))
                IngestNamesOrigins(pakFile, DbCom);
            else if (PakTest.IsSpeakerGroup(pakFile))
                IngestSpeakerGroups(pakFile, DbCom);
            else if (PakTest.IsAudio(pakFile))
                IngestAudio(pakFile, DbCom);
            else
                return;//Just to make it explicit
        }

        static public void IngestPak(string pakFilePath, SqliteCommand DbCom)
        {
            PackageReader pakReader = new();//Invoke LsLib
            using Package pak = pakReader.Read(pakFilePath);
            foreach (PackagedFileInfo pakFile in pak.Files)
            {
                try
                {
                    IngestSwitch(pakFile, DbCom);//Ingest offloaded for error handeling and potential future parallelization
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error ingesting {pakFile.Name} from {Path.GetFileName(pakFilePath)}: {e.Message}");
                }
            }
        }

    }
}
