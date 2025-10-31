using LSLib.LS;
using LSLib.LS.Enums;
using LSLib.LS.Story;
using LSLib.LS.Story.HeaderParser;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

        //Cue a kev-value pair for database insertion after null check and optional int conversion
        static public void AddValue(string QueKey, string QueVal, bool QueInt = false)
        {
            if (Forbidden(QueVal))
            {
                BgSQLite.LoadNull(QueKey);
            }
            else if (QueInt)
            {
                BgSQLite.LoadParam(QueKey, int.Parse(QueVal));
            }
            else
            {
                BgSQLite.LoadParam(QueKey, QueVal);
            }
        }

        //Cue a key-value pair for database insertion from an XML node attribute after null check and optional int conversion from direct XML read
        static public void PropertyValue(XmlNode QueNode, string QueAttributeName, bool QueInt = false)
        {
            XmlNode ValNode = QueNode.Attributes.GetNamedItem(QueAttributeName);
            if (ValNode != null)
            {
                AddValue(QueAttributeName, ValNode.Value, QueInt);
            }
        //blank handeling
        return;
        }

        //Cue a key-value pair for database insertion from an XML node attribute after null check and optional int conversion from dict read
        static public void PropertyValue(Dictionary<string, string> QueNodeDict, string QueAttributeName, bool QueInt = false)
        {
            if (QueNodeDict.TryGetValue(QueAttributeName, out string QueAttributeValue))
            {
                AddValue(QueAttributeName, QueAttributeValue, QueInt);
                return;
            }
            BgSQLite.LoadNull(QueAttributeName);
        }

        //Cue a batch of key-value pairs for database insertion from XML node attributes after null check from direct XML read - assumes all strings
        static public void PropertyValueStrings(XmlNode QueNode, string[] QueAttributes)
        {
            foreach (string QueAttribute in QueAttributes)
            {
                PropertyValue(QueNode, QueAttribute);
            }
        }

        //Cue a batch of key-value pairs for database insertion from XML node attributes after null check from dict read - assumes all strings
        static public void PropertyValueStrings(Dictionary<string, string> QueNodeDict, string[] QueAttributes)
        {
            foreach (string QueAttribute in QueAttributes)
            {
                PropertyValue(QueNodeDict, QueAttribute);
            }
        }
    }

    static public class Parse //Larain Gamefile Parser
    {
        static bool NodeIs(XmlNode Node, String TargetID)//Private check function. If it returns false in this context, somebody fucked up
        {
            return (Node.Attributes.GetNamedItem("id").Value == TargetID);
        }

        static void IngestMeta(XmlNode MetaNode, string TagUUID, string Source)
        {
            if (MetaNode.FirstChild == null)
            {
                return;//TODO log empy cat
            }

            foreach (XmlNode SubNode in MetaNode.FirstChild.ChildNodes)
            {
                string TagID = SubNode.Attributes.GetNamedItem("id").Value;//Uncaught, becuase if there's something there, surely it has an ID right?
                XmlNode SubNodePayload = SubNode.FirstChild;
                if (!NodeIs(SubNodePayload, "Name"))
                {
                    throw new ArgumentException("Node passed to Parse.Meta contains an attribute other than name");
                }

                BgSQLite.LoadCom("INSERT or REPLACE INTO Meta VALUES (@UUID,@id,@value,@Source)");
                Que.AddValue("UUID", TagUUID);
                Que.AddValue("id", TagID);
                Que.AddValue("Source", Source);
                Que.PropertyValue(SubNodePayload, "value");
                BgSQLite.ExecuteNonQuery();
            }
        }

        static void IngestMetas(XmlNode MetasNode, string TagUUID, string Source)
        {
            if (MetasNode == null || !MetasNode.HasChildNodes)
            {
                return;//TODO Some Logging. That specific file didn't have any
            }
            foreach (XmlNode MetaNode in MetasNode.ChildNodes)
            {
                IngestMeta(MetaNode, TagUUID, Source);
            }
            
        }

        static public void IngestTags(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode TagNode = Fetch.Node(pakDoc, "/save/region/node");
            if (!NodeIs(TagNode, "Tags"))
            {
                throw new ArgumentException("Node passed to Parse.Tags is not a Tag node");
            }
            BgSQLite.LoadCom("INSERT or REPLACE INTO Tags VALUES (@UUID,@Name,@DisplayName,@DisplayDescription,@Icon,@Description,@Source)");
            Dictionary<string, string> TagDict = Fetch.Properties(TagNode);
            Que.PropertyValueStrings(TagDict, new string[] { "UUID", "Name", "DisplayName", "DisplayDescription", "Icon", "Description" });
            Que.AddValue("Source", pakFile.Name);
            BgSQLite.ExecuteNonQuery();

            XmlNode MetasNode = Fetch.Node(pakDoc, "/save/region/node/children");
            IngestMetas(MetasNode, TagDict["UUID"], pakFile.Name);
        }

        static public void IngestFlags(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode FlagNode = Fetch.Node(pakDoc);
            if (!NodeIs(FlagNode, "Flags"))
            {
                throw new ArgumentException("Node passed to Parse.Flags is not a Flags node");
            }
            BgSQLite.LoadCom("INSERT or REPLACE INTO Flags VALUES (@UUID,@Name,@Description,@Usage,@Source)");
            Dictionary<string, string> FlagDict = Fetch.Properties(FlagNode);
            Que.PropertyValueStrings(FlagDict, new string[] { "UUID", "Name", "Description" });
            Que.PropertyValue(FlagDict, "Usage", true);
            Que.AddValue("Source", pakFile.Name);
            BgSQLite.ExecuteNonQuery();
        }

        static public void IngestQuests(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode QuestNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestReactions(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode ReactNode = Fetch.Node(pakDoc, "/save/region/node/children/node");
        }

        static public void IngestDC(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode DiffNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestNamesMerged(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML_Conditional(pakFile);
            XmlNode NameMerdNode = Fetch.Node(pakDoc);
        }

        static public void IngestNamesOrigins(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.XML(pakFile);
            XmlNode NameOriNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestSpeakerGroups(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML(pakFile);
            XmlNode SpeakGroupNode = Fetch.Node(pakDoc, "/save/region/node/children");
        }

        static public void IngestAudio(PackagedFileInfo pakFile)
        {
            XmlDocument pakDoc = Fetch.LSX_XML(pakFile);
            XmlNode AudNode = Fetch.Node(pakDoc, "/save/region/node/children/node/children/node/children");
        }

        static public void IngestLang(PackagedFileInfo pakFile)//Called sepeartly from switch
        {
            using var pakStream = pakFile.CreateContentReader();
            var fileExt = LocaUtils.ExtensionToFileFormat(pakFile.Name);
            var locReader = LocaUtils.Load(pakStream, fileExt);

            BgSQLite.LoadCom("INSERT or REPLACE INTO Localization VALUES (@UUID,@Line)");
            foreach (var entry in locReader.Entries)
            {
                BgSQLite.LoadParam("UUID", entry.Key);
                BgSQLite.LoadParam("Line", entry.Text);
                BgSQLite.ExecuteNonQuery();
            }
        }

        static public void IngestSwitch(PackagedFileInfo pakFile)
        {
            if (PakTest.IsTag(pakFile))
                IngestTags(pakFile);
            else if (PakTest.IsFlag(pakFile))
                IngestFlags(pakFile);
            else if (PakTest.IsQuest(pakFile))
                IngestQuests(pakFile);
            else if (PakTest.IsReaction(pakFile))
                IngestReactions(pakFile);
            else if (PakTest.IsDC(pakFile))
                IngestDC(pakFile);
            else if (PakTest.IsMerged(pakFile))
                IngestNamesMerged(pakFile);
            else if (PakTest.IsOrigin(pakFile))
                IngestNamesOrigins(pakFile);
            else if (PakTest.IsSpeakerGroup(pakFile))
                IngestSpeakerGroups(pakFile);
            else if (PakTest.IsAudio(pakFile))
                IngestAudio(pakFile);
            else
                return;//Just to make it explicit
        }

        static public void IngestPak(string pakFilePath)
        {
            BgSQLite.Begin();
            PackageReader pakReader = new();//Invoke LsLib
            using Package pak = pakReader.Read(pakFilePath);
            foreach (PackagedFileInfo pakFile in pak.Files)
            {
                try
                {
                    IngestSwitch(pakFile);//Ingest offloaded for error handeling and potential future parallelization
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error ingesting {pakFile.Name} from {Path.GetFileName(pakFilePath)}: {e.Message}");
                }
            }
            BgSQLite.End();
        }

        static public void IngestLocalization(string GamePath, string locLang)
        {
            BgSQLite.Begin();
            string langPakPath = $"{GamePath}\\Localization\\{locLang}\\{locLang}.pak";
            if (locLang == "English")//Larian just had to have an edge case
                langPakPath = $"{GamePath}\\Localization\\English.pak";

            PackageReader pakReader = new();//Invoke LsLib
            using Package pak = pakReader.Read(langPakPath);
            foreach (PackagedFileInfo pakFile in pak.Files)
            {
                if (PakTest.IsLang(pakFile, locLang))
                {
                    try
                    {
                        IngestLang(pakFile);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error ingesting {pakFile.Name} from {Path.GetFileName(langPakPath)}: {e.Message}");
                    }
                }
            }
            
        }
    }
}
